using HarmonyLib;
using RimWorld;
using Verse;

namespace Arachnoids
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Arachnophobe_SpiderKilledMemory
    {
        private static TraitDef _arachnophobe;
        private static ThoughtDef _relief;

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null) return;

            // Only care about dead arachnoids
            if (!__instance.Dead) return;

            string defName = __instance.def?.defName;
            if (defName == null || !defName.StartsWith("Arachnoid_")) return;

            Map map = __instance.MapHeld;
            if (map == null || map.mapPawns == null) return;

            _arachnophobe ??= DefDatabase<TraitDef>.GetNamedSilentFail("Arachnoid_Arachnophobe");
            _relief ??= DefDatabase<ThoughtDef>.GetNamedSilentFail("Arachnoid_ArachnophobiaRelief_SpiderKilled");

            if (_arachnophobe == null || _relief == null) return;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead) continue;
                if (!p.RaceProps.Humanlike) continue;

                if (p.story?.traits == null) continue;
                if (!p.story.traits.HasTrait(_arachnophobe)) continue;

                if (p.needs?.mood?.thoughts?.memories == null) continue;
                p.needs.mood.thoughts.memories.TryGainMemory(_relief);
            }
        }
    }
}