using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class JobDriver_ArachnoidRescueFromCocoon : JobDriver
    {
        private Building_ArachnoidCocoon Cocoon => job.targetA.Thing as Building_ArachnoidCocoon;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Cocoon == null || Cocoon.DestroyedOrNull());
            this.FailOn(() => !Cocoon.HasPawn);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 10 seconds = 600 ticks
            Toil wait = Toils_General.Wait(600);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return wait;

            yield return new Toil
            {
                initAction = () =>
                {
                    var cocoon = Cocoon;
                    if (cocoon == null || !cocoon.HasPawn)
                        return;

                    Pawn rescued = cocoon.ContainedPawn;
                    cocoon.ReleasePawn();

                    // Thoughts: trauma on the victim
                    if (rescued?.needs?.mood != null)
                    {
                        ThoughtDef trauma = DefDatabase<ThoughtDef>.GetNamedSilentFail("Arachnoid_CocoonTrauma");
                        if (trauma != null)
                            rescued.needs.mood.thoughts.memories.TryGainMemory(trauma);
                    }

                    // Social opinion: victim likes rescuer
                    if (rescued?.needs?.mood != null && pawn != null)
                    {
                        ThoughtDef rescuedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Arachnoid_RescuedFromCocoon");
                        if (rescuedThought != null)
                            rescued.needs.mood.thoughts.memories.TryGainMemory(rescuedThought, pawn);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}