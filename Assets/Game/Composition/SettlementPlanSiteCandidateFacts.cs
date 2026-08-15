using System;
using System.Collections.Generic;
using System.Globalization;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Exact horizontal world-space footprint bounds for one generated site, in WorldGen decimetres.
    /// Composition consumes these resolved bounds instead of trying to reconstruct rotation, wings,
    /// overhangs, or other generator-owned geometry from an archetype name.
    /// </summary>
    public readonly struct SiteFootprintBoundsDm
    {
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        public SiteFootprintBoundsDm(int minX, int minZ, int maxX, int maxZ)
        {
            if (maxX <= minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxZ <= minZ) throw new ArgumentOutOfRangeException(nameof(maxZ));

            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }
    }

    /// <summary>
    /// Explicit projection from one WorldGen planned site into the gameplay semantics understood by
    /// WorldBuilder. Geometry is already resolved in world space: the projection provider must supply
    /// the real footprint and public-entrance anchor rather than letting Composition infer them from
    /// frontage or architectural archetype. Archetype may remain Unspecified when the WorldGen site
    /// has no honest mapping into WorldBuilder's semantic taxonomy.
    /// </summary>
    public readonly struct SettlementSiteProjection
    {
        public SiteArchetype Archetype { get; }
        public IReadOnlyList<SiteCapabilityOffer> Capabilities { get; }
        public SiteFootprintBoundsDm Footprint { get; }
        public Int2 PublicEntranceDm { get; }

        public SettlementSiteProjection(
            SiteArchetype archetype,
            SiteFootprintBoundsDm footprint,
            Int2 publicEntranceDm,
            params SiteCapabilityOffer[] capabilities)
        {
            Archetype = archetype;
            Footprint = footprint;
            PublicEntranceDm = publicEntranceDm;
            Capabilities = capabilities ?? Array.Empty<SiteCapabilityOffer>();
        }
    }

    /// <summary>
    /// Content/generator-specific semantic projection. Returning false excludes a planned site from
    /// the WorldBuilder candidate set; the adapter never guesses archetype, capability, footprint,
    /// or entrance geometry.
    /// </summary>
    public interface ISettlementSiteProjectionProvider
    {
        bool TryProject(PlannedSite site, out SettlementSiteProjection projection);
    }

    /// <summary>
    /// Traversal truth for a settlement plan. This must be backed by the actual traversal/navigation
    /// model. It is deliberately separate from geometry so Composition cannot substitute Euclidean
    /// distance for reachability or path length.
    /// </summary>
    public interface ISettlementTraversalFacts
    {
        bool IsReachable(
            int subjectRoleId,
            int targetRoleId,
            TraversalProfile traversal);

        int TraversalDistanceMetres(
            int subjectRoleId,
            int targetRoleId,
            TraversalProfile traversal);
    }

    /// <summary>
    /// Composition bridge from the renderer-independent WorldGen SettlementPlan into WorldBuilder's
    /// site-role solver input. This adapter intentionally covers only building-scale PlannedSite
    /// candidates that the supplied projection provider can describe honestly.
    ///
    /// Euclidean metrics use explicit resolved footprint/entrance facts. Traversal queries are
    /// delegated to ISettlementTraversalFacts and are never synthesized.
    /// </summary>
    public sealed class SettlementPlanSiteCandidateFacts : ISiteCandidateFacts
    {
        private readonly RegionRef _region;
        private readonly SettlementRef _settlement;
        private readonly ISettlementTraversalFacts _traversal;
        private readonly SiteCandidate[] _candidates;
        private readonly Dictionary<ResolvedSiteId, Entry> _entries;

        public IReadOnlyList<SiteCandidate> Candidates => _candidates;

        public SettlementPlanSiteCandidateFacts(
            SettlementPlan plan,
            RegionRef region,
            SettlementRef settlement,
            ISettlementSiteProjectionProvider projections,
            ISettlementTraversalFacts traversal)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (projections == null) throw new ArgumentNullException(nameof(projections));
            _traversal = traversal ?? throw new ArgumentNullException(nameof(traversal));

            _region = region;
            _settlement = settlement;
            _entries = new Dictionary<ResolvedSiteId, Entry>();

            var candidates = new List<SiteCandidate>(plan.Sites.Count);
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                if (!projections.TryProject(site, out projection))
                    continue;

                ResolvedSiteId id = CandidateId(plan.Id, site.RoleId);
                if (_entries.ContainsKey(id))
                    throw new InvalidOperationException(
                        $"Settlement plan '{plan.Id}' exposes duplicate projected site role id '{site.RoleId}'.");

                SiteCapabilityOffer[] capabilities = CopyCapabilities(projection.Capabilities);
                var candidate = new SiteCandidate(id, projection.Archetype, capabilities);
                candidates.Add(candidate);
                _entries.Add(id, new Entry(site, projection));
            }

            _candidates = candidates.ToArray();
        }

        public static ResolvedSiteId CandidateId(string settlementPlanId, int roleId)
        {
            if (string.IsNullOrWhiteSpace(settlementPlanId))
                throw new ArgumentException("Settlement plan id is required.", nameof(settlementPlanId));

            return new ResolvedSiteId(
                settlementPlanId + "/site/" + roleId.ToString(CultureInfo.InvariantCulture));
        }

        public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) =>
            _entries.ContainsKey(candidate) && region.Equals(_region);

        public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) =>
            _entries.ContainsKey(candidate) && settlement.Equals(_settlement);

        public bool IsReachable(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal)
        {
            Entry subjectEntry = RequireEntry(subject);
            Entry targetEntry = RequireEntry(target);
            return _traversal.IsReachable(
                subjectEntry.Site.RoleId,
                targetEntry.Site.RoleId,
                traversal);
        }

        public int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target)
        {
            SiteFootprintBoundsDm a = RequireEntry(subject).Projection.Footprint;
            SiteFootprintBoundsDm b = RequireEntry(target).Projection.Footprint;

            int dx = AxisGap(a.MinX, a.MaxX, b.MinX, b.MaxX);
            int dz = AxisGap(a.MinZ, a.MaxZ, b.MinZ, b.MaxZ);
            double distanceDm = Math.Sqrt((double)dx * dx + (double)dz * dz);
            return DecimetresToNearestMetre(distanceDm);
        }

        public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target)
        {
            Int2 a = RequireEntry(subject).Projection.PublicEntranceDm;
            Int2 b = RequireEntry(target).Projection.PublicEntranceDm;

            double dx = a.X - b.X;
            double dz = a.Y - b.Y;
            return DecimetresToNearestMetre(Math.Sqrt(dx * dx + dz * dz));
        }

        public int TraversalDistanceMetres(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal)
        {
            Entry subjectEntry = RequireEntry(subject);
            Entry targetEntry = RequireEntry(target);
            return _traversal.TraversalDistanceMetres(
                subjectEntry.Site.RoleId,
                targetEntry.Site.RoleId,
                traversal);
        }

        private Entry RequireEntry(ResolvedSiteId id)
        {
            Entry entry;
            if (_entries.TryGetValue(id, out entry)) return entry;
            throw new ArgumentException(
                $"Resolved site '{id}' is not a projected candidate in this settlement plan.",
                nameof(id));
        }

        private static SiteCapabilityOffer[] CopyCapabilities(
            IReadOnlyList<SiteCapabilityOffer> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<SiteCapabilityOffer>();

            var copy = new SiteCapabilityOffer[source.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }

        private static int AxisGap(int aMin, int aMax, int bMin, int bMax)
        {
            if (aMax < bMin) return bMin - aMax;
            if (bMax < aMin) return aMin - bMax;
            return 0;
        }

        private static int DecimetresToNearestMetre(double decimetres) =>
            (int)Math.Round(decimetres / 10.0, MidpointRounding.AwayFromZero);

        private sealed class Entry
        {
            public PlannedSite Site { get; }
            public SettlementSiteProjection Projection { get; }

            public Entry(PlannedSite site, SettlementSiteProjection projection)
            {
                Site = site;
                Projection = projection;
            }
        }
    }
}
