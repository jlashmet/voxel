using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public readonly struct RegionRef : IEquatable<RegionRef>
    {
        public string Id { get; }
        public RegionRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(RegionRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RegionRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct RouteRef : IEquatable<RouteRef>
    {
        public string Id { get; }
        public RouteRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(RouteRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RouteRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct SettlementRef : IEquatable<SettlementRef>
    {
        public string Id { get; }
        public SettlementRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SettlementRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SettlementRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public enum BiomeFamily
    {
        Unspecified = 0,
        TemperateForest = 1,
        Grassland = 2,
        Wetland = 3,
        Mountain = 4,
        Desert = 5,
        Tundra = 6,
        Tropical = 7
    }

    public enum RouteKind
    {
        Road = 0,
        TradeRoad = 1,
        Trail = 2,
        Waterway = 3
    }

    public enum RouteImportance
    {
        Local = 0,
        Secondary = 1,
        Primary = 2
    }

    public enum SettlementArchetype
    {
        Unspecified = 0,
        Hamlet = 1,
        Village = 2,
        Town = 3,
        City = 4,
        FortifiedTown = 5
    }

    public readonly struct PopulationRange
    {
        public int Minimum { get; }
        public int Maximum { get; }

        public PopulationRange(int minimum, int maximum)
        {
            if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    public sealed class RegionSpec
    {
        public RegionRef Ref { get; }
        public BiomeFamily Biome { get; }

        internal RegionSpec(RegionRef @ref, BiomeFamily biome)
        {
            Ref = @ref;
            Biome = biome;
        }
    }

    public sealed class RouteSpec
    {
        public RouteRef Ref { get; }
        public RegionRef Region { get; }
        public RouteKind Kind { get; }
        public RouteImportance Importance { get; }

        internal RouteSpec(RouteRef @ref, RegionRef region, RouteKind kind, RouteImportance importance)
        {
            Ref = @ref;
            Region = region;
            Kind = kind;
            Importance = importance;
        }
    }

    public sealed class SettlementSpec
    {
        public SettlementRef Ref { get; }
        public RegionRef Region { get; }
        public SettlementArchetype Archetype { get; }
        public PopulationRange Population { get; }
        public bool HasPopulationRange { get; }

        internal SettlementSpec(
            SettlementRef @ref,
            RegionRef region,
            SettlementArchetype archetype,
            PopulationRange population,
            bool hasPopulationRange)
        {
            Ref = @ref;
            Region = region;
            Archetype = archetype;
            Population = population;
            HasPopulationRange = hasPopulationRange;
        }
    }

    /// <summary>
    /// Hard route-access constraint. The generated settlement must expose a primary public access
    /// connected to the named route, and the generated connector path from route centreline to that
    /// access must have a length inside ConnectorLengthMetres. This is intentionally stronger and
    /// more precise than a fuzzy "settlement near route" relationship.
    /// </summary>
    public sealed class SettlementRouteAccessSpec
    {
        public SettlementRef Settlement { get; }
        public RouteRef Route { get; }
        public DistanceRangeMetres ConnectorLengthMetres { get; }

        internal SettlementRouteAccessSpec(
            SettlementRef settlement,
            RouteRef route,
            DistanceRangeMetres connectorLengthMetres)
        {
            Settlement = settlement;
            Route = route;
            ConnectorLengthMetres = connectorLengthMetres;
        }
    }

    public enum SitePlacementKind
    {
        Region = 0,
        Settlement = 1
    }

    /// <summary>
    /// Declares which generated spatial owner is responsible for a site. A site in a settlement is
    /// placed by settlement/site planning; a region-owned site is placed outside that nested scope.
    /// </summary>
    public readonly struct SitePlacementSpec
    {
        public SiteRef Site { get; }
        public SitePlacementKind Kind { get; }
        public RegionRef Region { get; }
        public SettlementRef Settlement { get; }

        private SitePlacementSpec(
            SiteRef site,
            SitePlacementKind kind,
            RegionRef region,
            SettlementRef settlement)
        {
            Site = site;
            Kind = kind;
            Region = region;
            Settlement = settlement;
        }

        internal static SitePlacementSpec InRegion(SiteRef site, RegionRef region) =>
            new SitePlacementSpec(site, SitePlacementKind.Region, region, default);

        internal static SitePlacementSpec InSettlement(SiteRef site, SettlementRef settlement) =>
            new SitePlacementSpec(site, SitePlacementKind.Settlement, default, settlement);
    }

    public sealed class WorldHierarchyBlueprint
    {
        public IReadOnlyList<RegionSpec> Regions { get; }
        public IReadOnlyList<RouteSpec> Routes { get; }
        public IReadOnlyList<SettlementSpec> Settlements { get; }
        public IReadOnlyList<SettlementRouteAccessSpec> RouteAccess { get; }
        public IReadOnlyList<SitePlacementSpec> SitePlacements { get; }

        internal WorldHierarchyBlueprint(
            RegionSpec[] regions,
            RouteSpec[] routes,
            SettlementSpec[] settlements,
            SettlementRouteAccessSpec[] routeAccess,
            SitePlacementSpec[] sitePlacements)
        {
            Regions = regions ?? Array.Empty<RegionSpec>();
            Routes = routes ?? Array.Empty<RouteSpec>();
            Settlements = settlements ?? Array.Empty<SettlementSpec>();
            RouteAccess = routeAccess ?? Array.Empty<SettlementRouteAccessSpec>();
            SitePlacements = sitePlacements ?? Array.Empty<SitePlacementSpec>();
        }
    }

    public sealed partial class CampaignBuilder
    {
        internal readonly List<RegionSpec> Regions = new List<RegionSpec>();
        internal readonly List<RouteSpec> Routes = new List<RouteSpec>();
        internal readonly List<SettlementSpec> Settlements = new List<SettlementSpec>();
        internal readonly List<SettlementRouteAccessSpec> SettlementRouteAccess = new List<SettlementRouteAccessSpec>();
        internal readonly List<SitePlacementSpec> SitePlacements = new List<SitePlacementSpec>();

        internal WorldHierarchyBlueprint BuildHierarchy() => new WorldHierarchyBlueprint(
            Regions.ToArray(),
            Routes.ToArray(),
            Settlements.ToArray(),
            SettlementRouteAccess.ToArray(),
            SitePlacements.ToArray());
    }

    public sealed partial class WorldBlueprintBuilder
    {
        public RegionRef RequireRegion(string id, Action<RegionBuilder> configure)
        {
            var regionRef = new RegionRef(id);
            var builder = new RegionBuilder(regionRef);
            configure?.Invoke(builder);
            _campaign.Regions.Add(builder.Build());
            return regionRef;
        }

        public RouteRef RequireRoute(string id, Action<RouteBuilder> configure)
        {
            var routeRef = new RouteRef(id);
            var builder = new RouteBuilder(routeRef);
            configure?.Invoke(builder);
            _campaign.Routes.Add(builder.Build());
            return routeRef;
        }

        public SettlementRef RequireSettlement(string id, Action<SettlementBuilder> configure)
        {
            var settlementRef = new SettlementRef(id);
            var builder = new SettlementBuilder(settlementRef, _campaign.SettlementRouteAccess);
            configure?.Invoke(builder);
            _campaign.Settlements.Add(builder.Build());
            return settlementRef;
        }

        public SiteRef RequireSite(string id, RegionRef region, Action<SiteBuilder> configure)
        {
            SiteRef site = RequireSite(id, configure);
            _campaign.SitePlacements.Add(SitePlacementSpec.InRegion(site, region));
            return site;
        }

        public SiteRef RequireSite(string id, SettlementRef settlement, Action<SiteBuilder> configure)
        {
            SiteRef site = RequireSite(id, configure);
            _campaign.SitePlacements.Add(SitePlacementSpec.InSettlement(site, settlement));
            return site;
        }
    }

    public sealed class RegionBuilder
    {
        private readonly RegionRef _ref;
        private BiomeFamily _biome = BiomeFamily.Unspecified;

        internal RegionBuilder(RegionRef @ref) => _ref = @ref;

        public RegionBuilder Biome(BiomeFamily biome)
        {
            _biome = biome;
            return this;
        }

        internal RegionSpec Build() => new RegionSpec(_ref, _biome);
    }

    public sealed class RouteBuilder
    {
        private readonly RouteRef _ref;
        private RegionRef _region;
        private bool _hasRegion;
        private RouteKind _kind = RouteKind.Road;
        private RouteImportance _importance = RouteImportance.Local;

        internal RouteBuilder(RouteRef @ref) => _ref = @ref;

        public RouteBuilder InRegion(RegionRef region)
        {
            _region = region;
            _hasRegion = true;
            return this;
        }

        public RouteBuilder Kind(RouteKind kind)
        {
            _kind = kind;
            return this;
        }

        public RouteBuilder Importance(RouteImportance importance)
        {
            _importance = importance;
            return this;
        }

        internal RouteSpec Build()
        {
            if (!_hasRegion)
                throw new InvalidOperationException($"Route '{_ref}' must belong to a region.");
            return new RouteSpec(_ref, _region, _kind, _importance);
        }
    }

    public sealed class SettlementBuilder
    {
        private readonly SettlementRef _ref;
        private readonly List<SettlementRouteAccessSpec> _routeAccessSink;
        private RegionRef _region;
        private bool _hasRegion;
        private SettlementArchetype _archetype = SettlementArchetype.Unspecified;
        private PopulationRange _population;
        private bool _hasPopulationRange;

        internal SettlementBuilder(
            SettlementRef @ref,
            List<SettlementRouteAccessSpec> routeAccessSink)
        {
            _ref = @ref;
            _routeAccessSink = routeAccessSink ?? throw new ArgumentNullException(nameof(routeAccessSink));
        }

        public SettlementBuilder InRegion(RegionRef region)
        {
            _region = region;
            _hasRegion = true;
            return this;
        }

        public SettlementBuilder Archetype(SettlementArchetype archetype)
        {
            _archetype = archetype;
            return this;
        }

        public SettlementBuilder Population(int minimum, int maximum)
        {
            _population = new PopulationRange(minimum, maximum);
            _hasPopulationRange = true;
            return this;
        }

        public SettlementBuilder ConnectTo(RouteRef route, DistanceRangeMetres connectorLengthMetres)
        {
            _routeAccessSink.Add(new SettlementRouteAccessSpec(_ref, route, connectorLengthMetres));
            return this;
        }

        internal SettlementSpec Build()
        {
            if (!_hasRegion)
                throw new InvalidOperationException($"Settlement '{_ref}' must belong to a region.");
            return new SettlementSpec(_ref, _region, _archetype, _population, _hasPopulationRange);
        }
    }
}
