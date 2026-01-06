using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class JobDriver_ArachnoidDragVictimToLair : JobDriver
    {
        private Pawn Victim => job.targetA.Thing as Pawn;
        private IntVec3 DropCell => job.targetB.Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn victim = Victim;
            if (victim == null) return false;
            return pawn.Reserve(victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Victim == null || Victim.Dead);
            this.FailOn(() => !DropCell.IsValid || !DropCell.InBounds(pawn.Map));

            // Go to victim
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // Start carrying victim (version-safe)
            yield return StartCarryVictimToil();

            // Carry to drop cell
            yield return Verse.AI.Toils_Haul.CarryHauledThingToCell(TargetIndex.B);

            // Cocoon them instead of dropping on ground
            yield return CocoonAtCellToil();
        }

        private Toil StartCarryVictimToil()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Pawn victim = Victim;
                if (victim == null || victim.Dead)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (job.count <= 0) job.count = 1;

                if (pawn.carryTracker == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (pawn.carryTracker.CarriedThing == victim)
                    return;

                pawn.carryTracker.TryStartCarry(victim, 1);

                if (pawn.carryTracker.CarriedThing != victim)
                    EndJobWith(JobCondition.Incompletable);
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil CocoonAtCellToil()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Pawn victim = Victim;
                if (victim == null || victim.Dead)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Map map = pawn.Map;
                if (map == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                ThingDef cocoonDef = DefDatabase<ThingDef>.GetNamedSilentFail("Arachnoid_Cocoon");
                if (cocoonDef == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 c = DropCell;
                if (!c.InBounds(map) || !c.Standable(map))
                    c = pawn.Position;

                // If something is already there, try nearby
                if (c.GetEdifice(map) != null)
                    c = CellFinder.RandomClosewalkCellNear(c, map, 4);

                var cocoonThing = GenSpawn.Spawn(cocoonDef, c, map, WipeMode.Vanish) as Building_ArachnoidCocoon;
                if (cocoonThing == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Drop carried pawn into cocoon
                if (pawn.carryTracker?.CarriedThing == victim)
                {
                    pawn.carryTracker.TryDropCarriedThing(c, ThingPlaceMode.Direct, out _);
                }

                // Ensure victim is on map (TryAcceptPawn handles despawn safely)
                if (victim.Spawned == false)
                {
                    // If TryDropCarriedThing didn’t spawn it for some reason, spawn it briefly
                    GenSpawn.Spawn(victim, c, map, WipeMode.Vanish);
                }

                bool accepted = cocoonThing.TryAcceptPawn(victim);

                var comp = pawn.TryGetComp<CompArachnoidHome>();
                int now = Find.TickManager.TicksGame;
                if (accepted)
                {
                    // Successful hunt resolution: apply full cooldown.
                    comp?.EndHunt(success: true, now: now);
                }
                else
                {
                    // If we fail, at least leave them on the ground and apply short cooldown.
                    if (!victim.Spawned)
                        GenSpawn.Spawn(victim, c, map, WipeMode.Vanish);

                    comp?.EndHunt(success: false, now: now);
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}