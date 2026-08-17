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

    /// <summary>Door layout for one facade, including optional step/porch treatment.</summary>
    public struct HouseDoorLayoutConfig
    {
        public HouseFacade Facade;
        public HouseFacadePlacementMode Placement;
        public int Count;
        public OpeningConfig Opening;
        public FixedList128Bytes<int> ExplicitOffsets;
        public bool StepsEnabled;
        public int StepDepth;
        public int StepHeight;
        public StructureMaterialRole StepMaterialRole;

        public bool IsWellFormed =>
            Count >= 0 &&
            (Count == 0 || (Opening.Kind == StructureOpeningKind.Door && Opening.Width > 0 && Opening.Height > 0)) &&
            StepDepth >= 0 && StepHeight >= 0 &&
            (Placement != HouseFacadePlacementMode.ExplicitOffsets || ExplicitOffsets.Length == Count);
    }

    /// <summary>Window layout for one facade with deterministic width/height variation in Opening.</summary>
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

        public bool IsWellFormed =>
            Count >= 0 &&
            (Count == 0 || (Opening.Kind == StructureOpeningKind.Window && Opening.Width > 0 && Opening.Height > 0)) &&
            ShutterThickness >= 0 &&
            (Placement != HouseFacadePlacementMode.ExplicitOffsets || ExplicitOffsets.Length == Count);
    }

    /// <summary>Configurable chimney plus optional link to an authored interior/fireplace volume.</summary>
    public struct HouseChimneyConfig
    {
        public bool Enabled;
        public int2 LocalPosition;
        public VerticalAccentConfig Geometry;
        public int FireplaceInteriorVolumeIndex;

        public bool HasFireplaceHook => FireplaceInteriorVolumeIndex >= 0;
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
            (Width > 0 && Depth > 0 && Thickness > 0 && BottomOffset >= 0);
    }
}
