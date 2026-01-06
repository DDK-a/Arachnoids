using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class ThinkNode_ConditionalOutsideHomeRadius : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Map == null) return false;

            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null || !comp.HasHome) return false;

            // Leash ONLY in Defend mode (prevents hunt/drag conflicts)
            if (comp.Mode != ArachnoidMode.Defend) return false;

            float dist = pawn.Position.DistanceTo(comp.HomeCell);
            return dist > comp.Radius;
        }
    }
}