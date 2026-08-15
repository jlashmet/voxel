using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Generator-facing snapshot of the authored world hierarchy. Unlike WorldHierarchyBlueprint,
    /// these plans group nested ownership and access requirements so downstream generation does not
    /// need to re-scan authoring objects or recover semantics from dependency node names.
    /// </summary>
    public sealed class WorldHierarchyPlan
    {
        public static readonly WorldHierarchyPlan Empty = new WorldHierarchyPlan(null, null, null, null, null);

        public IReadOnlyList<WorldRegionPlan> Regions { get; }
        public IReadOnlyList<WorldRoutePlan> Routes { get; }
        public IReadOnlyList<WorldSettlementPlan> Settlements { get; }
        public IReadOnlyList<WorldRouteAccessPlan> RouteAccess { get; }
        public IReadOnlyList<WorldSitePlacementPlan> SitePlacements { get; }

        public WorldHierarchyPlan(
            WorldRegionPlan[] regions,
            WorldRoutePlan[] routes,
            WorldSettlementPlan[] settlements,
            WorldRouteAccessPlan[] routeAccess,
            WorldSitePlacementPlan[] sitePlacements)
        {
            Regions = regions ?? Array.Empty<WorldRegionPlan>();
            Routes = routes ?? Array.Empty<WorldRoutePlan>();
            Settlements = settlements ?? Array.Empty<WorldSettlementPlan>();
            RouteAccess = routeAccess ?? Array.Empty<WorldRouteAccessPlan>();
            SitePlacements = sitePlacements ?? Array.Empty<WorldSitePlacementPlan>();
        }
    }

    public sealed class WorldRegionPlan
    {
        public RegionRef Region { get; }
        public BiomeFamily Biome { get; }
        public IReadOnlyList<RouteRef> Routes { get; }
        public IReadOnlyList<SettlementRef> Settlements { get; }
        public IReadOnlyList<SiteRef> RegionOwnedSites { get; }

        public WorldRegionPlan(
            RegionRef region,
            BiomeFamily biome,
            RouteRef[] routes,
            SettlementRef[] settlements,
            SiteRef[] regionOwnedSites)
        {
            Region = region;
            Biome = biome;
            Routes = routes ?? Array.Empty<RouteRef>();
            Settlements = settlements ?? Array.Empty<SettlementRef>();
            RegionOwnedSites = regionOwnedSites ?? Array.Empty<SiteRef>();
        }
    }

    public sealed class WorldRoutePlan
    {
        public RouteRef Route { get; }
        public RegionRef Region { get; }
        public RouteKind Kind { get; }
        public RouteImportance Importance { get; }
        public IReadOnlyList<WorldRouteAccessPlan> SettlementAccess { get; }

        public WorldRoutePlan(
            RouteRef route,
            RegionRef region,
            RouteKind kind,
            RouteImportance importance,
            WorldRouteAccessPlan[] settlementAccess)
        {
            Route = route;
            Region = region;
            Kind = kind;
            Importance = importance;
            SettlementAccess = settlementAccess ?? Array.Empty<WorldRouteAccessPlan>();
        }
    }

    public sealed class WorldSettlementPlan
    {
        public SettlementRef Settlement { get; }
        public RegionRef Region { get; }
        public SettlementArchetype Archetype { get; }
        public PopulationRange Population { get; }
        public bool HasPopulationRange { get; }
        public IReadOnlyList<WorldRouteAccessPlan> RouteAccess { get; }
        public IReadOnlyList<SiteRef> Sites { get; }

        public WorldSettlementPlan(
            SettlementRef settlement,
            RegionRef region,
            SettlementArchetype archetype,
            PopulationRange population,
            bool hasPopulationRange,
            WorldRouteAccessPlan[] routeAccess,
            SiteRef[] sites)
        {
            Settlement = settlement;
            Region = region;
            Archetype = archetype;
            Population = population;
            HasPopulationRange = hasPopulationRange;
            RouteAccess = routeAccess ?? Array.Empty<WorldRouteAccessPlan>();
            Sites = sites ?? Array.Empty<SiteRef>();
        }
    }

    /// <summary>
    /// Hard physical-access requirement after authoring has been compiled. The settlement's primary
    /// public access must connect to Route, with the connector path length inside ConnectorLengthMetres.
    /// </summary>
    public sealed class WorldRouteAccessPlan
    {
        public SettlementRef Settlement { get; }
        public RouteRef Route { get; }
        public DistanceRangeMetres ConnectorLengthMetres { get; }

        public WorldRouteAccessPlan(
            SettlementRef settlement,
            RouteRef route,
            DistanceRangeMetres connectorLengthMetres)
        {
            Settlement = settlement;
            Route = route;
            ConnectorLengthMetres = connectorLengthMetres;
        }
    }

    /// <summary>Compiled ownership of one site role by either a region planner or settlement planner.</summary>
    public sealed class WorldSitePlacementPlan
    {
        public SiteRef Site { get; }
        public SitePlacementKind Kind { get; }
        public RegionRef Region { get; }
        public SettlementRef Settlement { get; }

        public WorldSitePlacementPlan(SiteRef site, RegionRef region)
        {
            Site = site;
            Kind = SitePlacementKind.Region;
            Region = region;
            Settlement = default;
        }

        public WorldSitePlacementPlan(SiteRef site, SettlementRef settlement)
        {
            Site = site;
            Kind = SitePlacementKind.Settlement;
            Region = default;
            Settlement = settlement;
        }
    }
}
