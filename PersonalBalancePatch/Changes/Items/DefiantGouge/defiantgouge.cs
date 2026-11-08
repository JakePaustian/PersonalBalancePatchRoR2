using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace PersonalBalancePatch.Changes.Items.DefiantGouge
{
    // Defiant Gouge rework:
    //  - Vanilla: using ANY shrine summons enemies.
    //  - Now: only a FAILED Shrine of Chance summons enemies. In exchange, holders fail less often,
    //    and each extra stack multiplies the failure chance down (never reaching zero).
    public class DefiantGougeChanges
    {
        private const float FailureAtOneStack = 0.25f;             // vanilla Shrine of Chance fails 45%
        private const float FailureMultiplierPerExtraStack = 0.8f; // each extra stack: failure x 0.8
        private const float CreditPerStack = 80f;                  // same as vanilla's encounter budget
        private const string LookingGlassGuid = "droppod.lookingglass";

        public static void Init() {
            // 1. Switch off the vanilla "any shrine" trigger (IL edit).
            IL.RoR2.GlobalEventManager.OnInteractionBegin += DisableVanillaTrigger;
            // 2. Lower the failure chance for holders, and punish failures.
            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;

            // 3. Text: item pickup/description and the chat message.
            R2API.LanguageAPI.Add("ITEM_MONSTERSONSHRINEUSE_PICKUP", BuildPickup(), "en");
            R2API.LanguageAPI.Add("ITEM_MONSTERSONSHRINEUSE_DESC", BuildDescription(), "en");
            R2API.LanguageAPI.Add("PERSONALBALANCEPATCH_GOUGE_WRONG_GOD", "<style=cShrine>{0} prayed to the wrong god...</color>", "en");
            R2API.LanguageAPI.Add("PERSONALBALANCEPATCH_GOUGE_WRONG_GOD_2P", "<style=cShrine>You prayed to the wrong god...</color>", "en");

            // 4. LookingGlass (optional).
            if (Chainloader.PluginInfos.ContainsKey(LookingGlassGuid))
                ItemCatalog.availability.CallWhenAvailable(PatchLookingGlass);
        }

        public static void Destroy() {
            IL.RoR2.GlobalEventManager.OnInteractionBegin -= DisableVanillaTrigger;
            On.RoR2.ShrineChanceBehavior.AddShrineStack -= ShrineChanceBehavior_AddShrineStack;
        }

        private static float FailureChance(int stacks) {
            return FailureAtOneStack * Mathf.Pow(FailureMultiplierPerExtraStack, Mathf.Max(1, stacks) - 1);
        }

        // ---- IL hook: make the vanilla Gouge block think the player has 0 stacks ----
        private static void DisableVanillaTrigger(ILContext il) {
            ILCursor c = new ILCursor(il);
            // Find:  ldsfld RoR2Content.Items.MonstersOnShrineUse ; callvirt Inventory.GetItemCountEffective
            bool found = c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.MonstersOnShrineUse)),
                x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)));
            if (!found) {
                PersonalBalancePatchPlugin.Log.LogError("DefiantGouge: could not find the item-count read in OnInteractionBegin; vanilla trigger is NOT disabled.");
                return;
            }
            // The count is now on the stack. Throw it away and push 0 instead.
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldc_I4_0);
        }

        // ---- Shrine of Chance hook ----
        private static void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig,
                ShrineChanceBehavior self, Interactor activator) {
            int stacks = 0;
            CharacterBody body = activator ? activator.GetComponent<CharacterBody>() : null;
            if (body && body.inventory)
                stacks = body.inventory.GetItemCountEffective(RoR2Content.Items.MonstersOnShrineUse);

            if (!NetworkServer.active || stacks <= 0) {
                orig(self, activator);
                return;
            }

            float vanillaFailure = self.failureChance;
            int successesBefore = self.successfulPurchaseCount;

            self.failureChance = Mathf.Min(vanillaFailure, FailureChance(stacks));
            try {
                orig(self, activator);
            } finally {
                self.failureChance = vanillaFailure; // other players' shrines stay vanilla
            }

            // A failed roll leaves the success counter unchanged.
            if (self.successfulPurchaseCount == successesBefore)
                Punish(self, activator, body, stacks);
        }

        private static void Punish(ShrineChanceBehavior shrine, Interactor activator, CharacterBody body, int stacks) {
            Chat.SendBroadcastChat(new Chat.SubjectFormatChatMessage {
                subjectAsCharacterBody = body,
                baseToken = "PERSONALBALANCEPATCH_GOUGE_WRONG_GOD"
            });

            GameObject encounterPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/Encounters/MonstersOnShrineUseEncounter");
            if (!encounterPrefab || !Stage.instance) return;

            Vector3 position = shrine.transform.position;
            GameObject encounter = UnityEngine.Object.Instantiate(encounterPrefab, position, Quaternion.identity);
            NetworkServer.Spawn(encounter);

            CombatDirector director = encounter.GetComponent<CombatDirector>();
            if (!director) { NetworkServer.Destroy(encounter); return; }

            float credit = CreditPerStack * Stage.instance.entryDifficultyCoefficient * stacks;
            DirectorCard card = director.SelectMonsterCardForCombatShrine(credit);
            if (card == null) { NetworkServer.Destroy(encounter); return; }

            director.CombatShrineActivation(activator, credit, card);
            EffectManager.SpawnEffect(
                LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/MonstersOnShrineUse"),
                new EffectData { origin = position, rotation = Quaternion.identity },
                transmit: true);
        }

        // ---- Text ----
        private static string BuildPickup() {
            return "Shrines of Chance fail less often, but failing summons enemies nearby.";
        }

        private static string BuildDescription() {
            int failPct = Mathf.RoundToInt(FailureAtOneStack * 100f);
            int cutPct = Mathf.RoundToInt((1f - FailureMultiplierPerExtraStack) * 100f);
            return $"<style=cIsUtility>Shrines of Chance</style> fail much less often: failure chance becomes <style=cIsUtility>{failPct}%</style> "
                 + $"<style=cStack>(each additional stack reduces it by {cutPct}%, multiplicatively)</style>. "
                 + "<style=cIsHealth>A failed Shrine of Chance summons enemies</style> nearby. <style=cIsUtility>Scales over time.</style>";
        }

        // ---- LookingGlass: keep its two existing rows, add a failure-chance row ----
        private static void PatchLookingGlass() {
            var lgAssembly = Chainloader.PluginInfos[LookingGlassGuid].Instance.GetType().Assembly;
            var defsType = lgAssembly.GetType("LookingGlass.ItemStatsNameSpace.ItemDefinitions");
            var table = (IDictionary)defsType.GetField("allItemDefinitions").GetValue(null);

            object def = table[(int)RoR2Content.Items.MonstersOnShrineUse.itemIndex];
            if (def == null) { PersonalBalancePatchPlugin.Log.LogWarning("LookingGlass has no Defiant Gouge entry."); return; }
            var defType = def.GetType();

            var descriptions = (IList)defType.GetField("descriptions").GetValue(def);
            if (descriptions.Contains("Shrine of Chance Failure: ")) return; // already patched

            descriptions.Add("Shrine of Chance Failure: ");
            ((IList)defType.GetField("valueTypes").GetValue(def))
                .Add(Enum.Parse(defType.GetNestedType("ValueType"), "Utility"));
            ((IList)defType.GetField("measurementUnits").GetValue(def))
                .Add(Enum.Parse(defType.GetNestedType("MeasurementUnits"), "Percentage"));

            var calcField = defType.GetField("calculateValuesFlat");
            var original = (Func<int, List<float>>)calcField.GetValue(def);
            Func<int, List<float>> wrapped = stack => {
                List<float> values = original(stack);
                values.Add(FailureChance(stack));
                return values;
            };
            calcField.SetValue(def, wrapped);

            PersonalBalancePatchPlugin.Log.LogInfo("Patched LookingGlass stats for Defiant Gouge.");
        }
    }
}
