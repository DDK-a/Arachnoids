using RimWorld;
using Verse;

namespace Arachnoids
{
    public class ThoughtWorker_ArachnoidCocooned : ThoughtWorker
    {
        private static HediffDef _cocoonedDef;
        private static TraitDef _arachnophobeTrait;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead) return ThoughtState.Inactive;
            if (p.needs?.mood == null) return ThoughtState.Inactive;

            if (_cocoonedDef == null)
                _cocoonedDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arachnoid_Cocooned");

            if (_cocoonedDef == null) return ThoughtState.Inactive;
            if (p.health?.hediffSet == null) return ThoughtState.Inactive;

            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(_cocoonedDef);
            if (h == null) return ThoughtState.Inactive;

            // Stage 2 once fully cocooned (severity reaches 1.0)
            if (h.Severity >= 1f)
                return ThoughtState.ActiveAtStage(2);

            // Otherwise, differentiate first-time vs repeat using arachnophobe trait.
            if (_arachnophobeTrait == null)
                _arachnophobeTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Arachnoid_Arachnophobe");

            bool hasArachnophobe =
                (_arachnophobeTrait != null) &&
                (p.story?.traits != null) &&
                p.story.traits.HasTrait(_arachnophobeTrait);

            return hasArachnophobe ? ThoughtState.ActiveAtStage(1) : ThoughtState.ActiveAtStage(0);
        }
    }
}