using System.Collections.Generic;
using Unity.Mathematics;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// Semantic materials understood by world generation. Backends decide how these become voxel
    /// material ids, meshes, SDF materials, or editor preview colors.
    /// </summary>
    public enum MaterialRole : byte
    {
        FoundationStone,
        Masonry,
        DarkMasonry,
        Timber,
        Glass,
        WarmWindow,
        RoofTile,
        Slate,
        Cloth,
        Moss,
        Water,
    }

    /// <summary>Reusable structural grammars available to a conventional human settlement.</summary>
    public enum StructureArchetype : byte
    {
        Townhouse,
        WideHouse,
        Shop,
        Inn,
        Warehouse,
        Mansion,
        Church,
        Well,
    }

    /// <summary>
    /// Shared architectural language for one location. Measurements are integer decimetres so the
    /// semantic plan is deterministic but remains independent of a backend's voxel resolution.
    /// </summary>
    public readonly struct ArchitectureTheme
    {
        public readonly string Id;
        public readonly MaterialRole Foundation;
        public readonly MaterialRole Wall;
        public readonly MaterialRole Frame;
        public readonly MaterialRole Window;
        public readonly MaterialRole Roof;
        public readonly MaterialRole AccentStone;
        public readonly int FoundationHeightDm;
        public readonly int WallThicknessDm;
        public readonly int FloorHeightDm;
        public readonly int DoorHeightDm;
        public readonly int WindowBaseDm;
        public readonly int WindowHeightDm;
        public readonly int BeamWidthDm;
        public readonly int RoofOverhangDm;
        public readonly int TypicalRoofHeightDm;
        public readonly int GrandRoofHeightDm;
        public readonly int UpperStoreyOverhangDm;

        public ArchitectureTheme(
            string id,
            MaterialRole foundation,
            MaterialRole wall,
            MaterialRole frame,
            MaterialRole window,
            MaterialRole roof,
            MaterialRole accentStone,
            int foundationHeightDm,
            int wallThicknessDm,
            int floorHeightDm,
            int doorHeightDm,
            int windowBaseDm,
            int windowHeightDm,
            int beamWidthDm,
            int roofOverhangDm,
            int typicalRoofHeightDm,
            int grandRoofHeightDm,
            int upperStoreyOverhangDm)
        {
            Id = id;
            Foundation = foundation;
            Wall = wall;
            Frame = frame;
            Window = window;
            Roof = roof;
            AccentStone = accentStone;
            FoundationHeightDm = foundationHeightDm;
            WallThicknessDm = wallThicknessDm;
            FloorHeightDm = floorHeightDm;
            DoorHeightDm = doorHeightDm;
            WindowBaseDm = windowBaseDm;
            WindowHeightDm = windowHeightDm;
            BeamWidthDm = beamWidthDm;
            RoofOverhangDm = roofOverhangDm;
            TypicalRoofHeightDm = typicalRoofHeightDm;
            GrandRoofHeightDm = grandRoofHeightDm;
            UpperStoreyOverhangDm = upperStoreyOverhangDm;
        }
    }

    /// <summary>One semantically identified site in a generated settlement.</summary>
    public readonly struct PlannedSite
    {
        /// <summary>Stable content-defined role id. Gameplay binds to this, never PositionDm.</summary>
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly int2 PositionDm;
        public readonly byte Orientation;

        public PlannedSite(int roleId, StructureArchetype archetype, int2 positionDm, byte orientation)
        {
            RoleId = roleId;
            Archetype = archetype;
            PositionDm = positionDm;
            Orientation = (byte)(orientation & 3);
        }
    }

    /// <summary>
    /// Renderer-independent result of settlement planning. It says what exists and where its plot
    /// is; it contains no voxels, GameObjects, material bytes, meshes, or renderer references.
    /// </summary>
    public sealed class SettlementPlan
    {
        public string Id { get; }
        public uint Seed { get; }
        public int2 CentreDm { get; }
        public ArchitectureTheme Theme { get; }
        public IReadOnlyList<PlannedSite> Sites => _sites;

        private readonly List<PlannedSite> _sites;

        public SettlementPlan(string id, uint seed, int2 centreDm,
                              ArchitectureTheme theme, List<PlannedSite> sites)
        {
            Id = id;
            Seed = seed;
            CentreDm = centreDm;
            Theme = theme;
            _sites = sites;
        }
    }
}
