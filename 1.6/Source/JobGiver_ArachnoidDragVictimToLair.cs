using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    /// <summary>
    /// Drag the current victim to the lair.
    /// Also supports "Defend capture": if in Defend mode and a freshly-downed, webbed enemy is found
    /// within a small radius of the lair, this will promote the spider into Drag mode and begin hauling.
    /// </summary>
    public class JobGiver_ArachnoidDragVictimToLair : ThinkNode_JobGiver
    {
        private const int DefendCaptureRadius = 20;
        private const int FreshDownedWindowTicks = 1200; // ~20s

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Dead) return null;

            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null || !comp.HasHome) return null;

            // Don't start a drag job if we're already carrying something.
            if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null) return null;

            // If we're in Defend mode, opportunistically capture ONLY if a valid downed pawn is near the lair.
            if (comp.Mode == ArachnoidMode.Defend)
            {
                Pawn nearby = TryFindDefendCaptureVictim(pawn, comp);
                if (nearby != null)
                {
                    comp.BeginDragCapture(nearby, Find.TickManager.TicksGame);

                    // Keep "combat engaged" state so other nodes remain consistent while dragging.
                    if (pawn.mindState != null)
                    {
                        pawn.mindState.enemyTarget = nearby;
                        pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                    }
                }
                else
                {
                    return null;
                }
            }

            // Standard drag mode behavior
            if (comp.Mode != ArachnoidMode.Drag) return null;

            Pawn victim = comp.GetVictimPawn(pawn.Map);
            if (victim == null || victim.Dead || !victim.Spawned)
            {
                comp.ClearVictim();
                comp.Mode = ArachnoidMode.Defend;
                return null;
            }

            if (!victim.Downed)
            {
                // If somehow not downed anymore, revert to hunt
                comp.Mode = ArachnoidMode.Hunt;
                return null;
            }

            JobDef dragDef = DefDatabase<JobDef>.GetNamedSilentFail("Arachnoid_DragVictimToLair");
            if (dragDef == null) return null;

            IntVec3 dropCell = FindLairDropCell(pawn.Map, comp.HomeCell);

            Job job = JobMaker.MakeJob(dragDef, victim, dropCell);
            job.count = 1;
            job.expiryInterval = 3000;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private Pawn TryFindDefendCaptureVictim(Pawn spider, CompArachnoidHome comp)
        {
            Map map = spider.Map;
            IntVec3 home = comp.HomeCell;

            if (!home.InBounds(map)) return null;

            // Only capture if the downed pawn is near the lair.
            // Prefer the closest eligible pawn to the spider (feels responsive).
            Pawn best = null;
            float bestDist = float.MaxValue;

            HediffDef webbedDef = DefDatabase<HediffDef>.GetNamedSilentFail("Webbed");

            foreach (IntVec3 c in GenRadial.RadialCellsAround(home, DefendCaptureRadius, true))
            {
                if (!c.InBounds(map)) continue;

                var thingList = c.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (!(thingList[i] is Pawn p)) continue;
                    if (p == null || p.Dead) continue;
                    if (!p.Spawned) continue;
                    if (!p.RaceProps.Humanlike) continue;
                    if (!p.Downed) continue;

                    // Don't yoink pawns already being carried or in a bed.
                    if (p.CarriedBy != null) continue;
                    if (p.CurrentBed() != null) continue;

                    // Must be an enemy (avoids kidnapping neutrals unless they are hostile)
                    if (!spider.HostileTo(p)) continue;

                    // "Fresh downed" approximation:
                    // - must be webbed (spider signature)
                    // - and must have been harmed recently
                    if (webbedDef != null && p.health?.hediffSet != null && !p.health.hediffSet.HasHediff(webbedDef))
                        continue;

                    int now = Find.TickManager.TicksGame;
                    int lastHarm = p.mindState != null ? p.mindState.lastHarmTick : -999999;
                    if (now - lastHarm > FreshDownedWindowTicks)
                        continue;

                    // Must be reachable for hauling
                    if (!spider.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;

                    float d = spider.Position.DistanceTo(p.Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
            }

            return best;
        }

        private IntVec3 FindLairDropCell(Map map, IntVec3 home)
        {
            // Prefer within 4 tiles (i.e. <5) and standable
            if (home.InBounds(map) && home.Standable(map))
                return home;

            // Find closest standable near home within 4 tiles; fall back to random closewalk
            for (int r = 1; r <= 4; r++)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(home, r, true))
                {
                    if (!c.InBounds(map)) continue;
                    if (!c.Standable(map)) continue;
                    return c;
                }
            }

            return CellFinder.RandomClosewalkCellNear(home, map, 4);
        }
    }
}