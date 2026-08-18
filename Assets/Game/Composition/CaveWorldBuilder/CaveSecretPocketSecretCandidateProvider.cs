using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;

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
    /// WorldBuilder-facing candidate provider backed only by physically verified cave pockets.
    /// The candidate id is derived from physical voxel geometry and deliberately excludes SiteRef,
    /// so semantic aliases of one physical cave share the same reservation identity.
    /// </summary>
    public sealed class CaveSecretPocketSecretCandidateProvider : ISecretCandidateProvider
    {
        private readonly Dictionary<SiteRef, IReadOnlyList<SecretCandidate>> _candidates =
            new Dictionary<SiteRef, IReadOnlyList<SecretCandidate>>();

        public CaveSecretPocketSecretCandidateProvider(
            IReadOnlyList<CaveSecretPocketProjection> projections)
        {
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

                SecretCandidate candidate = ToCandidate(in projection);

                int existingQuality;
                if (qualityByPhysicalId.TryGetValue(candidate.Id.Id, out existingQuality))
                {
                    if (existingQuality != projection.QualityBasisPoints)
                        throw new InvalidOperationException(
                            "Physical cave secret candidate '" + candidate.Id +
                            "' was projected with inconsistent quality across site aliases.");
                }
                else
                {
                    qualityByPhysicalId.Add(candidate.Id.Id, projection.QualityBasisPoints);
                }

                HashSet<string> siteIds;
                if (!idsBySite.TryGetValue(projection.Site, out siteIds))
                {
                    siteIds = new HashSet<string>(StringComparer.Ordinal);
                    idsBySite.Add(projection.Site, siteIds);
                }
                if (!siteIds.Add(candidate.Id.Id))
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

        private static SecretCandidate ToCandidate(in CaveSecretPocketProjection projection)
        {
            CaveSecretPocket pocket = projection.Pocket;
            string candidateId = PhysicalCandidateId(in pocket);
            var entrance = new SecretEntranceCandidate(
                candidateId + "/barrier",
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

        private static string BoundsKey(in Game.Structures.Api.DecorationBounds bounds) =>
            string.Concat(
                Integer(bounds.Min.x), ",", Integer(bounds.Min.y), ",", Integer(bounds.Min.z), "-",
                Integer(bounds.MaxExclusive.x), ",", Integer(bounds.MaxExclusive.y), ",",
                Integer(bounds.MaxExclusive.z));

        private static string Integer(int value) =>
            value.ToString(CultureInfo.InvariantCulture);
    }
}
