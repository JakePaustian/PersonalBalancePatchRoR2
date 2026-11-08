using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.Projectile;

namespace PersonalBalancePatch.Changes.Items.SingularityBand
{ 
    public class VoidBand
    {
        public static void Init() {
            R2API.LanguageAPI.Add("ITEM_ELEMENTALRINGVOID_DESC", BuildDescription(), "en");
            On.RoR2.Projectile.ProjectileManager.FireProjectile_FireProjectileInfo += FireProjectile_FireProjectileInfo;
            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += CharacterBody_AddTimedBuff_BuffDef_float;
            if (Chainloader.PluginInfos.ContainsKey("droppod.lookingglass"))
                ItemCatalog.availability.CallWhenAvailable(PatchLookingGlass);
        }

        public static void Destroy() {
            On.RoR2.Projectile.ProjectileManager.FireProjectile_FireProjectileInfo -= FireProjectile_FireProjectileInfo;
            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float -= CharacterBody_AddTimedBuff_BuffDef_float;
        }

        private static void FireProjectile_FireProjectileInfo(On.RoR2.Projectile.ProjectileManager.orig_FireProjectile_FireProjectileInfo orig,
                ProjectileManager self, FireProjectileInfo info) {
            if (info.projectilePrefab && info.projectilePrefab.name == "ElementalRingVoidBlackHole") {
                info.damage *= 1.5f;
            }
            orig(self, info);
        }

        private static void CharacterBody_AddTimedBuff_BuffDef_float(On.RoR2.CharacterBody.orig_AddTimedBuff_BuffDef_float orig, 
                CharacterBody self, BuffDef buffDef, float duration) {
            if (buffDef == DLC1Content.Buffs.ElementalRingVoidCooldown && duration > 13) {
                return;
            } else orig(self, buffDef, duration);
        }

        private static string BuildDescription() {
            return "Hits that deal <style=cIsDamage>more than 400% damage</style> also fire a black hole that "
                + "<style=cIsUtility>draws enemies within 15m into its center</style>. "
                + "Lasts <style=cIsUtility>5</style> seconds before collapsing, dealing "
                + $"<style=cIsDamage>{150}%</style> <style=cStack>(+{150}% per stack)</style> TOTAL damage. "
                + $"Recharges every <style=cIsUtility>{13}</style> seconds. "
                + "<style=cIsVoid>Corrupts all Runald's and Kjaro's Bands</style>.";
        }

        private static void PatchLookingGlass()
        {
            var lgAssembly = Chainloader.PluginInfos["droppod.lookingglass"].Instance.GetType().Assembly;
            var defsType = lgAssembly.GetType("LookingGlass.ItemStatsNameSpace.ItemDefinitions");
            var table = (IDictionary)defsType.GetField("allItemDefinitions").GetValue(null);

            object def = table[(int)DLC1Content.Items.ElementalRingVoid.itemIndex];
            if (def == null) { PersonalBalancePatchPlugin.Log.LogWarning("LookingGlass has no Singularity Band entry."); return; }
            var defType = def.GetType();

            // Add a "Recharge Time" row once (the three lists must stay the same length).
            var descriptions = (IList)defType.GetField("descriptions").GetValue(def);
            if (!descriptions.Contains("Recharge Time: "))
            {
                descriptions.Add("Recharge Time: ");
                ((IList)defType.GetField("valueTypes").GetValue(def))
                    .Add(Enum.Parse(defType.GetNestedType("ValueType"), "Utility"));
                ((IList)defType.GetField("measurementUnits").GetValue(def))
                    .Add(Enum.Parse(defType.GetNestedType("MeasurementUnits"), "Seconds"));
            }

            // Replace the formula: Total Damage, Proc Coefficient, Recharge Time.
            Func<int, List<float>> calc = stack => new List<float> { stack * 1.5f, 1f, 13f};
            defType.GetField("calculateValuesFlat").SetValue(def, calc);

            PersonalBalancePatchPlugin.Log.LogInfo("Patched LookingGlass stats for Singularity Band.");
        }
    }
}
