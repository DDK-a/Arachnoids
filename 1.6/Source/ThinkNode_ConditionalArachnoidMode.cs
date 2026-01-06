using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class ThinkNode_ConditionalArachnoidMode : ThinkNode_Conditional
    {
        public ArachnoidMode mode = ArachnoidMode.Defend;

        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;
            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null) return false;
            return comp.Mode == mode;
        }
    }
}