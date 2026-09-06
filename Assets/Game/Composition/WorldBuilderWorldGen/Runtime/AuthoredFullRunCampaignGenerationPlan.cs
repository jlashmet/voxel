using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Semantic-to-physical generation result for the authored full campaign. This intentionally stops
    /// at stable generated-site identities and top-down physical anchors; final terrain-relative NPC and
    /// cutscene transforms remain the responsibility of the production realization layer.
    /// </summary>
    public sealed class AuthoredFullRunCampaignGenerationPlan
    {
        private readonly AuthoredFullRunPhysicalSiteFacts _facts;

        public AuthoredFullRunPhysicalWorldPlan World { get; }
        public SiteResolutionResult Sites { get; }
        public IReadOnlyList<NpcSiteAssignment> NpcAssignments { get; }

        internal AuthoredFullRunCampaignGenerationPlan(
            AuthoredFullRunPhysicalWorldPlan world,
            AuthoredFullRunPhysicalSiteFacts facts,
            SiteResolutionResult sites,
            IReadOnlyList<NpcSiteAssignment> npcAssignments)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            _facts = facts ?? throw new ArgumentNullException(nameof(facts));
            Sites = sites ?? throw new ArgumentNullException(nameof(sites));
            NpcAssignments = npcAssignments ?? throw new ArgumentNullException(nameof(npcAssignments));
        }

        public bool TryGetResolvedSite(SiteRef role, out ResolvedSiteId site)
        {
            for (var i = 0; i < Sites.Bindings.Count; i++)
            {
                if (!Sites.Bindings[i].Role.Equals(role)) continue;
                site = Sites.Bindings[i].Site;
                return true;
            }
            site = default;
            return false;
        }

        public bool TryGetPhysicalAnchor(SiteRef role, out Int2 centreDm)
        {
            centreDm = default;
            return TryGetResolvedSite(role, out ResolvedSiteId site)
                && _facts.TryGetCentre(site, out centreDm);
        }
    }

    public static class AuthoredFullRunCampaignGenerator
    {
        public static AuthoredFullRunCampaignGenerationPlan Plan(AuthoredFullRunPhysicalWorldPlan world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            var facts = new AuthoredFullRunPhysicalSiteFacts(world);
            SiteResolutionResult sites = SiteRoleResolver.Resolve(world.Graph, facts);
            if (!sites.IsResolved)
                throw new InvalidOperationException(FormatFailure(sites));

            IReadOnlyList<NpcSiteAssignment> npcs =
                NpcPlacementResolver.ResolveSites(world.Graph, sites);

            return new AuthoredFullRunCampaignGenerationPlan(world, facts, sites, npcs);
        }

        private static string FormatFailure(SiteResolutionResult result)
        {
            string text = "The authored full-run campaign cannot resolve against the recovered physical macro world.";
            for (var i = 0; i < result.Diagnostics.Count; i++)
                text += "\n" + result.Diagnostics[i];
            return text;
        }
    }

    /// <summary>
    /// Candidate facts projected directly from the recovered top-down physical plan. Settlement sites
    /// use real generated building blockouts. Region-owned encounter sites use the owning region's first
    /// source-backed settlement as an outdoor anchor, so no continuation coordinate is authored here.
    /// </summary>
    internal sealed class AuthoredFullRunPhysicalSiteFacts : ISiteCandidateFacts, ICutsceneStageCandidateFacts
    {
        private sealed class Record
        {
            public SiteCandidate Candidate;
            public RegionRef Region;
            public SettlementRef Settlement;
            public bool HasSettlement;
            public string OwnerNodeId;
            public Int2 CentreDm;
            public int HalfWidthDm;
            public int HalfDepthDm;
        }

        private static readonly SiteCapabilityOffer[] GameplayCapabilities =
        {
            new SiteCapabilityOffer(SiteCapabilityKind.ConversationSpace, 16),
            new SiteCapabilityOffer(SiteCapabilityKind.CutsceneStage, 16)
        };

        private readonly SiteCandidate[] _candidates;
        private readonly Dictionary<ResolvedSiteId, Record> _records;
        private readonly TopDownWorldPhysicalPlan _physical;

        public IReadOnlyList<SiteCandidate> Candidates => _candidates;

        public AuthoredFullRunPhysicalSiteFacts(AuthoredFullRunPhysicalWorldPlan world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            _physical = world.Physical;
            _records = new Dictionary<ResolvedSiteId, Record>();
            var candidates = new List<SiteCandidate>();

            WorldHierarchyPlan hierarchy = world.Graph.HierarchyPlan;
            for (var i = 0; i < hierarchy.Settlements.Count; i++)
            {
                WorldSettlementPlan semantic = hierarchy.Settlements[i];
                if (!world.TryGetPhysicalSettlement(semantic.Settlement, out TopDownWorldSettlementPlan physical))
                    throw new InvalidOperationException("Missing physical settlement for '" + semantic.Settlement + "'.");
                if (physical.Buildings.Count == 0)
                    throw new InvalidOperationException(
                        "Physical settlement '" + semantic.Settlement + "' exposes no generated site blockouts.");

                for (var buildingIndex = 0; buildingIndex < physical.Buildings.Count; buildingIndex++)
                {
                    TopDownWorldBuildingBlockoutPlan building = physical.Buildings[buildingIndex];
                    Add(
                        candidates,
                        new ResolvedSiteId(semantic.Settlement.Id + "/building-" + buildingIndex.ToString("D2")),
                        semantic.Region,
                        semantic.Settlement,
                        true,
                        semantic.Settlement.Id,
                        building.CentreDm,
                        building.HalfExtentXDm,
                        building.HalfExtentZDm);
                }
            }

            for (var regionIndex = 0; regionIndex < hierarchy.Regions.Count; regionIndex++)
            {
                WorldRegionPlan region = hierarchy.Regions[regionIndex];
                if (region.RegionOwnedSites.Count == 0) continue;
                if (!TryResolveRegionAnchor(hierarchy, region.Region, out WorldSettlementPlan semantic, out TopDownWorldSettlementPlan physical))
                    throw new InvalidOperationException(
                        "Region '" + region.Region + "' owns campaign sites but has no source-backed settlement anchor.");

                int halfWidth = TopDownWorldPhysicalPlanner.GenericSettlementStreetHalfWidthDm;
                int halfDepth = TopDownWorldPhysicalPlanner.GenericSettlementStreetHalfWidthDm;
                Add(
                    candidates,
                    new ResolvedSiteId(region.Region.Id + "/outdoor-" + semantic.Settlement.Id),
                    region.Region,
                    default,
                    false,
                    semantic.Settlement.Id,
                    physical.CentreDm,
                    halfWidth,
                    halfDepth);
            }

            _candidates = candidates.ToArray();
        }

        public bool TryGetCentre(ResolvedSiteId site, out Int2 centreDm)
        {
            if (_records.TryGetValue(site, out Record record))
            {
                centreDm = record.CentreDm;
                return true;
            }
            centreDm = default;
            return false;
        }

        public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) =>
            _records.TryGetValue(candidate, out Record record) && record.Region.Equals(region);

        public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) =>
            _records.TryGetValue(candidate, out Record record)
            && record.HasSettlement
            && record.Settlement.Equals(settlement);

        public bool IsReachable(ResolvedSiteId subject, ResolvedSiteId target, TraversalProfile traversal)
        {
            if (!TryRecord(subject, out Record from) || !TryRecord(target, out Record to)) return false;
            if (string.Equals(from.OwnerNodeId, to.OwnerNodeId, StringComparison.Ordinal)) return true;

            var visited = new HashSet<string>(StringComparer.Ordinal) { from.OwnerNodeId };
            var queue = new Queue<string>();
            queue.Enqueue(from.OwnerNodeId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                for (var i = 0; i < _physical.Routes.Count; i++)
                {
                    TopDownWorldPhysicalRoutePlan route = _physical.Routes[i];
                    string next = null;
                    if (string.Equals(route.Route.FromId, current, StringComparison.Ordinal)) next = route.Route.ToId;
                    else if (string.Equals(route.Route.ToId, current, StringComparison.Ordinal)) next = route.Route.FromId;
                    if (next == null || !visited.Add(next)) continue;
                    if (string.Equals(next, to.OwnerNodeId, StringComparison.Ordinal)) return true;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        public int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target)
        {
            if (!TryRecord(subject, out Record from) || !TryRecord(target, out Record to)) return int.MaxValue;
            int centreDm = EuclideanDistanceDm(from.CentreDm, to.CentreDm);
            int clearanceDm = Math.Max(from.HalfWidthDm, from.HalfDepthDm)
                + Math.Max(to.HalfWidthDm, to.HalfDepthDm);
            return Math.Max(0, centreDm - clearanceDm) / 10;
        }

        public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) =>
            CentreDistanceMetres(subject, target);

        public int TraversalDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target, TraversalProfile traversal) =>
            IsReachable(subject, target, traversal) ? CentreDistanceMetres(subject, target) : int.MaxValue;

        public bool TryGetCutsceneStageEnvelope(ResolvedSiteId candidate, out CutsceneStageEnvelope envelope)
        {
            if (!TryRecord(candidate, out Record record))
            {
                envelope = default;
                return false;
            }

            int halfWidth = Math.Max(10, record.HalfWidthDm - 5);
            int depth = Math.Max(20, record.HalfDepthDm * 2 - 10);
            envelope = new CutsceneStageEnvelope(halfWidth, depth);
            return true;
        }

        private int CentreDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target)
        {
            if (!TryRecord(subject, out Record from) || !TryRecord(target, out Record to)) return int.MaxValue;
            return EuclideanDistanceDm(from.CentreDm, to.CentreDm) / 10;
        }

        private bool TryResolveRegionAnchor(
            WorldHierarchyPlan hierarchy,
            RegionRef region,
            out WorldSettlementPlan semantic,
            out TopDownWorldSettlementPlan physical)
        {
            for (var i = 0; i < hierarchy.Settlements.Count; i++)
            {
                WorldSettlementPlan candidate = hierarchy.Settlements[i];
                if (!candidate.Region.Equals(region)) continue;
                if (!_physical.TryGetSettlement(candidate.Settlement.Id, out physical)) continue;
                semantic = candidate;
                return true;
            }
            semantic = null;
            physical = null;
            return false;
        }

        private void Add(
            List<SiteCandidate> candidates,
            ResolvedSiteId id,
            RegionRef region,
            SettlementRef settlement,
            bool hasSettlement,
            string ownerNodeId,
            Int2 centreDm,
            int halfWidthDm,
            int halfDepthDm)
        {
            var candidate = new SiteCandidate(id, SiteArchetype.Unspecified, GameplayCapabilities);
            var record = new Record
            {
                Candidate = candidate,
                Region = region,
                Settlement = settlement,
                HasSettlement = hasSettlement,
                OwnerNodeId = ownerNodeId,
                CentreDm = centreDm,
                HalfWidthDm = halfWidthDm,
                HalfDepthDm = halfDepthDm
            };
            if (!_records.TryAdd(id, record))
                throw new InvalidOperationException("Duplicate full-run physical site candidate '" + id + "'.");
            candidates.Add(candidate);
        }

        private bool TryRecord(ResolvedSiteId site, out Record record) => _records.TryGetValue(site, out record);

        private static int EuclideanDistanceDm(Int2 a, Int2 b)
        {
            long dx = (long)a.X - b.X;
            long dz = (long)a.Y - b.Y;
            return (int)Math.Round(Math.Sqrt(dx * dx + dz * dz));
        }
    }
}
