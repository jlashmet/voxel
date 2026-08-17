using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>A rectangular piece of a composed structure footprint, in local X/Z voxels.</summary>
    public struct RectangularFootprintConfig
    {
        public int OffsetX;
        public int OffsetZ;
        public int Width;
        public int Depth;
    }

    /// <summary>
    /// Reusable footprint/foundation configuration. The primary rectangle covers simple buildings;
    /// optional rectangles provide a bounded extension point for L/T/cross plans without requiring
    /// a new footprint representation for each archetype.
    /// </summary>
    public struct FootprintFoundationConfig
    {
        public RectangularFootprintConfig Primary;
        public FixedList128Bytes<RectangularFootprintConfig> Extensions;
        public bool FoundationEnabled;
        public int FoundationDepth;
        public int TerrainSkirtDepth;
        public int MaxTerrainAdjustment;
        public StructureMaterialRole MaterialRole;
    }

    /// <summary>How adjoining wall runs own their corner volume.</summary>
    public enum WallCornerMode : byte
    {
        Continuous = 0,
        FirstRunOwnsCorner = 1,
        SecondRunOwnsCorner = 2,
        LeaveCornerOpen = 3,
    }

    /// <summary>
    /// Archetype-neutral wall-run configuration. Repetition spacing is the component cadence used
    /// by windows, buttresses, crenellations, or other regular facade details layered on the run.
    /// </summary>
    public struct WallRunConfig
    {
        public int Thickness;
        public int Height;
        public int BaseOffset;
        public int MaterialBandHeight;
        public int RepetitionSpacing;
        public WallCornerMode CornerMode;
        public StructureMaterialRole PrimaryMaterialRole;
        public StructureMaterialRole SecondaryMaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    /// <summary>Reusable repeated-level/floor-slab configuration.</summary>
    public struct FloorLevelConfig
    {
        public int FloorCount;
        public int LevelHeight;
        public int SlabThickness;
        public int MinimumLevelHeightDelta;
        public int MaximumLevelHeightDelta;
        public StructureMaterialRole SlabMaterialRole;
    }

    /// <summary>Shared architectural opening families.</summary>
    public enum StructureOpeningKind : byte
    {
        Door = 0,
        Window = 1,
        Arch = 2,
        Niche = 3,
    }

    /// <summary>
    /// Reusable opening configuration. Width/height variation is resolved from semantic child seeds
    /// by the authoring component; spacing and margins remain integer voxels.
    /// </summary>
    public struct OpeningConfig
    {
        public StructureOpeningKind Kind;
        public int Width;
        public int Height;
        public int BottomOffset;
        public int Spacing;
        public int StartMargin;
        public int EndMargin;
        public int FrameThickness;
        public int LintelThickness;
        public int WidthVariation;
        public int HeightVariation;
        public StructureMaterialRole FrameMaterialRole;
        public StructureMaterialRole FillMaterialRole;
    }

    /// <summary>Roof families expressible using the current bounded integer primitive set.</summary>
    public enum RoofStyle : byte
    {
        Flat = 0,
        Shed = 1,
        Gable = 2,
        Hip = 3,
    }

    /// <summary>Local axis followed by a roof ridge or shed slope.</summary>
    public enum RoofAxis : byte
    {
        X = 0,
        Z = 1,
    }

    /// <summary>
    /// Reusable integer roof configuration. Pitch is rise/run rather than an angle so authoritative
    /// generation never requires floating-point trigonometry. Flat roofs ignore pitch and ridge.
    /// </summary>
    public struct RoofConfig
    {
        public RoofStyle Style;
        public RoofAxis RidgeAxis;
        public int PitchRise;
        public int PitchRun;
        public int EaveOverhang;
        public int Thickness;
        public int ParapetHeight;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    public enum StairRampStyle : byte
    {
        StraightStairs = 0,
        Ramp = 1,
        LandingTurn = 2,
    }

    /// <summary>Reusable straight stair, ramp, and landing-turn configuration.</summary>
    public struct StairRampConfig
    {
        public StairRampStyle Style;
        public RoofAxis Axis;
        public int Width;
        public int StepCount;
        public int Rise;
        public int Run;
        public int LandingLength;
        public StructureMaterialRole MaterialRole;
    }

    public enum TowerShape : byte
    {
        Square = 0,
        Round = 1,
    }

    public enum TowerPlacementMode : byte
    {
        Explicit = 0,
        Corners = 1,
        EvenlySpaced = 2,
    }

    public enum TowerTopStyle : byte
    {
        Flat = 0,
        Parapet = 1,
        Roof = 2,
        Spire = 3,
    }

    /// <summary>Reusable tower/turret dimensions, placement, top treatment, and opening policy.</summary>
    public struct TowerConfig
    {
        public TowerShape Shape;
        public TowerPlacementMode PlacementMode;
        public TowerTopStyle TopStyle;
        public int Width;
        public int Depth;
        public int Radius;
        public int Height;
        public int Count;
        public int Spacing;
        public bool OpeningsEnabled;
        public OpeningConfig Opening;
        public RoofConfig Roof;
        public StructureMaterialRole WallMaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    public enum ColumnShape : byte
    {
        Square = 0,
        Round = 1,
    }

    /// <summary>Reusable column plus repetition settings for colonnades.</summary>
    public struct ColumnConfig
    {
        public ColumnShape Shape;
        public int Width;
        public int Radius;
        public int Height;
        public int BaseHeight;
        public int CapitalHeight;
        public int Count;
        public int Spacing;
        public StructureMaterialRole ShaftMaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    /// <summary>
    /// Reusable buttress configuration. FlyingSpan/connection fields are an explicit bounded hook
    /// for a later flying-buttress approximation rather than an archetype-specific duplicate.
    /// </summary>
    public struct ButtressConfig
    {
        public int Width;
        public int Depth;
        public int Height;
        public int Count;
        public int Spacing;
        public int Taper;
        public bool FlyingEnabled;
        public int FlyingSpan;
        public int FlyingRise;
        public int FlyingConnectionHeight;
        public StructureMaterialRole MaterialRole;
    }

    /// <summary>Reusable parapet/battlement/crenellation cadence.</summary>
    public struct BattlementConfig
    {
        public int ParapetThickness;
        public int ParapetHeight;
        public int MerlonWidth;
        public int MerlonHeight;
        public int GapWidth;
        public int CornerMerlonWidth;
        public StructureMaterialRole MaterialRole;
    }

    public enum VerticalAccentStyle : byte
    {
        Chimney = 0,
        Spire = 1,
        Pinnacle = 2,
        Vent = 3,
    }

    /// <summary>
    /// Shared vertical accent geometry for chimneys, spires, pinnacles, and similar protrusions.
    /// Archetype-only semantics such as fireplace ownership or bell behavior stay outside this type.
    /// </summary>
    public struct VerticalAccentConfig
    {
        public VerticalAccentStyle Style;
        public int Width;
        public int Depth;
        public int Height;
        public int Taper;
        public int Count;
        public int Spacing;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }
}
