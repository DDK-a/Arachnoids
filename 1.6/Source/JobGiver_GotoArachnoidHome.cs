using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class JobGiver_GotoArachnoidHome : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return null;

            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null || !comp.HasHome) return null;

            IntVec3 home = comp.HomeCell;
            if (!home.IsValid || !home.InBounds(pawn.Map)) return null;

            // If the exact home cell isn't standable, choose a nearby walkable cell.
            if (!home.Standable(pawn.Map))
            {
                home = CellFinder.RandomClosewalkCellNear(home, pawn.Map, 6);
                if (!home.IsValid) return null;
            }

            var job = JobMaker.MakeJob(JobDefOf.Goto, home);
            job.expiryInterval = 500;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }
}