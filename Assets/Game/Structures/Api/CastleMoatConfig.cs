using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Optional bounded rectangular moat/ditch around a castle curtain. The carve is deliberately
    /// castle-owned terrain policy: it uses a fixed local bounding ring and never scans or edits
    /// outside the configured outer half-extents.
    /// </summary>
    public struct CastleMoatConfig
    {
        public bool Enabled;

        /// <summary>Half-extents of the protected dry interior measured from the castle centre.</summary>
        public int2 InnerHalfExtents;

        /// <summary>Horizontal moat width added outside <see cref="InnerHalfExtents"/>.</summary>
        public int Width;

        /// <summary>Vertical depth below the castle plateau top.</summary>
        public int Depth;

        /// <summary>Optional water depth measured upward from the carved moat bed. Zero is dry.</summary>
        public int WaterDepth;

        /// <summary>Semantic material placed at the moat bed before optional water fill.</summary>
        public StructureMaterialRole BedMaterialRole;

        public int2 OuterHalfExtents => InnerHalfExtents + new int2(Width, Width);

        public bool IsWellFormed
        {
            get
            {
                if (!Enabled) return true;
                if (InnerHalfExtents.x <= 0 || InnerHalfExtents.y <= 0 || Width <= 0 || Depth <= 0)
                    return false;
                if (WaterDepth < 0 || WaterDepth > Depth)
                    return false;

                // Keep all extent arithmetic within signed-int range before the runtime expands
                // local coordinates into world positions.
                return (long)InnerHalfExtents.x + Width <= int.MaxValue &&
                       (long)InnerHalfExtents.y + Width <= int.MaxValue;
            }
        }
    }

    public static class CastleMoatPresets
    {
        /// <summary>
        /// The existing castle has no authored moat; the lower river remains unchanged. This preset
        /// supplies useful bounded dimensions so callers can enable the moat with one override.
        /// </summary>
        public static CastleMoatConfig Compatibility(in CastlePlan plan) => new CastleMoatConfig
        {
            Enabled = false,
            InnerHalfExtents = new int2(
                plan.BaileyHalfX + plan.WallThickness + 12,
                plan.BaileyHalfZ + plan.WallThickness + 12),
            Width = 34,
            Depth = 28,
            WaterDepth = 12,
            BedMaterialRole = StructureMaterialRole.Underground,
        };
    }
}
