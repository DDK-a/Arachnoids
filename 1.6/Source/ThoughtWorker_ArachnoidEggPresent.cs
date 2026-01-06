using RimWorld;
using Verse;

namespace Arachnoids
{
    public class ThoughtWorker_ArachnoidEggPresent : ThoughtWorker
    {
        private static HediffDef _eggDef;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead) return ThoughtState.Inactive;
            if (p.needs?.mood == null) return ThoughtState.Inactive;
            if (!p.RaceProps.Humanlike) return ThoughtState.Inactive;

            if (_eggDef == null)
                _eggDef = DefDatabase<HediffDef>.GetNamedSilentFail("RJW_ArachnoidEgg");

            if (_eggDef == null) return ThoughtState.Inactive;
            if (p.health?.hediffSet == null) return ThoughtState.Inactive;

            return p.health.hediffSet.HasHediff(_eggDef) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}