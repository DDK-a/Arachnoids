using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Arachnoids
{
    public partial class CompArachnoidHome
    {
        // Adds dev-only gizmos to the pawn when devmode + godmode are enabled.
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            if (!Prefs.DevMode) yield break;
            if (!DebugSettings.godMode) yield break;

            // Only show on the spider pawn itself
            if (parent is not Pawn pawn) yield break;
            if (!HasHome) yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: Reset hunt cooldown",
                defaultDesc = "Sets NextHuntTick to now so the spider can start a hunt immediately.",
                action = () =>
                {
                    NextHuntTick = Find.TickManager.TicksGame;
                    Messages.Message("Arachnoid hunt cooldown reset.", MessageTypeDefOf.TaskCompletion, false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Force Defend mode",
                defaultDesc = "Sets mode to Defend and clears victim.",
                action = () =>
                {
                    Mode = ArachnoidMode.Defend;
                    ClearVictim();
                    Messages.Message("Arachnoid mode set to Defend.", MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }
    }
}