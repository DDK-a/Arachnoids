using HarmonyLib;
using RimWorld;
using Verse;

namespace Arachnoids
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_SpiderlingAutoHome
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (__instance == null || map == null) return;
            if (__instance.Dead) return;

            // Only for spiderlings
            if (__instance.kindDef == null || __instance.kindDef.defName != "Arachnoid_Spiderling")
                return;

            var comp = __instance.TryGetComp<CompArachnoidHome>();
            if (comp == null) return;

            // Ensure mode is always defend
            comp.Mode = ArachnoidMode.Defend;

            // If already has home (loaded from save), do nothing
            if (respawningAfterLoad && comp.HasHome) return;
            if (comp.HasHome) return;

            // Find nearest ArachnoidNest
            ThingDef nestDef = DefDatabase<ThingDef>.GetNamedSilentFail("ArachnoidNest");
            if (nestDef == null)
            {
                // Fallback: just set home to spawn cell
                comp.SetHome(__instance.Position, (comp.props as CompProperties_ArachnoidHome)?.defaultRadius ?? 20);
                return;
            }

            var nests = map.listerThings.ThingsOfDef(nestDef);
            Thing best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < nests.Count; i++)
            {
                Thing t = nests[i];
                if (t == null) continue;
                int d = __instance.Position.DistanceToSquared(t.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }

            int radius = (comp.props as CompProperties_ArachnoidHome)?.defaultRadius ?? 20;

            if (best != null)
                comp.SetHome(best.Position, radius);
            else
                comp.SetHome(__instance.Position, radius);
        }
    }
}