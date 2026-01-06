using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    // Prevents vanilla JobGiver_AIAbilityFight from running before pawn.abilities exists / contains the ability.
    public class ThinkNode_ConditionalHasAbility : ThinkNode_Conditional
    {
        public AbilityDef ability; // set in XML as <ability>WebShot</ability>

        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;
            if (ability == null) return false;

            var tracker = pawn.abilities;
            if (tracker == null) return false;

            // GetAbility exists in 1.6 AbilityTracker
            return tracker.GetAbility(ability) != null;
        }
    }
}