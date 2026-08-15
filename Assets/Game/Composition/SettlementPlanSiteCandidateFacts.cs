using System;
using System.Collections.Generic;
using System.Globalization;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Explicit projection from one WorldGen planned building/site into the gameplay semantics
    /// understood by WorldBuilder. The composition layer requires these facts to be supplied rather
    /// than inferring gameplay capabilities from a visual/architectural archetype.
    /// </summary>
    public readonly struct SettlementSiteProjection
    {
        public SiteArchetype Archetype { get; }
        public IReadOnlyList<SiteCapabilityOffer> Capabilities { get; }
        public int WidthDm { get; }
        public int DepthDm { get; }

        public SettlementSiteProjection(
            SiteArchetype archetype,
            int widthDm,
            int depthDm,
            params SiteCapabilityOffer[] capabilities)
        {
            if (archetype == SiteArchetype.Unspecified)
                throw new ArgumentException(
                    "A projected generated site must have a concrete WorldBuilder archetype.",
                    nameof(archetype));
            if (widthDm <= 0) throw new ArgumentOutOfRangeException(nameof(widthDm));
            if (depthDm <= 0) throw new ArgumentOutOfRangeException(nameof(depthDm));

            Archetype = archetype;
            WidthDm = widthDm;
            DepthDm = depthDm;
            Capabilities = capabilities ?? Array.Empty<SiteCapabilityOffer>();
        }
    }

    /// <summary>
    /// Content/generator-specific semantic projection. Returning false excludes a planned site from
    /// the WorldBuilder candidate set; the adapter never guesses an archetype or capability.
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
    /// Boundary and public-entrance Euclidean distances are derived from planned footprint geometry.
    /// Traversal queries are delegated to ISettlementTraversalFacts and are never synthesized.
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
            Entry a = RequireEntry(subject);
            Entry b = RequireEntry(target);

            int aMinX = a.Site.PositionDm.X;
            int aMaxX = aMinX + a.Projection.WidthDm;
            int aMinZ = a.Site.PositionDm.Y;
            int aMaxZ = aMinZ + a.Projection.DepthDm;

            int bMinX = b.Site.PositionDm.X;
            int bMaxX = bMinX + b.Projection.WidthDm;
            int bMinZ = b.Site.PositionDm.Y;
            int bMaxZ = bMinZ + b.Projection.DepthDm;

            int dx = AxisGap(aMinX, aMaxX, bMinX, bMaxX);
            int dz = AxisGap(aMinZ, aMaxZ, bMinZ, bMaxZ);
            double distanceDm = Math.Sqrt((double)dx * dx + (double)dz * dz);
            return DecimetresToNearestMetre(distanceDm);
        }

        public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target)
        {
            Entry a = RequireEntry(subject);
            Entry b = RequireEntry(target);
            Point2 aEntrance = PublicEntrance(a);
            Point2 bEntrance = PublicEntrance(b);

            double dx = aEntrance.X - bEntrance.X;
            double dz = aEntrance.Z - bEntrance.Z;
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

        private static Point2 PublicEntrance(Entry entry)
        {
            double minX = entry.Site.PositionDm.X;
            double maxX = minX + entry.Projection.WidthDm;
            double minZ = entry.Site.PositionDm.Y;
            double maxZ = minZ + entry.Projection.DepthDm;
            double centreX = (minX + maxX) / 2.0;
            double centreZ = (minZ + maxZ) / 2.0;

            switch ((FrontageDirection)entry.Site.Orientation)
            {
                case FrontageDirection.South: return new Point2(centreX, minZ);
                case FrontageDirection.West:  return new Point2(minX, centreZ);
                case FrontageDirection.North: return new Point2(centreX, maxZ);
                case FrontageDirection.East:  return new Point2(maxX, centreZ);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported planned-site orientation '{entry.Site.Orientation}'.");
            }
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

        private readonly struct Point2
        {
            public double X { get; }
            public double Z { get; }

            public Point2(double x, double z)
            {
                X = x;
                Z = z;
            }
        }
    }
}
