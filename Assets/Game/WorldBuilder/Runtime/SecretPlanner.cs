using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    public static class SecretPlanner
    {
        private sealed class ValidCandidate
        {
            public SecretCandidate Candidate;
            public SecretEntranceCandidate Entrance;
            public uint TieBreak;
        }

        public static IReadOnlyList<ResolvedSecretPlan> ResolveForSite(
            SecretPolicySpec policy,
            SiteRef site,
            ISecretCandidateProvider provider,
            uint worldSeed)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            IReadOnlyList<SecretCandidate> supplied = provider.GetCandidates(site) ?? Array.Empty<SecretCandidate>();
            var valid = new List<ValidCandidate>();
            for (var i = 0; i < supplied.Count; i++)
            {
                SecretCandidate candidate = supplied[i];
                if (!candidate.Site.Equals(site)) continue;
                if (policy.RequiresHiddenSpace && !candidate.HiddenFromNormalTraversal) continue;
                if (!TrySelectEntrance(policy, candidate, out SecretEntranceCandidate entrance)) continue;
                valid.Add(new ValidCandidate
                {
                    Candidate = candidate,
                    Entrance = entrance,
                    TieBreak = StableHash(worldSeed, policy.Ref.Id, site.Id, candidate.Id.Id)
                });
            }

            valid.Sort(CompareCandidates);
            int required = policy.Distribution.MinimumPerEligibleSite;
            if (valid.Count < required)
                throw new InvalidOperationException(
                    "Secret policy '" + policy.Ref + "' requires at least " + required +
                    " valid secret candidate(s) at site '" + site + "', but only " + valid.Count + " exist.");

            int selectedCount = Math.Min(required, valid.Count);
            int maximum = Math.Min(policy.Distribution.MaximumPerEligibleSite, valid.Count);
            for (int slot = required; slot < maximum; slot++)
            {
                uint trial = StableHash(worldSeed, policy.Ref.Id, site.Id, "slot:" + slot);
                if (trial % 10000u < (uint)policy.Distribution.ProbabilityBasisPoints) selectedCount++;
            }

            var result = new List<ResolvedSecretPlan>(selectedCount);
            for (var i = 0; i < selectedCount; i++)
            {
                ValidCandidate selected = valid[i];
                result.Add(new ResolvedSecretPlan(
                    policy.Ref, site, selected.Candidate.Id, selected.Entrance.Id,
                    policy.Container, policy.Reward));
            }
            return result;
        }

        public static ResolvedSecretPlan ResolveRequired(
            RequiredSecretSpec secret,
            ISecretCandidateProvider provider,
            uint worldSeed)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            IReadOnlyList<SecretCandidate> supplied = provider.GetCandidates(secret.Site) ?? Array.Empty<SecretCandidate>();
            var valid = new List<ValidCandidate>();
            for (var i = 0; i < supplied.Count; i++)
            {
                SecretCandidate candidate = supplied[i];
                if (!candidate.Site.Equals(secret.Site)) continue;
                if (secret.RequiresHiddenSpace && !candidate.HiddenFromNormalTraversal) continue;
                if (!TrySelectEntrance(secret.Entrance, candidate, out SecretEntranceCandidate entrance)) continue;
                valid.Add(new ValidCandidate
                {
                    Candidate = candidate,
                    Entrance = entrance,
                    TieBreak = StableHash(worldSeed, secret.Ref.Id, secret.Site.Id, candidate.Id.Id)
                });
            }

            if (valid.Count == 0)
                throw new InvalidOperationException(
                    "Required secret '" + secret.Ref + "' has no valid '" + secret.Entrance +
                    "' candidate at site '" + secret.Site + "'.");

            valid.Sort(CompareCandidates);
            ValidCandidate selected = valid[0];
            return new ResolvedSecretPlan(
                secret.Ref,
                secret.Site,
                selected.Candidate.Id,
                selected.Entrance.Id,
                secret.Container,
                secret.Reward);
        }

        private static bool TrySelectEntrance(SecretPolicySpec policy, SecretCandidate candidate, out SecretEntranceCandidate selected)
        {
            bool found = false;
            selected = default;
            for (var i = 0; i < candidate.Entrances.Count; i++)
            {
                SecretEntranceCandidate entrance = candidate.Entrances[i];
                if (!AllowsEntrance(policy, entrance.Type) || !EntranceSatisfiesTopology(entrance)) continue;
                if (!found || string.CompareOrdinal(entrance.Id, selected.Id) < 0)
                {
                    found = true;
                    selected = entrance;
                }
            }
            return found;
        }

        private static bool TrySelectEntrance(SecretEntranceType requiredType, SecretCandidate candidate, out SecretEntranceCandidate selected)
        {
            bool found = false;
            selected = default;
            for (var i = 0; i < candidate.Entrances.Count; i++)
            {
                SecretEntranceCandidate entrance = candidate.Entrances[i];
                if (entrance.Type != requiredType || !EntranceSatisfiesTopology(entrance)) continue;
                if (!found || string.CompareOrdinal(entrance.Id, selected.Id) < 0)
                {
                    found = true;
                    selected = entrance;
                }
            }
            return found;
        }

        private static bool EntranceSatisfiesTopology(SecretEntranceCandidate entrance)
        {
            if (!entrance.SeparatesHiddenSpaceBeforeOpen) return false;
            if (!entrance.GrantsNormalTraversalAfterOpen) return false;
            if (entrance.IsStructurallyCritical) return false;
            if (entrance.Type == SecretEntranceType.DestroyableFalseWall)
            {
                if (!entrance.SupportsDestruction) return false;
                if (!entrance.CanMatchHostSurface) return false;
            }
            return true;
        }

        private static bool AllowsEntrance(SecretPolicySpec policy, SecretEntranceType type)
        {
            for (var i = 0; i < policy.EntranceTypes.Count; i++) if (policy.EntranceTypes[i] == type) return true;
            return false;
        }

        private static int CompareCandidates(ValidCandidate left, ValidCandidate right)
        {
            int quality = right.Candidate.QualityBasisPoints.CompareTo(left.Candidate.QualityBasisPoints);
            if (quality != 0) return quality;
            int tie = left.TieBreak.CompareTo(right.TieBreak);
            if (tie != 0) return tie;
            return string.CompareOrdinal(left.Candidate.Id.Id, right.Candidate.Id.Id);
        }

        private static uint StableHash(uint seed, params string[] values)
        {
            unchecked
            {
                uint hash = 2166136261u ^ seed;
                for (var i = 0; i < values.Length; i++)
                {
                    string value = values[i] ?? string.Empty;
                    for (var j = 0; j < value.Length; j++)
                    {
                        hash ^= value[j];
                        hash *= 16777619u;
                    }
                    hash ^= 0xffu;
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
