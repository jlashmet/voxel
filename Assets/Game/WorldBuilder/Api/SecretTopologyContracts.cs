using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum SecretSpaceKind
    {
        HiddenRoom = 0,
        CavityBehindWall = 1,
        SideCave = 2,
        HiddenAlcove = 3
    }

    /// <summary>One authored secret that must resolve at one specific site.</summary>
    public sealed class RequiredSecretSpec
    {
        public SecretRef Ref { get; }
        public SiteRef Site { get; }
        public SecretEntranceType Entrance { get; }
        public bool RequiresHiddenSpace { get; }
        public ContainerArchetype Container { get; }
        public LootTableRef Reward { get; }

        internal RequiredSecretSpec(
            SecretRef @ref,
            SiteRef site,
            SecretEntranceType entrance,
            bool requiresHiddenSpace,
            ContainerArchetype container,
            LootTableRef reward)
        {
            Ref = @ref;
            Site = site;
            Entrance = entrance;
            RequiresHiddenSpace = requiresHiddenSpace;
            Container = container;
            Reward = reward;
        }
    }

    public readonly struct SecretCandidateId : IEquatable<SecretCandidateId>
    {
        public string Id { get; }
        public SecretCandidateId(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretCandidateId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretCandidateId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct SecretEntranceCandidate
    {
        public string Id { get; }
        public SecretEntranceType Type { get; }
        public bool SeparatesHiddenSpaceBeforeOpen { get; }
        public bool GrantsNormalTraversalAfterOpen { get; }
        public bool IsStructurallyCritical { get; }
        public bool SupportsDestruction { get; }
        public bool CanMatchHostSurface { get; }

        public SecretEntranceCandidate(
            string id,
            SecretEntranceType type,
            bool separatesHiddenSpaceBeforeOpen,
            bool grantsNormalTraversalAfterOpen,
            bool isStructurallyCritical,
            bool supportsDestruction,
            bool canMatchHostSurface)
        {
            Id = WorldIdRules.Require(id, nameof(id));
            Type = type;
            SeparatesHiddenSpaceBeforeOpen = separatesHiddenSpaceBeforeOpen;
            GrantsNormalTraversalAfterOpen = grantsNormalTraversalAfterOpen;
            IsStructurallyCritical = isStructurallyCritical;
            SupportsDestruction = supportsDestruction;
            CanMatchHostSurface = canMatchHostSurface;
        }
    }

    /// <summary>
    /// A real hidden-space opportunity exposed by a generated site. The hidden volume already exists;
    /// WorldBuilder selects among candidates instead of inventing rooms after geometry is finalized.
    /// QualityBasisPoints is a deterministic 0..10000 ranking supplied by the generator.
    /// </summary>
    public sealed class SecretCandidate
    {
        public SecretCandidateId Id { get; }
        public SiteRef Site { get; }
        public SecretSpaceKind SpaceKind { get; }
        public bool HiddenFromNormalTraversal { get; }
        public int QualityBasisPoints { get; }
        public IReadOnlyList<SecretEntranceCandidate> Entrances { get; }

        public SecretCandidate(
            SecretCandidateId id,
            SiteRef site,
            SecretSpaceKind spaceKind,
            bool hiddenFromNormalTraversal,
            int qualityBasisPoints,
            SecretEntranceCandidate[] entrances)
        {
            if (qualityBasisPoints < 0 || qualityBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(qualityBasisPoints));
            Id = id;
            Site = site;
            SpaceKind = spaceKind;
            HiddenFromNormalTraversal = hiddenFromNormalTraversal;
            QualityBasisPoints = qualityBasisPoints;
            Entrances = entrances ?? Array.Empty<SecretEntranceCandidate>();
        }
    }

    /// <summary>
    /// Implemented by building/cave/world realization code. External generators reference only
    /// Game.WorldBuilder.Api; selection policy remains inside Game.WorldBuilder.Runtime.
    /// </summary>
    public interface ISecretCandidateProvider
    {
        IReadOnlyList<SecretCandidate> GetCandidates(SiteRef site);
    }

    public enum SecretResolutionSourceKind
    {
        Policy = 0,
        RequiredSecret = 1
    }

    public sealed class ResolvedSecretPlan
    {
        public SecretResolutionSourceKind SourceKind { get; }
        public SecretPolicyRef Policy { get; }
        public SecretRef RequiredSecret { get; }
        public SiteRef Site { get; }
        public SecretCandidateId Candidate { get; }
        public string EntranceId { get; }
        public ContainerArchetype Container { get; }
        public LootTableRef Reward { get; }

        public ResolvedSecretPlan(
            SecretPolicyRef policy,
            SiteRef site,
            SecretCandidateId candidate,
            string entranceId,
            ContainerArchetype container,
            LootTableRef reward)
        {
            SourceKind = SecretResolutionSourceKind.Policy;
            Policy = policy;
            RequiredSecret = default;
            Site = site;
            Candidate = candidate;
            EntranceId = WorldIdRules.Require(entranceId, nameof(entranceId));
            Container = container;
            Reward = reward;
        }

        public ResolvedSecretPlan(
            SecretRef requiredSecret,
            SiteRef site,
            SecretCandidateId candidate,
            string entranceId,
            ContainerArchetype container,
            LootTableRef reward)
        {
            SourceKind = SecretResolutionSourceKind.RequiredSecret;
            Policy = default;
            RequiredSecret = requiredSecret;
            Site = site;
            Candidate = candidate;
            EntranceId = WorldIdRules.Require(entranceId, nameof(entranceId));
            Container = container;
            Reward = reward;
        }
    }
}
