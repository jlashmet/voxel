using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum VerticalCirculationStyle : byte
    {
        Stairs = 0,
        Ramp = 1,
    }

    /// <summary>Reusable integer stairs/ramp configuration with bounded landing cadence.</summary>
    public struct VerticalCirculationConfig
    {
        public VerticalCirculationStyle Style;
        public int Width;
        public int TotalRise;
        public int StepRise;
        public int StepRun;
        public int LandingLength;
        public int LandingEverySteps;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;
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

    /// <summary>Archetype-neutral tower/turret dimensions, placement cadence, top, and openings.</summary>
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
        public int PlacementSpacing;
        public int WallThickness;
        public int TaperPerLevel;
        public OpeningConfig Opening;
        public RoofConfig Roof;
        public StructureMaterialRole WallMaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    /// <summary>One reusable column profile expressed only in integer voxel dimensions.</summary>
    public struct ColumnConfig
    {
        public int Width;
        public int Depth;
        public int Height;
        public int BaseHeight;
        public int CapitalHeight;
        public int Taper;
        public StructureMaterialRole ShaftMaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    /// <summary>Repeated columns with explicit count/cadence and optional connecting lintel.</summary>
    public struct ColonnadeConfig
    {
        public ColumnConfig Column;
        public int Count;
        public int Spacing;
        public int StartMargin;
        public int EndMargin;
        public bool ConnectWithLintel;
        public int LintelHeight;
        public StructureMaterialRole LintelMaterialRole;
    }

    public enum ButtressStyle : byte
    {
        Solid = 0,
        FlyingApproximation = 1,
    }

    /// <summary>
    /// Repeated structural support. FlyingApproximation reserves explicit upper/lower attachment
    /// heights and clearance so a later builder can compose an arch/diagonal support deterministically.
    /// </summary>
    public struct ButtressConfig
    {
        public ButtressStyle Style;
        public int Width;
        public int Depth;
        public int Height;
        public int Spacing;
        public int StartMargin;
        public int EndMargin;
        public int LowerAttachmentHeight;
        public int UpperAttachmentHeight;
        public int FlyingClearance;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    public enum BattlementStyle : byte
    {
        SolidParapet = 0,
        Crenellated = 1,
    }

    /// <summary>Reusable parapet/battlement cadence independent of castle-specific semantics.</summary>
    public struct BattlementConfig
    {
        public BattlementStyle Style;
        public int Height;
        public int Thickness;
        public int MerlonWidth;
        public int CrenelWidth;
        public int StartMargin;
        public int EndMargin;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;
    }

    public enum VerticalAccentKind : byte
    {
        Chimney = 0,
        Spire = 1,
        Pinnacle = 2,
        Vent = 3,
    }

    /// <summary>
    /// Shared vertical projection geometry. Archetype meaning such as a church bell tower remains
    /// outside this type; this only captures the overlapping chimney/spire/pinnacle shape controls.
    /// </summary>
    public struct VerticalAccentConfig
    {
        public VerticalAccentKind Kind;
        public int Width;
        public int Depth;
        public int Height;
        public int Taper;
        public int CapHeight;
        public bool Hollow;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole CapMaterialRole;
    }

    /// <summary>One local room/interior volume to carve from otherwise solid authored geometry.</summary>
    public struct InteriorVolumeConfig
    {
        public int3 Offset;
        public int3 Size;
        public int FloorThickness;
        public int CeilingThickness;
        public StructureMaterialRole FloorMaterialRole;
        public StructureMaterialRole WallMaterialRole;
    }

    /// <summary>Explicit connective opening between room/interior volumes.</summary>
    public struct InteriorConnectionConfig
    {
        public int FromVolumeIndex;
        public int ToVolumeIndex;
        public Facing Facing;
        public int HorizontalOffset;
        public int BottomOffset;
        public int Width;
        public int Height;
        public StructureMaterialRole FrameMaterialRole;
    }

    /// <summary>Bounded room carving plus explicit connections sufficient to preserve navigation.</summary>
    public struct InteriorLayoutConfig
    {
        public FixedList512Bytes<InteriorVolumeConfig> Volumes;
        public FixedList512Bytes<InteriorConnectionConfig> Connections;
    }

    /// <summary>Reusable enclosed open-space/courtyard composition in local X/Z coordinates.</summary>
    public struct CourtyardConfig
    {
        public int OffsetX;
        public int OffsetZ;
        public int Width;
        public int Depth;
        public int PerimeterClearance;
        public bool OpenToSky;
        public bool SurfaceEnabled;
        public StructureMaterialRole SurfaceMaterialRole;
    }

    /// <summary>Stable consumer-facing meanings for structure attachment points.</summary>
    public enum StructureAttachmentKind : byte
    {
        MainEntrance = 0,
        RearEntrance = 1,
        Road = 2,
        Basement = 3,
        Crypt = 4,
        Cave = 5,
        Extension = 6,
    }

    /// <summary>Named local attachment request without exposing the producing structure's internals.</summary>
    public struct StructureAttachmentConfig
    {
        public StructureAttachmentKind Kind;
        public int3 LocalPosition;
        public Facing Facing;
    }

    public static class StructureAttachmentSemantics
    {
        public static FixedString32Bytes Name(StructureAttachmentKind kind)
        {
            switch (kind)
            {
                case StructureAttachmentKind.MainEntrance: return new FixedString32Bytes("MainEntrance");
                case StructureAttachmentKind.RearEntrance: return new FixedString32Bytes("RearEntrance");
                case StructureAttachmentKind.Road: return new FixedString32Bytes("Road");
                case StructureAttachmentKind.Basement: return new FixedString32Bytes("Basement");
                case StructureAttachmentKind.Crypt: return new FixedString32Bytes("Crypt");
                case StructureAttachmentKind.Cave: return new FixedString32Bytes("Cave");
                case StructureAttachmentKind.Extension: return new FixedString32Bytes("Extension");
                default: return default;
            }
        }
    }
}
