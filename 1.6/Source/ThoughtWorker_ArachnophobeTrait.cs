using RimWorld;
using Verse;

namespace Arachnoids
{
    public class ThoughtWorker_ArachnophobeTrait : ThoughtWorker
    {
        private static TraitDef _traitDef;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead) return ThoughtState.Inactive;
            if (p.needs?.mood == null) return ThoughtState.Inactive;
            if (!p.RaceProps.Humanlike) return ThoughtState.Inactive;
            if (p.story?.traits == null) return ThoughtState.Inactive;

            if (_traitDef == null)
                _traitDef = DefDatabase<TraitDef>.GetNamedSilentFail("Arachnoid_Arachnophobe");

            if (_traitDef == null) return ThoughtState.Inactive;
            if (!p.story.traits.HasTrait(_traitDef)) return ThoughtState.Inactive;

            // If no map, treat as "no spiders here" stage.
            Map map = p.MapHeld;
            if (map == null || map.mapPawns == null) return ThoughtState.ActiveAtStage(0);

            bool spidersPresent = false;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other.Dead) continue;
                string defName = other.def?.defName;
                if (defName != null && defName.StartsWith("Arachnoid_"))
                {
                    spidersPresent = true;
                    break;
                }
            }

            return spidersPresent ? ThoughtState.ActiveAtStage(1) : ThoughtState.ActiveAtStage(0);
        }
    }
}