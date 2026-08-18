using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>Castle-owned well detail placed inside a shared open-space courtyard.</summary>
    public struct CastleCourtyardWellConfig
    {
        public bool Enabled;
        public int2 LocalCentre;
        public int OuterRadius;
        public int InnerRadius;
        public int WallHeight;
        public int ShaftDepth;
        public int WaterRadius;
        public int WaterDepth;

        public bool IsWellFormed => !Enabled ||
            (OuterRadius > 0 && InnerRadius > 0 && InnerRadius < OuterRadius &&
             WallHeight > 0 && ShaftDepth > 0 && WaterRadius > 0 &&
             WaterRadius <= InnerRadius && WaterDepth > 0 && WaterDepth <= ShaftDepth);
    }

    /// <summary>
    /// One bounded secondary-building slot in castle-local X/Z coordinates. The extension anchor is
    /// the stable semantic handoff for a later chapel, barracks, workshop, stable, or other feature;
    /// the compatibility authorer may still place its historical placeholder shell at LocalOrigin.
    /// </summary>
    public struct CastleCourtyardBuildingSlotConfig
    {
        public int2 LocalOrigin;
        public AttachmentAnchorConfig Anchor;

        public bool IsWellFormed =>
            Anchor.Kind == StructureAttachmentKind.Extension && Anchor.IsWellFormed;
    }

    /// <summary>
    /// Castle courtyard composition over the shared rectangular open-space contract. Runtime feature
    /// slots are intentionally represented as semantic anchors rather than speculative CallSlot
    /// execution; the shared runtime slot contract remains deferred until an archetype requires it.
    /// </summary>
    public struct CastleCourtyardConfig
    {
        public OpenSpaceConfig OpenSpace;

        /// <summary>0..100 chance that each paved column uses the primary surface material.</summary>
        public int PrimarySurfacePercent;

        public CastleCourtyardWellConfig Well;

        /// <summary>
        /// When true, preserve the historical three small authored outbuilding shells at configured
        /// slot origins. Disabling them leaves the courtyard open while retaining attachment anchors.
        /// </summary>
        public bool AuthorCompatibilityBuildings;

        public FixedList512Bytes<CastleCourtyardBuildingSlotConfig> SecondaryBuildingSlots;

        public bool IsWellFormed
        {
            get
            {
                if (!OpenSpace.IsWellFormed || PrimarySurfacePercent < 0 ||
                    PrimarySurfacePercent > 100 || !Well.IsWellFormed)
                    return false;

                for (int i = 0; i < SecondaryBuildingSlots.Length; i++)
                {
                    if (!SecondaryBuildingSlots[i].IsWellFormed)
                        return false;
                }

                return true;
            }
        }

        public int3 ResolveSecondaryBuildingAnchor(in CastlePlan plan, int index)
        {
            if ((uint)index >= (uint)SecondaryBuildingSlots.Length)
                throw new System.ArgumentOutOfRangeException(nameof(index));

            return plan.Centre + SecondaryBuildingSlots[index].Anchor.LocalPosition;
        }
    }

    public static class CastleCourtyardPresets
    {
        /// <summary>Preserves the current paved court, well, and three north-side outbuilding origins.</summary>
        public static CastleCourtyardConfig Compatibility(in CastlePlan plan)
        {
            var openEdge = new OpenSpaceEdgeConfig
            {
                Kind = OpenSpaceEdgeKind.Open,
                Height = 0,
                Thickness = 0,
                RepetitionSpacing = 0,
                EntranceWidth = 0,
                PrimaryMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var config = new CastleCourtyardConfig
            {
                OpenSpace = new OpenSpaceConfig
                {
                    Area = new StructureFootprintRect(
                        new int2(-plan.BaileyHalfX + 40, -plan.BaileyHalfZ + 40),
                        new int2(plan.BaileyHalfX * 2 - 80, plan.BaileyHalfZ * 2 - 80)),
                    SurfaceMode = OpenSpaceSurfaceMode.Paved,
                    SurfaceThickness = 2,
                    SurfaceMaterialRole = StructureMaterialRole.PrimaryWall,
                    North = openEdge,
                    East = openEdge,
                    South = openEdge,
                    West = openEdge,
                },
                PrimarySurfacePercent = 82,
                Well = new CastleCourtyardWellConfig
                {
                    Enabled = true,
                    LocalCentre = new int2(-plan.BaileyHalfX / 2, plan.BaileyHalfZ / 3),
                    OuterRadius = 16,
                    InnerRadius = 11,
                    WallHeight = 12,
                    ShaftDepth = 60,
                    WaterRadius = 10,
                    WaterDepth = 14,
                },
                AuthorCompatibilityBuildings = true,
            };

            int slotZ = plan.BaileyHalfZ - 130;
            for (int i = 0; i < 3; i++)
            {
                int slotX = -plan.BaileyHalfX + 60 + i * 150;
                config.SecondaryBuildingSlots.Add(new CastleCourtyardBuildingSlotConfig
                {
                    LocalOrigin = new int2(slotX, slotZ),
                    Anchor = new AttachmentAnchorConfig
                    {
                        Kind = StructureAttachmentKind.Extension,
                        LocalPosition = new int3(slotX, plan.PlateauHeight, slotZ),
                        Facing = Facing.South,
                        SnapToGround = false,
                    },
                });
            }

            return config;
        }
    }
}
