using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using HarmonyLib;

namespace PersonalBalancePatch.Changes.Economy
{ 
    public class LunarCoinChanges
    {

        private static Harmony harmony;

        public static void Init() {
            On.RoR2.PlayerCharacterMasterController.Awake += PlayerCharacterMasterController_Awake;
            GlobalEventManager.onDeathProcServer += OnDeathProcServerOverride;

            On.RoR2.NetworkUser.SyncLunarCoinsToServer += NetworkUser_SyncLunarCoinsToServer;
            Run.onRunStartGlobal += OnRunStartGlobal;

            On.RoR2.NetworkUser.RpcAwardLunarCoins += NetworkUser_RpcAwardLunarCoins;
            On.RoR2.NetworkUser.RpcDeductLunarCoins += NetworkUser_RpcDeductLunarCoins;

            harmony = Harmony.CreateAndPatchAll(typeof(LunarCoinsGetterPatch));
        }

        public static void Destroy() {
            On.RoR2.PlayerCharacterMasterController.Awake -= PlayerCharacterMasterController_Awake;
            GlobalEventManager.onDeathProcServer -= OnDeathProcServerOverride;
            
            On.RoR2.NetworkUser.SyncLunarCoinsToServer -= NetworkUser_SyncLunarCoinsToServer;
            Run.onRunStartGlobal -= OnRunStartGlobal;

            On.RoR2.NetworkUser.RpcAwardLunarCoins -= NetworkUser_RpcAwardLunarCoins;
            On.RoR2.NetworkUser.RpcDeductLunarCoins -= NetworkUser_RpcDeductLunarCoins;

            harmony?.UnpatchSelf();
        }

        // HOOK OVERRIDES
        private static void PlayerCharacterMasterController_Awake(On.RoR2.PlayerCharacterMasterController.orig_Awake orig,
                PlayerCharacterMasterController self) {
            orig(self);
            self.lunarCoinChanceMultiplier = 0;
        }

        private static void NetworkUser_SyncLunarCoinsToServer(On.RoR2.NetworkUser.orig_SyncLunarCoinsToServer orig, NetworkUser self) {
            return;
        }

        private static void NetworkUser_RpcAwardLunarCoins(On.RoR2.NetworkUser.orig_RpcAwardLunarCoins orig, NetworkUser self, uint count) {
            return;
        }

        private static void NetworkUser_RpcDeductLunarCoins(On.RoR2.NetworkUser.orig_RpcDeductLunarCoins orig, NetworkUser self, uint count) {
            return;
        }

        // EVENT SUBSCRIBERS
        private static void OnDeathProcServerOverride(DamageReport damageReport)
        {
            CharacterMaster characterMaster = damageReport.attackerMaster;
            if ((bool)characterMaster)
            {
                if ((bool)characterMaster.minionOwnership.ownerMaster)
                {
                    characterMaster = characterMaster.minionOwnership.ownerMaster;
                }
                PlayerCharacterMasterController component = characterMaster.GetComponent<PlayerCharacterMasterController>();
                if ((bool)component && Util.CheckRoll(2f))
                {
                    PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(RoR2Content.MiscPickups.LunarCoin.miscPickupIndex), damageReport.victim.transform.position, Vector3.up * 10f);
                }
            }
        }

        private static void OnRunStartGlobal(Run _) {
            if (!NetworkServer.active) return; // If not host

            foreach (NetworkUser user in NetworkUser.readOnlyInstancesList) {
                user.NetworknetLunarCoins = 0;
            }
        }
    }

    // HARMONY PATCHES
    [HarmonyPatch(typeof(NetworkUser), nameof(NetworkUser.lunarCoins), MethodType.Getter)]
    public static class LunarCoinsGetterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NetworkUser __instance, ref uint __result)
        {
            __result = __instance.NetworknetLunarCoins;
        }
    }
}
