using EntityStates.Commando;
using RoR2;

namespace PersonalBalancePatch.Changes.Survivors.Commando
{
    // Commando: Tactical Dive refreshes his other skills' cooldowns.
    //
    // Vanilla DodgeState.OnEnter already does  skillLocator.primary.Reset()  (that is the "reload" that
    // also refreshes Visions of Heresy's cooldown). We add the same for the secondary and special.
    // CombatDodge derives from DodgeState and calls base.OnEnter(), so it is covered as well.
    public class CommandoChanges
    {
        public static void Init() {
            On.EntityStates.Commando.DodgeState.OnEnter += DodgeState_OnEnter;

            R2API.LanguageAPI.Add("COMMANDO_UTILITY_DESCRIPTION",
                "<style=cIsUtility>Roll</style> a short distance. <style=cIsUtility>Refreshes the cooldowns</style> of your other skills.",
                "en");
        }

        public static void Destroy() {
            On.EntityStates.Commando.DodgeState.OnEnter -= DodgeState_OnEnter;
        }

        private static void DodgeState_OnEnter(On.EntityStates.Commando.DodgeState.orig_OnEnter orig, DodgeState self) {
            orig(self); // vanilla: plays the dive and resets the primary

            // Skill cooldowns belong to the player's own machine, so only that machine resets them
            // (the same rule vanilla uses for the primary).
            if (!self.isAuthority) return;

            SkillLocator skills = self.outer.GetComponent<SkillLocator>();
            if (!skills) return;

            if (skills.secondary) skills.secondary.Reset();
            if (skills.special) skills.special.Reset();
        }
    }
}
