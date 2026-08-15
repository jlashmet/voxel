using System;
using System.Collections.Generic;

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
        RoadSurface,
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

    /// <summary>Large-scale land-use identity used by settlement planning and later dressing passes.</summary>
    public enum DistrictKind : byte
    {
        Civic,
        Market,
        Residential,
        Working,
        Noble,
    }

    /// <summary>Semantic street hierarchy. The voxel backend decides how each becomes geometry.</summary>
    public enum StreetKind : byte
    {
        MainRoad,
        Secondary,
        Service,
    }

    /// <summary>
    /// Which side of a plot contains its public frontage. Values intentionally match the quarter
    /// turns required to rotate the current building grammar, whose authored front faces south.
    /// </summary>
    public enum FrontageDirection : byte
    {
        South = 0,
        West = 1,
        North = 2,
        East = 3,
    }

    /// <summary>
    /// The semantic movement network a site is intentionally connected to. None is retained for
    /// backwards-compatible/unfinished plans; production content should expose an explicit access.
    /// </summary>
    public enum SiteAccessKind : byte
    {
        None = 0,
        Street = 1,
        Plaza = 2,
    }

    /// <summary>
    /// Explicit connection from a stable site role to the settlement movement network. NetworkPointDm
    /// is the authored point on that street/plaza network, not a nearest-road guess made downstream.
    /// Architectural generation later connects the resolved public entrance to this point.
    /// </summary>
    public readonly struct PlannedSiteAccess
    {
        public readonly SiteAccessKind Kind;
        public readonly string TargetId;
        public readonly Int2 NetworkPointDm;

        public PlannedSiteAccess(SiteAccessKind kind, string targetId, Int2 networkPointDm)
        {
            if (kind == SiteAccessKind.None)
                throw new ArgumentException("A planned site access must target a street or plaza.", nameof(kind));
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("A planned site access target id is required.", nameof(targetId));

            Kind = kind;
            TargetId = targetId;
            NetworkPointDm = networkPointDm;
        }

        public bool IsSpecified => Kind != SiteAccessKind.None && !string.IsNullOrEmpty(TargetId);
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

    /// <summary>A semantic street centreline. The first planner slice uses orthogonal polylines.</summary>
    public sealed class PlannedStreet
    {
        public string Id { get; }
        public StreetKind Kind { get; }
        public int WidthDm { get; }
        public IReadOnlyList<Int2> Points => _points;

        private readonly List<Int2> _points;

        public PlannedStreet(string id, StreetKind kind, int widthDm, params Int2[] points)
        {
            Id = id;
            Kind = kind;
            WidthDm = widthDm;
            _points = new List<Int2>(points);
        }
    }

    /// <summary>Open civic space that later passes can furnish with stalls, signs, props, and NPCs.</summary>
    public readonly struct PlannedPlaza
    {
        public readonly string Id;
        public readonly Int2 CentreDm;
        public readonly Int2 SizeDm;

        public PlannedPlaza(string id, Int2 centreDm, Int2 sizeDm)
        {
            Id = id;
            CentreDm = centreDm;
            SizeDm = sizeDm;
        }
    }

    /// <summary>
    /// A street-facing plot allocated to a stable gameplay role. Gameplay binds to RoleId; layout
    /// can move the plot without changing quest identity. Access records the movement-network target
    /// chosen by the planner at the same time as the frontage allocation.
    /// </summary>
    public readonly struct BuildingPlot
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly Int2 PositionDm;
        public readonly FrontageDirection Frontage;
        public readonly PlannedSiteAccess Access;

        public BuildingPlot(int roleId, StructureArchetype archetype, DistrictKind district,
                            Int2 positionDm, FrontageDirection frontage)
            : this(roleId, archetype, district, positionDm, frontage, default(PlannedSiteAccess))
        {
        }

        public BuildingPlot(int roleId, StructureArchetype archetype, DistrictKind district,
                            Int2 positionDm, FrontageDirection frontage, PlannedSiteAccess access)
        {
            RoleId = roleId;
            Archetype = archetype;
            District = district;
            PositionDm = positionDm;
            Frontage = frontage;
            Access = access;
        }
    }

    /// <summary>
    /// Backend-friendly view of a plot. Kept as a separate type so gameplay can continue binding to
    /// stable role ids while adapters consume a compact position/orientation pair plus semantic access.
    /// </summary>
    public readonly struct PlannedSite
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly Int2 PositionDm;
        public readonly byte Orientation;
        public readonly PlannedSiteAccess Access;

        public PlannedSite(BuildingPlot plot)
        {
            RoleId = plot.RoleId;
            Archetype = plot.Archetype;
            PositionDm = plot.PositionDm;
            Orientation = (byte)plot.Frontage;
            Access = plot.Access;
        }
    }

    /// <summary>
    /// Renderer-independent result of settlement planning. It contains streets, civic space, and
    /// street-facing plots but no voxels, GameObjects, material bytes, meshes, or renderer references.
    /// </summary>
    public sealed class SettlementPlan
    {
        public string Id { get; }
        public uint Seed { get; }
        public Int2 CentreDm { get; }
        public ArchitectureTheme Theme { get; }
        public IReadOnlyList<PlannedStreet> Streets => _streets;
        public PlannedPlaza Plaza { get; }
        public IReadOnlyList<BuildingPlot> Plots => _plots;
        public IReadOnlyList<PlannedSite> Sites => _sites;

        private readonly List<PlannedStreet> _streets;
        private readonly List<BuildingPlot> _plots;
        private readonly List<PlannedSite> _sites;

        public SettlementPlan(string id, uint seed, Int2 centreDm, ArchitectureTheme theme,
                              List<PlannedStreet> streets, PlannedPlaza plaza,
                              List<BuildingPlot> plots)
        {
            Id = id;
            Seed = seed;
            CentreDm = centreDm;
            Theme = theme;
            _streets = streets;
            Plaza = plaza;
            _plots = plots;
            _sites = new List<PlannedSite>(plots.Count);

            for (int i = 0; i < plots.Count; i++)
                _sites.Add(new PlannedSite(plots[i]));
        }
    }
}
