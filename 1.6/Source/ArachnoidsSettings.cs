using UnityEngine;
using Verse;

namespace Arachnoids
{
    public class ArachnoidsSettings : ModSettings
    {
        // 0.5x - 2x
        public float huntRangeMultiplier = 1f;

        // Days until Arachnophobia + egging start (0.5 - 2 days)
        public float cocoonNegativeEffectsDays = 1f;

        public void Clamp()
        {
            huntRangeMultiplier = Mathf.Clamp(huntRangeMultiplier, 0.5f, 2f);
            cocoonNegativeEffectsDays = Mathf.Clamp(cocoonNegativeEffectsDays, 0.5f, 2f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref huntRangeMultiplier, "huntRangeMultiplier", 1f);
            Scribe_Values.Look(ref cocoonNegativeEffectsDays, "cocoonNegativeEffectsDays", 1f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Clamp();
        }
    }
}