using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.CaveWorldBuilder
{
    /// <summary>
    /// One verified physical cave pocket exposed at an authored WorldBuilder site role. Quality is a
    /// generator-supplied deterministic ranking; this bridge never invents a score from world distance.
    /// </summary>
    public readonly struct CaveSecretPocketProjection
    {
        public readonly SiteRef Site;
        public readonly CaveSecretPocket Pocket;
        public readonly int QualityBasisPoints;

        public CaveSecretPocketProjection(
            SiteRef site,
            in CaveSecretPocket pocket,
            int qualityBasisPoints)
        {
            if (string.IsNullOrEmpty(site.Id))
                throw new ArgumentException("Cave secret projection requires a valid site role.", nameof(site));
            if (!pocket.IsWellFormed)
                throw new ArgumentException(
                    "Cave secret projection requires a pocket verified by CaveSecretPocketAuthoring.",
                    nameof(pocket));
            if (qualityBasisPoints < 0 || qualityBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(qualityBasisPoints));

            Site = site;
            Pocket = pocket;
            QualityBasisPoints = qualityBasisPoints;
        }

        public bool IsWellFormed =>
            !string.IsNullOrEmpty(Site.Id) &&
            Pocket.IsWellFormed &&
            QualityBasisPoints >= 0 && QualityBasisPoints <= 10000;
    }

    /// <summary>
    /// WorldBuilder-facing candidate provider backed only by physically verified cave pockets. The same
    /// verified pockets also implement IHiddenSpaceRealizationFacts, so downstream gameplay resolves
    /// container and entrance geometry from the exact candidate/entrance ids selected by SecretPlanner.
    ///
    /// Candidate identity is derived from physical voxel geometry and deliberately excludes SiteRef, so
    /// semantic aliases of one physical cave share one reservation/realization identity.
    /// </summary>
    public sealed class CaveSecretPocketSecretCandidateProvider :
        ISecretCandidateProvider,
        IHiddenSpaceRealizationFacts
    {
        private readonly Dictionary<SiteRef, IReadOnlyList<SecretCandidate>> _candidates =
            new Dictionary<SiteRef, IReadOnlyList<SecretCandidate>>();
        private readonly Dictionary<string, RealizedWorldBounds> _candidateBounds =
            new Dictionary<string, RealizedWorldBounds>(StringComparer.Ordinal);
        private readonly Dictionary<string, RealizedWorldBounds> _entranceBounds =
            new Dictionary<string, RealizedWorldBounds>(StringComparer.Ordinal);

        public CaveSecretPocketSecretCandidateProvider(
            int voxelsPerDecimetre,
            IReadOnlyList<CaveSecretPocketProjection> projections)
        {
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));
            if (projections == null) throw new ArgumentNullException(nameof(projections));

            var mutable = new Dictionary<SiteRef, List<SecretCandidate>>();
            var idsBySite = new Dictionary<SiteRef, HashSet<string>>();
            var qualityByPhysicalId = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < projections.Count; i++)
            {
                CaveSecretPocketProjection projection = projections[i];
                if (!projection.IsWellFormed)
                    throw new ArgumentException(
                        "Cave secret projection at index " + i + " is not verified/well formed.",
                        nameof(projections));

                CaveSecretPocket pocket = projection.Pocket;
                string candidateId = PhysicalCandidateId(in pocket);
                string entranceId = EntranceId(candidateId);
                SecretCandidate candidate = ToCandidate(in projection, candidateId, entranceId);

                int existingQuality;
                if (qualityByPhysicalId.TryGetValue(candidateId, out existingQuality))
                {
                    if (existingQuality != projection.QualityBasisPoints)
                        throw new InvalidOperationException(
                            "Physical cave secret candidate '" + candidate.Id +
                            "' was projected with inconsistent quality across site aliases.");
                }
                else
                {
                    qualityByPhysicalId.Add(candidateId, projection.QualityBasisPoints);
                }

                AddOrVerifyBounds(
                    _candidateBounds,
                    candidateId,
                    RealizedBounds(in pocket.Pocket, voxelsPerDecimetre),
                    "candidate");
                AddOrVerifyBounds(
                    _entranceBounds,
                    entranceId,
                    RealizedBounds(in pocket.Barrier, voxelsPerDecimetre),
                    "entrance");

                HashSet<string> siteIds;
                if (!idsBySite.TryGetValue(projection.Site, out siteIds))
                {
                    siteIds = new HashSet<string>(StringComparer.Ordinal);
                    idsBySite.Add(projection.Site, siteIds);
                }
                if (!siteIds.Add(candidateId))
                    throw new InvalidOperationException(
                        "Physical cave secret candidate '" + candidate.Id +
                        "' was projected more than once for site '" + projection.Site + "'.");

                List<SecretCandidate> list;
                if (!mutable.TryGetValue(projection.Site, out list))
                {
                    list = new List<SecretCandidate>();
                    mutable.Add(projection.Site, list);
                }
                list.Add(candidate);
            }

            foreach (KeyValuePair<SiteRef, List<SecretCandidate>> pair in mutable)
            {
                pair.Value.Sort((left, right) =>
                    string.CompareOrdinal(left.Id.Id, right.Id.Id));
                _candidates.Add(pair.Key, pair.Value.ToArray());
            }
        }

        public IReadOnlyList<SecretCandidate> GetCandidates(SiteRef site)
        {
            IReadOnlyList<SecretCandidate> candidates;
            return _candidates.TryGetValue(site, out candidates)
                ? candidates
                : Array.Empty<SecretCandidate>();
        }

        public bool TryGetCandidateBounds(string candidateId, out RealizedWorldBounds bounds)
        {
            if (candidateId == null)
            {
                bounds = default(RealizedWorldBounds);
                return false;
            }
            return _candidateBounds.TryGetValue(candidateId, out bounds);
        }

        public bool TryGetEntranceBounds(string entranceId, out RealizedWorldBounds bounds)
        {
            if (entranceId == null)
            {
                bounds = default(RealizedWorldBounds);
                return false;
            }
            return _entranceBounds.TryGetValue(entranceId, out bounds);
        }

        private static SecretCandidate ToCandidate(
            in CaveSecretPocketProjection projection,
            string candidateId,
            string entranceId)
        {
            CaveSecretPocket pocket = projection.Pocket;
            var entrance = new SecretEntranceCandidate(
                entranceId,
                SecretEntranceType.DestroyableFalseWall,
                pocket.SeparatesHiddenSpaceBeforeOpen,
                pocket.GrantsNormalTraversalAfterOpen,
                pocket.IsStructurallyCritical,
                pocket.SupportsDestruction,
                pocket.CanMatchHostSurface);

            return new SecretCandidate(
                new SecretCandidateId(candidateId),
                projection.Site,
                SecretSpaceKind.SideCave,
                pocket.SeparatesHiddenSpaceBeforeOpen,
                projection.QualityBasisPoints,
                new[] { entrance });
        }

        private static RealizedWorldBounds RealizedBounds(
            in DecorationBounds bounds,
            int voxelsPerDecimetre)
        {
            return new RealizedWorldBounds(
                new Int3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
                new Int3(
                    bounds.MaxExclusive.x - 1,
                    bounds.MaxExclusive.y - 1,
                    bounds.MaxExclusive.z - 1),
                voxelsPerDecimetre);
        }

        private static void AddOrVerifyBounds(
            Dictionary<string, RealizedWorldBounds> values,
            string id,
            RealizedWorldBounds bounds,
            string kind)
        {
            RealizedWorldBounds existing;
            if (!values.TryGetValue(id, out existing))
            {
                values.Add(id, bounds);
                return;
            }

            if (!SameBounds(in existing, in bounds))
                throw new InvalidOperationException(
                    "Physical cave secret " + kind + " id '" + id +
                    "' resolved to inconsistent voxel bounds across site aliases.");
        }

        private static bool SameBounds(
            in RealizedWorldBounds left,
            in RealizedWorldBounds right) =>
            left.UnitsPerDecimetre == right.UnitsPerDecimetre &&
            SamePoint(left.MinInclusive, right.MinInclusive) &&
            SamePoint(left.MaxInclusive, right.MaxInclusive);

        private static bool SamePoint(Int3 left, Int3 right) =>
            left.X == right.X && left.Y == right.Y && left.Z == right.Z;

        private static string EntranceId(string candidateId) => candidateId + "/barrier";

        private static string PhysicalCandidateId(in CaveSecretPocket pocket)
        {
            return string.Concat(
                "cave-secret/terminal/",
                Integer(pocket.Terminal.Position.x), ",",
                Integer(pocket.Terminal.Position.y), ",",
                Integer(pocket.Terminal.Position.z), "/facing/",
                Integer((int)pocket.Terminal.ExitFacing),
                "/barrier/", BoundsKey(in pocket.Barrier),
                "/connector/", BoundsKey(in pocket.Connector),
                "/pocket/", BoundsKey(in pocket.Pocket));
        }

        private static string BoundsKey(in DecorationBounds bounds) =>
            string.Concat(
                Integer(bounds.Min.x), ",", Integer(bounds.Min.y), ",", Integer(bounds.Min.z), "-",
                Integer(bounds.MaxExclusive.x), ",", Integer(bounds.MaxExclusive.y), ",",
                Integer(bounds.MaxExclusive.z));

        private static string Integer(int value) =>
            value.ToString(CultureInfo.InvariantCulture);
    }
}
