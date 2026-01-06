using System;
using HarmonyLib;
using RimWorld;
using Verse;
using rjw; // patch_apparel

namespace Arachnoids
{
    [HarmonyPatch(typeof(PawnRenderTree), "InitializeAncestors")]
    public static class WebOverlayPatch
    {
        // Cache the HediffDef lookup so we don't hit DefDatabase constantly
        private static HediffDef _webbedDef;

        [HarmonyPostfix]
        public static void Postfix(PawnRenderTree __instance)
        {
            if (__instance?.pawn == null) return;

            // --- RJW_Cocoon-style guard: ONLY do anything if pawn is webbed ---
            _webbedDef ??= DefDatabase<HediffDef>.GetNamedSilentFail("Webbed");
            if (_webbedDef == null) return;

            var hs = __instance.pawn.health?.hediffSet;
            if (hs == null || !hs.HasHediff(_webbedDef)) return;

            // Optional extra safety: only humanlikes (prevents weirdness on animals)
            if (!__instance.pawn.RaceProps.Humanlike) return;

            // Use reflection to get nodesByTag so we work across RW versions/builds
            var nodesByTagObj = AccessTools.Field(typeof(PawnRenderTree), "nodesByTag")?.GetValue(__instance);
            if (nodesByTagObj == null) return;

            // TryGetValue via reflection
            var dictType = nodesByTagObj.GetType(); // Dictionary<PawnRenderNodeTagDef, PawnRenderNode>
            var tryGetValueMI = dictType.GetMethod(
                "TryGetValue",
                new[] { typeof(PawnRenderNodeTagDef), typeof(PawnRenderNode).MakeByRefType() }
            );
            if (tryGetValueMI == null) return;

            // Head
            object[] headArgs = { PawnRenderNodeTagDefOf.Head, null };
            bool gotHead = (bool)tryGetValueMI.Invoke(nodesByTagObj, headArgs);
            if (gotHead && headArgs[1] is PawnRenderNode headNode && headNode != null)
            {
                patch_apparel.attachSubWorkerToNodeAndChildren(headNode);
            }

            // Body
            object[] bodyArgs = { PawnRenderNodeTagDefOf.Body, null };
            bool gotBody = (bool)tryGetValueMI.Invoke(nodesByTagObj, bodyArgs);
            if (gotBody && bodyArgs[1] is PawnRenderNode bodyNode && bodyNode != null)
            {
                patch_apparel.attachSubWorkerToNodeAndChildren(bodyNode);
            }
        }
    }
}