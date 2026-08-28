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

    /// <summary>Semantic street hierarchy retained for settlements that intentionally author roads.</summary>
    public enum StreetKind : byte
    {
        MainRoad,
        Secondary,
        Service,
    }

    /// <summary>
    /// Quarter-turn structure orientation. The name is retained for source compatibility with the
    /// current architecture grammar; organic public access may use an independent eight-way vector.
    /// </summary>
    public enum FrontageDirection : byte
    {
        South = 0,
        West = 1,
        North = 2,
        East = 3,
    }

    /// <summary>The semantic public-space provenance of a site entrance.</summary>
    public enum SiteAccessKind : byte
    {
        None = 0,
        Street = 1,
        Plaza = 2,
        Route = 3,
    }

    /// <summary>
    /// Explicit connection from a stable site role to settlement public space. Route is a generic
    /// inferred circulation relation; Street/Plaza remain compatibility provenance for authored plans.
    /// </summary>
    public readonly struct PlannedSiteAccess
    {
        public readonly SiteAccessKind Kind;
        public readonly string TargetId;
        public readonly Int2 NetworkPointDm;

        public PlannedSiteAccess(SiteAccessKind kind, string targetId, Int2 networkPointDm)
        {
            if (kind == SiteAccessKind.None)
                throw new ArgumentException("A planned site access must target public space.", nameof(kind));
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("A planned site access target id is required.", nameof(targetId));

            Kind = kind;
            TargetId = targetId;
            NetworkPointDm = networkPointDm;
        }

        public bool IsSpecified => Kind != SiteAccessKind.None && !string.IsNullOrEmpty(TargetId);
    }

    /// <summary>
    /// Integer eight-way direction from a public entrance into its structure. Keeping this separate
    /// from quarter-turn structure orientation lets circulation approach diagonally without making
    /// architecture depend on floating point or arbitrary-angle transforms.
    /// </summary>
    public readonly struct PublicAccessDirection : IEquatable<PublicAccessDirection>
    {
        public readonly int X;
        public readonly int Z;

        public PublicAccessDirection(int x, int z)
        {
            X = Math.Sign(x);
            Z = Math.Sign(z);
            if (X == 0 && Z == 0)
                throw new ArgumentException("Public access direction cannot be zero.");
        }

        public bool Equals(PublicAccessDirection other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is PublicAccessDirection other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);

        public static PublicAccessDirection FromFrontage(FrontageDirection frontage)
        {
            switch (frontage)
            {
                case FrontageDirection.West: return new PublicAccessDirection(-1, 0);
                case FrontageDirection.North: return new PublicAccessDirection(0, -1);
                case FrontageDirection.East: return new PublicAccessDirection(1, 0);
                default: return new PublicAccessDirection(0, 1);
            }
        }
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

    /// <summary>A semantic street centreline retained as a compatibility primitive.</summary>
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

    /// <summary>
    /// Generic deterministic circulation polyline inferred from realized settlement geometry.
    /// It has no road hierarchy, cardinal-axis contract, renderer data, or pairwise site identity.
    /// </summary>
    public sealed class PlannedRoute
    {
        public string Id { get; }
        public int WidthDm { get; }
        public IReadOnlyList<Int2> Points => _points;

        private readonly List<Int2> _points;

        public PlannedRoute(string id, int widthDm, params Int2[] points)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Route id is required.", nameof(id));
            if (widthDm <= 0) throw new ArgumentOutOfRangeException(nameof(widthDm));
            if (points == null || points.Length < 2)
                throw new ArgumentException("A route requires at least two points.", nameof(points));
            Id = id;
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
    /// A stable gameplay-role plot. Frontage remains the quarter-turn structure orientation while
    /// AccessDirection records the public entrance's inward direction independently of street axes.
    /// </summary>
    public readonly struct BuildingPlot
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly Int2 PositionDm;
        public readonly FrontageDirection Frontage;
        public readonly PlannedSiteAccess Access;
        public readonly PublicAccessDirection AccessDirection;

        public BuildingPlot(int roleId, StructureArchetype archetype, DistrictKind district,
                            Int2 positionDm, FrontageDirection frontage)
            : this(roleId, archetype, district, positionDm, frontage,
                   default(PlannedSiteAccess), PublicAccessDirection.FromFrontage(frontage))
        {
        }

        public BuildingPlot(int roleId, StructureArchetype archetype, DistrictKind district,
                            Int2 positionDm, FrontageDirection frontage, PlannedSiteAccess access)
            : this(roleId, archetype, district, positionDm, frontage, access,
                   PublicAccessDirection.FromFrontage(frontage))
        {
        }

        public BuildingPlot(int roleId, StructureArchetype archetype, DistrictKind district,
                            Int2 positionDm, FrontageDirection frontage, PlannedSiteAccess access,
                            PublicAccessDirection accessDirection)
        {
            RoleId = roleId;
            Archetype = archetype;
            District = district;
            PositionDm = positionDm;
            Frontage = frontage;
            Access = access;
            AccessDirection = accessDirection;
        }
    }

    /// <summary>Backend-friendly compact semantic site view.</summary>
    public readonly struct PlannedSite
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly Int2 PositionDm;
        public readonly byte Orientation;
        public readonly PlannedSiteAccess Access;
        public readonly PublicAccessDirection AccessDirection;

        public PlannedSite(BuildingPlot plot)
        {
            RoleId = plot.RoleId;
            Archetype = plot.Archetype;
            PositionDm = plot.PositionDm;
            Orientation = (byte)plot.Frontage;
            Access = plot.Access;
            AccessDirection = plot.AccessDirection;
        }
    }

    /// <summary>
    /// Renderer-independent settlement result. Legacy Streets and generic inferred Routes can coexist
    /// during migration; new organic settlements may intentionally expose zero authored streets.
    /// </summary>
    public sealed class SettlementPlan
    {
        public string Id { get; }
        public uint Seed { get; }
        public Int2 CentreDm { get; }
        public ArchitectureTheme Theme { get; }
        public IReadOnlyList<PlannedStreet> Streets => _streets;
        public IReadOnlyList<PlannedRoute> Routes => _routes;
        public PlannedPlaza Plaza { get; }
        public IReadOnlyList<BuildingPlot> Plots => _plots;
        public IReadOnlyList<PlannedSite> Sites => _sites;

        private readonly List<PlannedStreet> _streets;
        private readonly List<PlannedRoute> _routes;
        private readonly List<BuildingPlot> _plots;
        private readonly List<PlannedSite> _sites;

        public SettlementPlan(string id, uint seed, Int2 centreDm, ArchitectureTheme theme,
                              List<PlannedStreet> streets, PlannedPlaza plaza,
                              List<BuildingPlot> plots)
            : this(id, seed, centreDm, theme, streets, new List<PlannedRoute>(), plaza, plots)
        {
        }

        public SettlementPlan(string id, uint seed, Int2 centreDm, ArchitectureTheme theme,
                              List<PlannedStreet> streets, List<PlannedRoute> routes,
                              PlannedPlaza plaza, List<BuildingPlot> plots)
        {
            Id = id;
            Seed = seed;
            CentreDm = centreDm;
            Theme = theme;
            _streets = streets ?? new List<PlannedStreet>();
            _routes = routes ?? new List<PlannedRoute>();
            Plaza = plaza;
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _sites = new List<PlannedSite>(_plots.Count);

            for (int i = 0; i < _plots.Count; i++)
                _sites.Add(new PlannedSite(_plots[i]));
        }
    }
}