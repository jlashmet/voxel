using System;
using Game.Cutscenes.Api;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Designer-facing reference to a region declared through WorldBuilder. Handles are created only
    /// by the owning campaign builder; stable *Ref values remain the compiled/runtime identity layer.
    /// </summary>
    public sealed class RegionHandle
    {
        private readonly WorldBlueprintBuilder _world;

        public RegionRef Ref { get; }
        public string Id => Ref.Id;

        internal CampaignBuilder Campaign => _world.Campaign;

        internal RegionHandle(WorldBlueprintBuilder world, RegionRef @ref)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Ref = @ref;
        }

        public RouteHandle Route(
            string id,
            RouteKind kind,
            Action<RouteAuthoringBuilder> configure = null) =>
            _world.AddRoute(this, id, kind, configure);

        public RouteHandle Road(string id, Action<RouteAuthoringBuilder> configure = null) =>
            Route(id, RouteKind.Road, configure);

        public RouteHandle TradeRoad(string id, Action<RouteAuthoringBuilder> configure = null) =>
            Route(id, RouteKind.TradeRoad, configure);

        public RouteHandle Trail(string id, Action<RouteAuthoringBuilder> configure = null) =>
            Route(id, RouteKind.Trail, configure);

        public RouteHandle Waterway(string id, Action<RouteAuthoringBuilder> configure = null) =>
            Route(id, RouteKind.Waterway, configure);

        public SettlementHandle Settlement(
            string id,
            SettlementArchetype archetype,
            Action<SettlementAuthoringBuilder> configure = null) =>
            _world.AddSettlement(this, id, archetype, configure);

        public SettlementHandle Settlement(
            string id,
            Action<SettlementAuthoringBuilder> configure = null) =>
            Settlement(id, SettlementArchetype.Unspecified, configure);

        public SettlementHandle Town(
            string id,
            Action<SettlementAuthoringBuilder> configure = null) =>
            Settlement(id, SettlementArchetype.Town, configure);

        public SiteHandle Site(
            string id,
            SiteArchetype archetype,
            Action<SiteAuthoringBuilder> configure = null) =>
            _world.AddRegionSite(this, id, archetype, configure);

        public SiteHandle Site(
            string id,
            Action<SiteAuthoringBuilder> configure = null) =>
            Site(id, SiteArchetype.Unspecified, configure);

        public static implicit operator RegionRef(RegionHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class RouteHandle
    {
        private readonly WorldBlueprintBuilder _world;

        public RouteRef Ref { get; }
        public string Id => Ref.Id;
        public RegionHandle Region { get; }

        internal CampaignBuilder Campaign => _world.Campaign;

        internal RouteHandle(WorldBlueprintBuilder world, RegionHandle region, RouteRef @ref)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Ref = @ref;
        }

        public static implicit operator RouteRef(RouteHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class SettlementHandle
    {
        private readonly WorldBlueprintBuilder _world;

        public SettlementRef Ref { get; }
        public string Id => Ref.Id;
        public RegionHandle Region { get; }

        internal CampaignBuilder Campaign => _world.Campaign;

        internal SettlementHandle(
            WorldBlueprintBuilder world,
            RegionHandle region,
            SettlementRef @ref)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Ref = @ref;
        }

        public SiteHandle Site(
            string id,
            SiteArchetype archetype,
            Action<SiteAuthoringBuilder> configure = null) =>
            _world.AddSettlementSite(this, id, archetype, configure);

        public SiteHandle Site(
            string id,
            Action<SiteAuthoringBuilder> configure = null) =>
            Site(id, SiteArchetype.Unspecified, configure);

        public SiteHandle Pub(
            string id,
            Action<SiteAuthoringBuilder> configure = null) =>
            Site(id, SiteArchetype.Pub, configure);

        public static implicit operator SettlementRef(SettlementHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class SiteHandle
    {
        private readonly WorldBlueprintBuilder _world;

        public SiteRef Ref { get; }
        public string Id => Ref.Id;

        internal CampaignBuilder Campaign => _world.Campaign;

        internal SiteHandle(WorldBlueprintBuilder world, SiteRef @ref)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Ref = @ref;
        }

        public NpcHandle Npc(string id, Action<NpcAuthoringBuilder> configure = null) =>
            _world.AddNpc(this, id, configure);

        public ObjectiveHandle Objective(
            string id,
            Action<ObjectiveAuthoringBuilder> configure)
        {
            ObjectiveRef objectiveRef = _world.Campaign.Story.Objective(
                id,
                builder =>
                {
                    builder.Target(Ref);
                    configure?.Invoke(new ObjectiveAuthoringBuilder(builder));
                });
            return new ObjectiveHandle(_world.Campaign, objectiveRef);
        }

        public CutsceneHandle Cutscene(
            CutsceneDefinition definition,
            Action<CutsceneAuthoringBuilder> configure = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            CutsceneRef cutsceneRef = _world.Campaign.Story.Cutscene(
                definition,
                builder =>
                {
                    builder.At(Ref);
                    configure?.Invoke(new CutsceneAuthoringBuilder(_world.Campaign, builder));
                });
            return new CutsceneHandle(_world.Campaign, cutsceneRef);
        }

        public static implicit operator SiteRef(SiteHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class NpcHandle
    {
        public NpcRef Ref { get; }
        public string Id => Ref.Id;
        public SiteHandle Site { get; }

        internal CampaignBuilder Campaign { get; }

        internal NpcHandle(CampaignBuilder campaign, SiteHandle site, NpcRef @ref)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Ref = @ref;
        }

        public static implicit operator NpcRef(NpcHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class ObjectiveHandle
    {
        public ObjectiveRef Ref { get; }
        public string Id => Ref.Id;

        internal CampaignBuilder Campaign { get; }

        internal ObjectiveHandle(CampaignBuilder campaign, ObjectiveRef @ref)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Ref = @ref;
        }

        public static implicit operator ObjectiveRef(ObjectiveHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    public sealed class CutsceneHandle
    {
        public CutsceneRef Ref { get; }
        public string Id => Ref.Id;

        internal CampaignBuilder Campaign { get; }

        internal CutsceneHandle(CampaignBuilder campaign, CutsceneRef @ref)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Ref = @ref;
        }

        public static implicit operator CutsceneRef(CutsceneHandle handle) =>
            handle == null ? throw new ArgumentNullException(nameof(handle)) : handle.Ref;
    }

    /// <summary>
    /// Typed player-slot target for cutscene bindings. Prefer the named slots in authored content.
    /// </summary>
    public readonly struct PlayerSlot
    {
        public int Index { get; }

        private PlayerSlot(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
        }

        public static PlayerSlot First => new PlayerSlot(0);
        public static PlayerSlot Second => new PlayerSlot(1);
        public static PlayerSlot Third => new PlayerSlot(2);
        public static PlayerSlot Fourth => new PlayerSlot(3);
        public static PlayerSlot At(int index) => new PlayerSlot(index);
    }

    public sealed class RegionAuthoringBuilder
    {
        private readonly RegionBuilder _inner;

        internal RegionAuthoringBuilder(RegionBuilder inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public RegionAuthoringBuilder Biome(BiomeFamily biome)
        {
            _inner.Biome(biome);
            return this;
        }
    }

    public sealed class RouteAuthoringBuilder
    {
        private readonly RouteBuilder _inner;

        internal RouteAuthoringBuilder(RouteBuilder inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public RouteAuthoringBuilder Importance(RouteImportance importance)
        {
            _inner.Importance(importance);
            return this;
        }
    }

    public sealed class SettlementAuthoringBuilder
    {
        private readonly CampaignBuilder _campaign;
        private readonly RegionHandle _region;
        private readonly SettlementBuilder _inner;

        internal SettlementAuthoringBuilder(
            CampaignBuilder campaign,
            RegionHandle region,
            SettlementBuilder inner)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _region = region ?? throw new ArgumentNullException(nameof(region));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public SettlementAuthoringBuilder Population(int minimum, int maximum)
        {
            _inner.Population(minimum, maximum);
            return this;
        }

        public SettlementAuthoringBuilder ConnectTo(
            RouteHandle route,
            DistanceRangeMetres connectorLengthMetres)
        {
            AuthoringHandleRules.RequireSameCampaign(_campaign, route?.Campaign, nameof(route));
            if (!route.Region.Ref.Equals(_region.Ref))
            {
                throw new InvalidOperationException(
                    $"Settlement in region '{_region.Id}' cannot connect to route '{route.Id}' " +
                    $"owned by region '{route.Region.Id}'.");
            }

            _inner.ConnectTo(route.Ref, connectorLengthMetres);
            return this;
        }
    }

    public sealed class SiteAuthoringBuilder
    {
        private readonly CampaignBuilder _campaign;
        private readonly SiteBuilder _inner;

        internal SiteAuthoringBuilder(CampaignBuilder campaign, SiteBuilder inner)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public SiteAuthoringBuilder RequireCapability(SiteCapabilityRequirement capability)
        {
            _inner.RequireCapability(capability);
            return this;
        }

        public SiteAuthoringBuilder DifferentSiteFrom(SiteHandle other)
        {
            RequireSameCampaign(other, nameof(other));
            _inner.DifferentSiteFrom(other.Ref);
            return this;
        }

        public SiteAuthoringBuilder ReachableFrom(
            SiteHandle other,
            TraversalProfile traversal)
        {
            RequireSameCampaign(other, nameof(other));
            _inner.ReachableFrom(other.Ref, traversal);
            return this;
        }

        public SiteAuthoringBuilder BoundaryDistanceFrom(
            SiteHandle other,
            DistanceRangeMetres distance)
        {
            RequireSameCampaign(other, nameof(other));
            _inner.BoundaryDistanceFrom(other.Ref, distance);
            return this;
        }

        public SiteAuthoringBuilder EntranceDistanceFrom(
            SiteHandle other,
            DistanceRangeMetres distance)
        {
            RequireSameCampaign(other, nameof(other));
            _inner.EntranceDistanceFrom(other.Ref, distance);
            return this;
        }

        public SiteAuthoringBuilder TravelDistanceFrom(
            SiteHandle other,
            TraversalProfile traversal,
            DistanceRangeMetres distance)
        {
            RequireSameCampaign(other, nameof(other));
            _inner.TravelDistanceFrom(other.Ref, traversal, distance);
            return this;
        }

        private void RequireSameCampaign(SiteHandle other, string paramName) =>
            AuthoringHandleRules.RequireSameCampaign(_campaign, other?.Campaign, paramName);
    }

    public sealed class NpcAuthoringBuilder
    {
        private readonly NpcBuilder _inner;

        internal NpcAuthoringBuilder(NpcBuilder inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public NpcAuthoringBuilder RequireConversation()
        {
            _inner.RequireConversation();
            return this;
        }
    }

    public sealed class ObjectiveAuthoringBuilder
    {
        private readonly ObjectiveBuilder _inner;

        internal ObjectiveAuthoringBuilder(ObjectiveBuilder inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public ObjectiveAuthoringBuilder CompleteWhen(IObjectiveCompletionSpec completion)
        {
            _inner.CompleteWhen(completion);
            return this;
        }
    }

    public sealed class CutsceneAuthoringBuilder
    {
        private readonly CampaignBuilder _campaign;
        private readonly CutsceneBuilder _inner;

        public CutsceneRef Ref => _inner.Ref;
        public CutsceneDefinition Definition => _inner.Definition;

        internal CutsceneAuthoringBuilder(CampaignBuilder campaign, CutsceneBuilder inner)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public CutsceneAuthoringBuilder Bind(CutsceneActorId actor, NpcHandle npc)
        {
            AuthoringHandleRules.RequireSameCampaign(_campaign, npc?.Campaign, nameof(npc));
            _inner.Bind(actor, CutsceneActorTarget.Npc(npc.Ref));
            return this;
        }

        public CutsceneAuthoringBuilder Bind(CutsceneActorId actor, PlayerSlot player)
        {
            _inner.Bind(actor, CutsceneActorTarget.Player(player.Index));
            return this;
        }
    }

    public sealed partial class WorldBlueprintBuilder
    {
        internal CampaignBuilder Campaign => _campaign;

        /// <summary>
        /// Preferred designer-facing region declaration. Relationships created from the returned handle
        /// are type-safe and ownership is fixed by nesting.
        /// </summary>
        public RegionHandle Region(
            string id,
            Action<RegionAuthoringBuilder> configure = null)
        {
            var regionRef = new RegionRef(id);
            var builder = new RegionBuilder(regionRef);
            configure?.Invoke(new RegionAuthoringBuilder(builder));
            _campaign.Regions.Add(builder.Build());
            return new RegionHandle(this, regionRef);
        }

        internal RouteHandle AddRoute(
            RegionHandle region,
            string id,
            RouteKind kind,
            Action<RouteAuthoringBuilder> configure)
        {
            RequireOwned(region, nameof(region));

            var routeRef = new RouteRef(id);
            var builder = new RouteBuilder(routeRef);
            builder.InRegion(region.Ref);
            builder.Kind(kind);
            configure?.Invoke(new RouteAuthoringBuilder(builder));
            _campaign.Routes.Add(builder.Build());
            return new RouteHandle(this, region, routeRef);
        }

        internal SettlementHandle AddSettlement(
            RegionHandle region,
            string id,
            SettlementArchetype archetype,
            Action<SettlementAuthoringBuilder> configure)
        {
            RequireOwned(region, nameof(region));

            var settlementRef = new SettlementRef(id);
            var builder = new SettlementBuilder(settlementRef, _campaign.SettlementRouteAccess);
            builder.InRegion(region.Ref);
            builder.Archetype(archetype);
            configure?.Invoke(new SettlementAuthoringBuilder(_campaign, region, builder));
            _campaign.Settlements.Add(builder.Build());
            return new SettlementHandle(this, region, settlementRef);
        }

        internal SiteHandle AddRegionSite(
            RegionHandle region,
            string id,
            SiteArchetype archetype,
            Action<SiteAuthoringBuilder> configure)
        {
            RequireOwned(region, nameof(region));
            SiteHandle site = AddSite(id, archetype, configure);
            _campaign.SitePlacements.Add(SitePlacementSpec.InRegion(site.Ref, region.Ref));
            return site;
        }

        internal SiteHandle AddSettlementSite(
            SettlementHandle settlement,
            string id,
            SiteArchetype archetype,
            Action<SiteAuthoringBuilder> configure)
        {
            RequireOwned(settlement, nameof(settlement));
            SiteHandle site = AddSite(id, archetype, configure);
            _campaign.SitePlacements.Add(SitePlacementSpec.InSettlement(site.Ref, settlement.Ref));
            return site;
        }

        internal NpcHandle AddNpc(
            SiteHandle site,
            string id,
            Action<NpcAuthoringBuilder> configure)
        {
            RequireOwned(site, nameof(site));

            var npcRef = new NpcRef(id);
            var builder = new NpcBuilder(npcRef);
            builder.PlaceAt(site.Ref);
            configure?.Invoke(new NpcAuthoringBuilder(builder));
            _campaign.Npcs.Add(builder.Build());
            return new NpcHandle(_campaign, site, npcRef);
        }

        private SiteHandle AddSite(
            string id,
            SiteArchetype archetype,
            Action<SiteAuthoringBuilder> configure)
        {
            var siteRef = new SiteRef(id);
            var builder = new SiteBuilder(siteRef, _campaign.SpatialConstraints);
            builder.Archetype(archetype);
            configure?.Invoke(new SiteAuthoringBuilder(_campaign, builder));
            _campaign.Sites.Add(builder.Build());
            return new SiteHandle(this, siteRef);
        }

        private void RequireOwned(RegionHandle handle, string paramName) =>
            AuthoringHandleRules.RequireSameCampaign(_campaign, handle?.Campaign, paramName);

        private void RequireOwned(SettlementHandle handle, string paramName) =>
            AuthoringHandleRules.RequireSameCampaign(_campaign, handle?.Campaign, paramName);

        private void RequireOwned(SiteHandle handle, string paramName) =>
            AuthoringHandleRules.RequireSameCampaign(_campaign, handle?.Campaign, paramName);
    }

    internal static class AuthoringHandleRules
    {
        public static void RequireSameCampaign(
            CampaignBuilder expected,
            CampaignBuilder actual,
            string paramName)
        {
            if (actual == null)
                throw new ArgumentNullException(paramName);
            if (!ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"Authoring handle '{paramName}' belongs to a different campaign.");
            }
        }
    }
}
