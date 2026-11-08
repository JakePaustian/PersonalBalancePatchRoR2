using R2API.Utils;
using BepInEx;
using RoR2;
using PersonalBalancePatch.Changes.Economy;
using PersonalBalancePatch.Changes.Items.SingularityBand;
using PersonalBalancePatch.Changes.Items.DefiantGouge;
using PersonalBalancePatch.Changes.Items.Transcendence;
using PersonalBalancePatch.Changes.Global;
using PersonalBalancePatch.Changes.Survivors.Commando;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace PersonalBalancePatch
{
    // [BepInPlugin] is what makes BepInEx load this class.
    // GUID must be globally unique and never change once published: "Author.ModName" is the convention.
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInDependency("droppod.lookingglass", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class PersonalBalancePatchPlugin : BaseUnityPlugin
    {
        public const string PluginAuthor = "SSF";
        public const string PluginName = "PersonalBalancePatch";
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        // Config entries show up in BepInEx/config/SSF.PersonalBalancePatch.cfg (and r2modman's Config editor).

        // Awake runs once, when BepInEx loads the plugin (at game startup, before the title screen).
        private void Awake()
        {
            Log = Logger;

            // ---- Concept 1: MMHOOK "On." hooks ----
            // Every game method has a matching On.<Namespace>.<Class>.<Method> event.
            // Your handler receives `orig` (the original method) and decides when/whether to call it.
            LunarCoinChanges.Init();
            VoidBand.Init();
            KillBounty.Init();
            DefiantGougeChanges.Init();
            TranscendenceChanges.Init();
            GlobalChanges.Init();
            CommandoChanges.Init();

            // ---- Concept 2: the game's own events ----

        }

        // Update runs every frame (Unity MonoBehaviour lifecycle).
        private void Update()
        {
            TranscendenceChanges.Tick(); // fires delayed shield-recharge sounds

            if (Run.instance == null) return;

            var body = PlayerCharacterMasterController.instances.Count > 0
                ? PlayerCharacterMasterController.instances[0].master.GetBody()
                : null;
            if (!body) return;

            // if (Input.GetKeyDown(KeyCode.F2) && NetworkServer.active) {
            //     body.inventory.GiveItemPermanent(RoR2Content.Items.FallBoots);
            // }
            // if (Input.GetKeyDown(KeyCode.F3) && NetworkServer.active) {
            //     body.inventory.GiveItemPermanent(RoR2Content.Items.ShieldOnly);
            // }
        }

        // Always unhook in OnDestroy so hot-reload tools and clean shutdowns work.
        private void OnDestroy()
        {
            LunarCoinChanges.Destroy();
            VoidBand.Destroy();
            KillBounty.Destroy();
            DefiantGougeChanges.Destroy();
            TranscendenceChanges.Destroy();
            GlobalChanges.Destroy();
            CommandoChanges.Destroy();
        }
    }
}
