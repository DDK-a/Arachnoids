using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    // During Hunt mode, reposition to a stand-off cell.
    // Crucially: NEVER issue Goto(victim). Only Goto(cell), and use Wait_Combat to stop.
    public class JobGiver_ArachnoidHuntApproachVictim : ThinkNode_JobGiver
    {
        private const int PreferredMinRange = 10;
        private const int PreferredMaxRange = 18;

        // If within this, we stop moving and let ability fire
        private const int StopAndShootRange = 20;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Dead || pawn.Downed) return null;

            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null || comp.Mode != ArachnoidMode.Hunt) return null;

            Pawn victim = comp.GetVictimPawn(pawn.Map);
            if (victim == null || victim.Dead)
            {
                comp.Mode = ArachnoidMode.Defend;
                comp.ClearVictim();
                return null;
            }

            if (victim.Downed) return null; // Drag mode should take over elsewhere

            // Keep "combat engaged" state while hunting, even if victim hasn't attacked.
            if (pawn.mindState != null)
            {
                pawn.mindState.enemyTarget = victim;
                pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
            }

            float dist = pawn.Position.DistanceTo(victim.Position);

            // If we're already reasonably close, STOP moving.
            // This prevents the endless "GoTo hugging" behavior and gives AIAbilityFight time to run.
            if (dist <= StopAndShootRange)
            {
                Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
                wait.expiryInterval = 30;
                wait.checkOverrideOnExpire = true;
                return wait;
            }

            // Otherwise reposition to a good web range cell near the victim.
            if (!TryFindStandOffCell(pawn, victim, out IntVec3 dest))
            {
                // Fallback: still go to a CELL near victim, not the victim itself.
                dest = CellFinder.RandomClosewalkCellNear(victim.Position, pawn.Map, PreferredMaxRange);
                if (!dest.InBounds(pawn.Map) || !dest.Standable(pawn.Map))
                    return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.expiryInterval = 60;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        private bool TryFindStandOffCell(Pawn hunter, Pawn victim, out IntVec3 dest)
        {
            Map map = hunter.Map;
            IntVec3 vPos = victim.Position;

            IntVec3 bestNoLos = IntVec3.Invalid;

            for (int r = PreferredMinRange; r <= PreferredMaxRange; r++)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(vPos, r, true))
                {
                    if (!c.InBounds(map)) continue;
                    if (!c.Standable(map)) continue;
                    if (!hunter.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    if (c.AdjacentTo8Way(vPos)) continue;

                    if (!bestNoLos.IsValid) bestNoLos = c;

                    if (GenSight.LineOfSight(c, vPos, map, skipFirstCell: true))
                    {
                        dest = c;
                        return true;
                    }
                }
            }

            if (bestNoLos.IsValid)
            {
                dest = bestNoLos;
                return true;
            }

            dest = IntVec3.Invalid;
            return false;
        }
    }
}