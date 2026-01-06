using UnityEngine;
using Verse;

namespace Arachnoids
{
    public class ArachnoidsMod : Mod
    {
        public static ArachnoidsSettings Settings;

        public ArachnoidsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ArachnoidsSettings>();
            Settings?.Clamp();
        }

        public override string SettingsCategory() => "Arachnoids";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings ??= GetSettings<ArachnoidsSettings>();
            Settings.Clamp();

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Hunt engagement range multiplier");
            listing.Label($"Current: {Settings.huntRangeMultiplier:0.00}x");
            Settings.huntRangeMultiplier = listing.Slider(Settings.huntRangeMultiplier, 0.5f, 2f);
            listing.Gap(10f);

            listing.Label("Days until cocooning consequences begin (arachnophobia + egging)");
            listing.Label($"Current: {Settings.cocoonNegativeEffectsDays:0.00} days ({Settings.cocoonNegativeEffectsDays * 24f:0}h)");
            Settings.cocoonNegativeEffectsDays = listing.Slider(Settings.cocoonNegativeEffectsDays, 0.5f, 2f);
            listing.Gap(12f);

            // Informational (fixed now)
            listing.Label("Egg insertion rate (fixed)");
            listing.Label("Current: 4 eggs per day (every 6 hours)");
            listing.Gap(12f);

            if (listing.ButtonText("Reset to defaults"))
            {
                Settings.huntRangeMultiplier = 1f;
                Settings.cocoonNegativeEffectsDays = 1f;
            }

            listing.End();

            Settings.Clamp();
            Settings.Write();
        }
    }
}