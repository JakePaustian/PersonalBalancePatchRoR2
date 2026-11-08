using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;

namespace PersonalBalancePatch.Changes.Items.Transcendence
{
    // Transcendence rework: shields recharge more slowly the more Transcendence you hold.
    //   wait before recharge starts = 7s + 1s per stack   (vanilla: 7s)
    //   time to refill from empty   = 2s + 1s per stack   (vanilla: 2s)
    // Bodies without the item keep the vanilla numbers.
    public class TranscendenceChanges
    {
        private const float VanillaDelay = 7f;
        private const float VanillaRefill = 2f;
        private const float VanillaRate = 0.5f;            // shield fraction per second (1 / 2s)
        private const float ExtraDelayPerStack = 1f;
        private const float ExtraRefillPerStack = 1f;
        private const string LookingGlassGuid = "droppod.lookingglass";

        public static void Init() {
            IL.RoR2.HealthComponent.ServerFixedUpdate += ServerFixedUpdate_IL;
            On.RoR2.CharacterBody.OnOutOfDangerChanged += CharacterBody_OnOutOfDangerChanged;

            R2API.LanguageAPI.Add("ITEM_SHIELDONLY_PICKUP", BuildPickup(), "en");
            R2API.LanguageAPI.Add("ITEM_SHIELDONLY_DESC", BuildDescription(), "en");

            if (Chainloader.PluginInfos.ContainsKey(LookingGlassGuid))
                ItemCatalog.availability.CallWhenAvailable(PatchLookingGlass);
        }

        public static void Destroy() {
            IL.RoR2.HealthComponent.ServerFixedUpdate -= ServerFixedUpdate_IL;
            On.RoR2.CharacterBody.OnOutOfDangerChanged -= CharacterBody_OnOutOfDangerChanged;
            pendingSounds.Clear();
        }

        // ---- How many Transcendence stacks does this health component's body hold? ----
        private static int Stacks(HealthComponent hc) {
            CharacterBody body = hc ? hc.body : null;
            if (body && body.inventory)
                return body.inventory.GetItemCountEffective(RoR2Content.Items.ShieldOnly);
            return 0;
        }

        private static float DelaySeconds(int stacks) { return VanillaDelay + ExtraDelayPerStack * stacks; }
        private static float RefillSeconds(int stacks) { return VanillaRefill + ExtraRefillPerStack * stacks; }

        // ---- The IL edit ----
        private static void ServerFixedUpdate_IL(ILContext il) {
            ILCursor c = new ILCursor(il);

            // Edit 1: the regen condition is  (body.outOfDanger || isShieldRegenForced) && ...
            // After the call to body.outOfDanger, the bool result is on the stack. Hand it to our
            // method (together with the HealthComponent) and use ITS answer instead.
            if (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt<CharacterBody>("get_outOfDanger"))) {
                c.Emit(OpCodes.Ldarg_0); // push 'this' (the HealthComponent)
                c.EmitDelegate<Func<bool, HealthComponent, bool>>(ShieldMayRecharge);
            } else {
                PersonalBalancePatchPlugin.Log.LogError("Transcendence: could not find get_outOfDanger in ServerFixedUpdate; recharge delay NOT changed.");
            }

