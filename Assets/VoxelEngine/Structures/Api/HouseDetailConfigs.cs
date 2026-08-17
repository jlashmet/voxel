using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum HouseFacade : byte
    {
        Front = 0,
        Rear = 1,
        Left = 2,
        Right = 3,
    }

    public enum HouseFacadePlacementMode : byte
    {
        Centered = 0,
        EvenlySpaced = 1,
        ExplicitOffsets = 2,
    }

    /// <summary>
    /// Door layout for one facade. Door dimensions/frame/lintel semantics stay in
    /// <see cref="OpeningConfig"/>; this layer owns facade count/placement and one bounded
    /// porch/step treatment associated with that entry.
    /// </summary>
    public struct HouseDoorLayoutConfig
    {
        public HouseFacade Facade;
        public HouseFacadePlacementMode Placement;
        public int Count;
        public OpeningConfig Opening;
        public FixedList128Bytes<int> ExplicitOffsets;
        public HouseEntryTreatmentConfig EntryTreatment;

        public bool IsWellFormed
        {
            get
            {
                if (Count < 0 || !EntryTreatment.IsWellFormed)
                    return false;

                if (Placement == HouseFacadePlacementMode.ExplicitOffsets)
                {
                    if (ExplicitOffsets.Length != Count)
                        return false;
                    for (var i = 0; i < ExplicitOffsets.Length; i++)
                    {
                        if (ExplicitOffsets[i] < 0)
                            return false;
                    }
                }

                if (Count == 0)
                    return true;

                return Opening.Kind == StructureOpeningKind.Door && Opening.IsWellFormed;
            }
        }
    }

    /// <summary>
    /// Window layout for one facade. The shared opening bottom offset is the sill height and its
    /// height determines the head; spacing, frames, and deterministic size variation remain in
    /// <see cref="OpeningConfig"/> instead of being duplicated by the house layer.
    /// </summary>
    public struct HouseWindowLayoutConfig
    {
        public HouseFacade Facade;
        public HouseFacadePlacementMode Placement;
        public int Count;
        public OpeningConfig Opening;
        public FixedList128Bytes<int> ExplicitOffsets;
        public bool ShuttersEnabled;
        public int ShutterThickness;
        public StructureMaterialRole ShutterMaterialRole;

        public int SillHeight => Opening.BottomOffset;
        public int HeadHeight => Opening.BottomOffset + Opening.Height;

        public bool IsWellFormed
        {
            get
            {
                if (Count < 0 || ShutterThickness < 0 || (ShuttersEnabled && ShutterThickness == 0))
                    return false;

                if (Placement == HouseFacadePlacementMode.ExplicitOffsets)
                {
                    if (ExplicitOffsets.Length != Count)
                        return false;
                    for (var i = 0; i < ExplicitOffsets.Length; i++)
                    {
                        if (ExplicitOffsets[i] < 0)
                            return false;
                    }
                }

                if (Count == 0)
                    return true;

                return Opening.Kind == StructureOpeningKind.Window && Opening.IsWellFormed;
            }
        }
    }

    /// <summary>Configurable chimney plus optional link to an authored interior/fireplace volume.</summary>
    public struct HouseChimneyConfig
    {
        public bool Enabled;
        public int2 LocalPosition;
        public VerticalAccentConfig Geometry;
        public int FireplaceInteriorVolumeIndex;

        public bool HasFireplaceHook => Enabled && FireplaceInteriorVolumeIndex >= 0;
        public bool IsWellFormed => !Enabled ||
            (Geometry.Style == StructureVerticalAccentStyle.Chimney && Geometry.IsWellFormed &&
             FireplaceInteriorVolumeIndex >= -1);
    }

    public enum HouseExteriorFeatureKind : byte
    {
        Porch = 0,
        Awning = 1,
        Balcony = 2,
    }

    /// <summary>Shared bounded hook for porch, awning, and balcony additions on a facade.</summary>
    public struct HouseExteriorFeatureConfig
    {
        public bool Enabled;
        public HouseExteriorFeatureKind Kind;
        public HouseFacade Facade;
        public int HorizontalOffset;
        public int BottomOffset;
        public int Width;
        public int Depth;
        public int Thickness;
        public RoofConfig CoverRoof;
        public StructureMaterialRole MaterialRole;

        public bool IsWellFormed => !Enabled ||
            (Width > 0 && Depth > 0 && Thickness > 0 && BottomOffset >= 0 &&
             (Kind != HouseExteriorFeatureKind.Awning || CoverRoof.IsWellFormed));
    }
}
