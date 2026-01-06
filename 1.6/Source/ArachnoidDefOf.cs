using RimWorld;
using Verse;

namespace Arachnoids
{
    [DefOf]
    public static class ArachnoidDefOf
    {
        public static HediffDef Webbed;
        //public static PawnRenderNodeDef Arachnoid_WebOverlay;

        static ArachnoidDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ArachnoidDefOf));
        }
    }
}