            // Edit 2: the refill line is  shield += body.maxShield * 0.5f * deltaTime
            // Find  get_maxShield ; ldc.r4 0.5  and replace the 0.5 with a computed rate.
            c.Index = 0;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxShield"),
                    x => x.MatchLdcR4(0.5f))) {
                c.Emit(OpCodes.Pop);     // throw away the 0.5
                c.Emit(OpCodes.Ldarg_0); // push 'this' (the HealthComponent)
                c.EmitDelegate<Func<HealthComponent, float>>(RefillRate);
            } else {
                PersonalBalancePatchPlugin.Log.LogError("Transcendence: could not find the shield refill rate in ServerFixedUpdate; refill time NOT changed.");
            }
        }

        // ---- The "shields are recharging" sound ----
        // Vanilla plays it the moment the body goes 'out of danger' (7s after damage). For holders the
        // shield doesn't actually start refilling until later, so we delay the sound by the same amount.
        // This runs on every machine (the flag change is synced), so each client plays its own sound.
        private struct PendingSound {
            public CharacterBody body;
            public float playAt;
        }

        private static readonly List<PendingSound> pendingSounds = new List<PendingSound>();

        private static void CharacterBody_OnOutOfDangerChanged(On.RoR2.CharacterBody.orig_OnOutOfDangerChanged orig, CharacterBody self) {
            int stacks = (self && self.inventory) ? self.inventory.GetItemCountEffective(RoR2Content.Items.ShieldOnly) : 0;
            if (stacks <= 0 || !self.outOfDanger) {
                orig(self); // no item, or going back IN danger: vanilla behaviour
                return;
            }

            // Holder just went out of danger. Vanilla would play the sound now; schedule it for later.
            HealthComponent hc = self.healthComponent;
            if (hc && hc.shield != hc.fullShield)
                pendingSounds.Add(new PendingSound { body = self, playAt = Time.time + ExtraDelayPerStack * stacks });

            self.MarkAllStatsDirty(); // the only other thing the vanilla method does
        }

        // Called every frame from the plugin's Update.
        public static void Tick() {
            for (int i = pendingSounds.Count - 1; i >= 0; i--) {
                PendingSound p = pendingSounds[i];
                if (!p.body) { pendingSounds.RemoveAt(i); continue; }
                if (Time.time < p.playAt) continue;

                pendingSounds.RemoveAt(i);
                HealthComponent hc = p.body.healthComponent;
                // Only play if still out of danger and the shield is still recharging.
                if (p.body.outOfDanger && hc && hc.shield < hc.fullShield) {
                    if (p.body.inventory && p.body.inventory.GetItemCountEffective(DLC3Content.Items.ShieldBooster) > 0)
                        Util.PlaySound("Play_item_use_shieldBooster_regen", p.body.gameObject);
                    Util.PlaySound("Play_item_proc_personal_shield_recharge", p.body.gameObject);
                }
            }
        }

        // Called in place of the plain  body.outOfDanger  check.
        private static bool ShieldMayRecharge(bool outOfDanger, HealthComponent hc) {
            if (!outOfDanger) return false;           // the vanilla 7 seconds haven't passed yet
            int stacks = Stacks(hc);
            if (stacks <= 0) return true;             // no item: vanilla behaviour
            // Item held: ALSO require the longer wait.
            return hc.body.outOfDangerStopwatch >= DelaySeconds(stacks);
        }

        // Called in place of the constant 0.5 (shield fraction refilled per second).
        private static float RefillRate(HealthComponent hc) {
            int stacks = Stacks(hc);
            if (stacks <= 0) return VanillaRate;
            return 1f / RefillSeconds(stacks);
        }

        // ---- Text ----
        private static string BuildPickup() {
            return "Convert all your health into shield. Increase maximum health. Shields recharge more slowly.";
        }

        private static string BuildDescription() {
            return "<style=cIsHealing>Convert</style> all but <style=cIsHealing>1 health</style> into <style=cIsHealing>regenerating shields</style>. "
                 + "<style=cIsHealing>Gain 50% <style=cStack>(+25% per stack)</style> maximum health</style>. "
                 + $"Shields start recharging <style=cIsDamage>{ExtraDelayPerStack:0.#}</style> <style=cStack>(+{ExtraDelayPerStack:0.#} per stack)</style> seconds later "
                 + $"and take <style=cIsDamage>{ExtraRefillPerStack:0.#}</style> <style=cStack>(+{ExtraRefillPerStack:0.#} per stack)</style> seconds longer to refill.";
        }

        // ---- LookingGlass: keep its existing row, add the two timings ----
        private static void PatchLookingGlass() {
            var lgAssembly = Chainloader.PluginInfos[LookingGlassGuid].Instance.GetType().Assembly;
            var defsType = lgAssembly.GetType("LookingGlass.ItemStatsNameSpace.ItemDefinitions");
            var table = (IDictionary)defsType.GetField("allItemDefinitions").GetValue(null);

            object def = table[(int)RoR2Content.Items.ShieldOnly.itemIndex];
            if (def == null) { PersonalBalancePatchPlugin.Log.LogWarning("LookingGlass has no Transcendence entry."); return; }
            var defType = def.GetType();

            var descriptions = (IList)defType.GetField("descriptions").GetValue(def);
            if (descriptions.Contains("Shield Recharge Delay: ")) return; // already patched

            var valueTypes = (IList)defType.GetField("valueTypes").GetValue(def);
            var units = (IList)defType.GetField("measurementUnits").GetValue(def);
            object utility = Enum.Parse(defType.GetNestedType("ValueType"), "Utility");
            object seconds = Enum.Parse(defType.GetNestedType("MeasurementUnits"), "Seconds");

            descriptions.Add("Shield Recharge Delay: "); valueTypes.Add(utility); units.Add(seconds);
            descriptions.Add("Shield Refill Time: ");    valueTypes.Add(utility); units.Add(seconds);

            var calcField = defType.GetField("calculateValuesFlat");
            var original = (Func<int, List<float>>)calcField.GetValue(def);
            Func<int, List<float>> wrapped = stack => {
                List<float> values = original(stack);
                values.Add(DelaySeconds(stack));
                values.Add(RefillSeconds(stack));
                return values;
            };
            calcField.SetValue(def, wrapped);

            PersonalBalancePatchPlugin.Log.LogInfo("Patched LookingGlass stats for Transcendence.");
        }
    }
}
