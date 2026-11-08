using RoR2;
using UnityEngine;

namespace FirstMod.Changes.Economy {
    public class KillBounty {

        private const float VanillaMultiplier = 2f;
        private const float StartMultiplier = 2.25f;   // used early
        private const float FloorMultiplier = 0.75f; // where it levels off
        private const float StartCoeff = 1f;         // decay begins at this coefficient
        private const float Decay = 4f; 

        public static void Init() {
            On.RoR2.DeathRewards.OnKilledServer += DeathRewards_OnKilledServer;
        }

        public static void Destroy() {
            On.RoR2.DeathRewards.OnKilledServer -= DeathRewards_OnKilledServer;
        }

        private static void DeathRewards_OnKilledServer(On.RoR2.DeathRewards.orig_OnKilledServer orig,
                DeathRewards self, DamageReport damagereport) {
            if (self.goldReward > 0) {
                uint vanilla = self.goldReward;
                float coeff = Run.instance.compensatedDifficultyCoefficient;
                float m = Multiplier(coeff);
                float scaled = vanilla * (m / VanillaMultiplier);
                uint paid = (uint)Mathf.Max(1f, Mathf.Round(scaled));
                self.goldReward = paid;

                FirstModPlugin.Log.LogInfo(
                    $"[KillBounty] stage={Run.instance.stageClearCount + 1} t={Run.instance.GetRunStopwatch() / 60f:F1}m " +
                    $"coeff={coeff:F2} mult={m:F2} chest(25)={Run.instance.GetDifficultyScaledCost(25)} gold={vanilla}->{paid}");
            }

            orig(self, damagereport);
        }

        private static float Multiplier(float coeff) {
            float t = Mathf.Max(0f, coeff - StartCoeff);
            return FloorMultiplier + (StartMultiplier - FloorMultiplier) * Mathf.Exp(-t / Decay);
        }
    }
}
