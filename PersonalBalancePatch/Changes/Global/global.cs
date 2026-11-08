using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace PersonalBalancePatch.Changes.Global
{
    // Changes that apply to every player and not to one specific item.
    public class GlobalChanges
    {
        public static void Init() {
            IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer += HitGround_IL;
        }

        public static void Destroy() {
            IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer -= HitGround_IL;
        }

        // Fall damage: shields no longer shrink it.
        // Vanilla:  damage = (impact overspeed / 60) * body.maxHealth
        // Now:      damage = (impact overspeed / 60) * (body.maxHealth + body.maxShield)
        // The shield still absorbs damage first, as before. What changes is that a big shield makes the
        // fall hurt proportionally more, instead of being measured against a tiny health pool.
        private static void HitGround_IL(ILContext il) {
            ILCursor c = new ILCursor(il);
            // Find:  ldarg.1 (the CharacterBody) ; callvirt CharacterBody.get_maxHealth
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(1),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxHealth"))) {
                c.Emit(OpCodes.Ldarg_1); // push the CharacterBody again (the first one was consumed)
                c.EmitDelegate<Func<float, CharacterBody, float>>(FallDamagePool);
            } else {
                PersonalBalancePatchPlugin.Log.LogError("Global: could not find maxHealth in OnCharacterHitGroundServer; fall damage NOT changed.");
            }
        }

        private static float FallDamagePool(float maxHealth, CharacterBody body) {
            return body ? maxHealth + body.maxShield : maxHealth;
        }
    }
}
