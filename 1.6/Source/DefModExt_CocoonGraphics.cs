using Verse;

namespace Arachnoids
{
    /// <summary>
    /// Allows a cocoon building to swap between two textures depending on contained pawn cocoon severity.
    /// </summary>
    public class DefModExt_CocoonGraphics : DefModExtension
    {
        /// <summary>
        /// Texture path used when cocooned severity is below 1.0 (early phase).
        /// Example: "Things/Building/Arachnoids/Cocoon"
        /// </summary>
        public string texPathEarly;

        /// <summary>
        /// Texture path used when cocooned severity is >= 1.0 (late phase: fully cocooned / egging begins).
        /// Example: "Things/Building/Arachnoids/Cocoon_Full"
        /// </summary>
        public string texPathLate;
    }
}