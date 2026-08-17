using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Foundation treatment beneath a structure footprint. These values describe authoring intent;
    /// reusable foundation emitters decide how to realise the treatment with bounded primitives.
    /// </summary>
    public enum StructureFoundationStyle : byte
    {
        /// <summary>No foundation geometry is requested.</summary>
        None = 0,

        /// <summary>A level slab of fixed thickness beneath the resolved base plane.</summary>
        Slab = 1,

        /// <summary>Fill downward from the level base plane until deterministic terrain support.</summary>
        TerrainFill = 2,

        /// <summary>Allow bounded stepped foundation levels over sloped terrain.</summary>
        Terraced = 3,
    }

    /// <summary>
    /// One rectangle in definition-local X/Z coordinates. Rectangles are half-open so adjacent
    /// parts compose without double-counting their shared edge.
    /// </summary>
    public struct StructureFootprintRect
    {
        public int2 Min;
        public int2 Size;

        public int2 MaxExclusive => Min + Size;
        public bool IsValid => Size.x > 0 && Size.y > 0;

        public StructureFootprintRect(int2 min, int2 size)
        {
            Min = min;
            Size = size;
        }

        public bool Contains(int2 localPosition)
        {
            int2 max = MaxExclusive;
            return localPosition.x >= Min.x && localPosition.x < max.x
                && localPosition.y >= Min.y && localPosition.y < max.y;
        }
    }

    /// <summary>
    /// Reusable footprint and foundation configuration shared by structure archetypes.
    ///
    /// The primary rectangle is the simple/common path. Additional rectangles are a bounded,
    /// blittable composition extension point for L/T/courtyard wings without requiring a second
    /// footprint representation or managed collections. Later footprint emitters can consume all
    /// parts uniformly while simple houses and sheds pay only for one rectangle.
    /// </summary>
    public struct StructureFootprintConfig
    {
        /// <summary>Main definition-local footprint rectangle.</summary>
        public StructureFootprintRect Primary;

        /// <summary>
        /// Optional additional rectangles composing the footprint. Overlap is allowed deliberately:
        /// union semantics let authors build wings and intersections without splitting rectangles.
        /// Fixed storage keeps the config bounded and Burst-compatible.
        /// </summary>
        public FixedList128Bytes<StructureFootprintRect> AdditionalRects;

        public BasePlaneRule BasePlane;
        public StructureFoundationStyle FoundationStyle;

        /// <summary>Fixed slab depth, or minimum support depth for terrain-filled foundations.</summary>
        public int FoundationDepth;

        /// <summary>
        /// Maximum vertical change between neighbouring terraces. Used only by Terraced foundations.
        /// A non-positive value is invalid for that style.
        /// </summary>
        public int MaxTerraceStep;

        /// <summary>Semantic material role resolved through StructureMaterialPalette.</summary>
        public StructureMaterialRole FoundationMaterial;

        public int PartCount => 1 + AdditionalRects.Length;
        public bool IsComposed => AdditionalRects.Length > 0;

        public StructureFootprintRect PartAt(int index)
        {
            if (index == 0) return Primary;
            return AdditionalRects[index - 1];
        }

        /// <summary>
        /// Cheap structural validity used before detailed catalogue validation. Dimension policy
        /// remains centralized in StructureConfigValidation; this only checks footprint invariants.
        /// </summary>
        public bool IsWellFormed
        {
            get
            {
                if (!Primary.IsValid) return false;
                if (FoundationStyle != StructureFoundationStyle.None && FoundationDepth <= 0)
                    return false;
                if (FoundationStyle == StructureFoundationStyle.Terraced && MaxTerraceStep <= 0)
                    return false;

                for (var i = 0; i < AdditionalRects.Length; i++)
                {
                    if (!AdditionalRects[i].IsValid) return false;
                }

                return true;
            }
        }
    }
}
