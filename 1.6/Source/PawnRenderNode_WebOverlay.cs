using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace Arachnoids
{
    public class PawnRenderNode_WebOverlay : PawnRenderNode
    {
        // Include bodyKey in the cache so one pawn’s body type can’t be reused for others
        private Dictionary<(string bodyKey, int stage, float alphaKey), Graphic> graphicCache
            = new Dictionary<(string, int, float), Graphic>();

        public PawnRenderNode_WebOverlay(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        private static string GetBodyKey(Pawn pawn)
        {
            // Default/fallback
            string bodyKey = "Female";

            var bt = pawn?.story?.bodyType;
            if (bt == null)
                return bodyKey;

            // Prefer vanilla body types first (stable and matches your folder names)
            if (bt == BodyTypeDefOf.Thin) return "Thin";
            if (bt == BodyTypeDefOf.Hulk) return "Hulk";
            if (bt == BodyTypeDefOf.Fat)  return "Fat";
            if (bt == BodyTypeDefOf.Male) return "Male";
            if (bt == BodyTypeDefOf.Female) return "Female";

            // If some modded body type is used, try its defName as a folder name
            if (!bt.defName.NullOrEmpty())
                return bt.defName;

            return bodyKey;
        }

        // RimWorld 1.6 pattern (like RJW-Cocoon): override GraphicFor
        public override Graphic GraphicFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return null;

            var web = pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamedSilentFail("Webbed"));
            if (web == null || web.Severity <= 0f) return null;

            // Pick stage by severity
            int stage =
                web.Severity <= 0.25f ? 1 :
                web.Severity <= 0.50f ? 2 :
                web.Severity <= 0.75f ? 3 : 4;

            // Calculate alpha blending based on stage
            float alpha;
            if (stage == 1 || stage == 2)
            {
                alpha = Mathf.Lerp(0.9f, 1.0f, Mathf.InverseLerp(0.01f, 0.50f, web.Severity));
            }
            else
            {
                alpha = 1.0f;
            }

            // Round alpha key to 2 decimals for cache stability
            float alphaKey = Mathf.Round(alpha * 100f) / 100f;

            string bodyKey = GetBodyKey(pawn);
            var key = (bodyKey, stage, alphaKey);
            if (graphicCache.TryGetValue(key, out Graphic cachedGraphic))
            {
                return cachedGraphic;
            }

            // 1) Try body-type specific folder first
            string path = $"Things/Effects/WebOverlay/{bodyKey}/web_{stage}";

            // Only draw if at least the south sprite exists (rotation handled by Graphic_Multi)
            if (ContentFinder<Texture2D>.Get(path + "_south", false) == null)
            {
                // 2) Fall back to legacy path (your original behavior)
                path = $"Things/Effects/WebOverlay/web_{stage}";
                if (ContentFinder<Texture2D>.Get(path + "_south", false) == null)
                    return null;
            }

            var baseGraphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Transparent);
            var graphic = baseGraphic.GetColoredVersion(baseGraphic.Shader, new Color(1f, 1f, 1f, alpha), Color.white);

            graphicCache[key] = graphic;
            return graphic;
        }
    }
}