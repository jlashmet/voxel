using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public readonly struct SecretRouteId : IEquatable<SecretRouteId>
    {
        public string Id { get; }
        public SecretRouteId(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretRouteId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretRouteId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct SecretClueAnchorId : IEquatable<SecretClueAnchorId>
    {
        public string Id { get; }
        public SecretClueAnchorId(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretClueAnchorId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretClueAnchorId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public enum SecretImportance
    {
        Minor = 0,
        Standard = 1,
        Major = 2
    }

    public enum SecretRouteKind
    {
        Door = 0,
        Trapdoor = 1,
        Pushable = 2,
        BreakableBarrier = 3,
        PressurePlateMechanism = 4,
        Climb = 5,
        Swim = 6,
        NaturalTraversal = 7,
        ScriptedMechanism = 8
    }

    public enum SecretClueChannel
    {
        Spatial = 0,
        Visual = 1,
        Audio = 2,
        Environmental = 3,
        Mechanical = 4,
        Narrative = 5,
        Navigation = 6
    }

    public enum SecretClueAnchorRole
    {
        ApproachEvidence = 0,
        ExteriorEvidence = 1,
        RouteAdjacentEvidence = 2,
        SightlineHint = 3,
        AcousticHint = 4,
        TraversalHint = 5,
        NarrativeHint = 6
    }

    public enum SecretHiddenVolumeRelation
    {
        Outside = 0,
        Boundary = 1,
        Inside = 2
    }

    public enum SecretBypassPolicy
    {
        ProtectedShell = 0,
        AuthoredBreakablesOnly = 1,
        SystemicBypassAllowed = 2
    }

    /// <summary>
    /// Geometry analysis supplied to planning/validation. WorldBuilder owns the policy decision while
    /// the voxel/feature realization layer owns how these facts are measured from generated geometry.
    /// </summary>
    public readonly struct SecretBypassEvidence
    {
        public bool HasTrivialUnintendedBypass { get; }
        public int DesignatedBreakableVoxelCount { get; }
        public int UndesignatedBreakableVoxelCount { get; }

        public SecretBypassEvidence(
            bool hasTrivialUnintendedBypass,
            int designatedBreakableVoxelCount,
            int undesignatedBreakableVoxelCount)
        {
            if (designatedBreakableVoxelCount < 0) throw new ArgumentOutOfRangeException(nameof(designatedBreakableVoxelCount));
            if (undesignatedBreakableVoxelCount < 0) throw new ArgumentOutOfRangeException(nameof(undesignatedBreakableVoxelCount));
            HasTrivialUnintendedBypass = hasTrivialUnintendedBypass;
            DesignatedBreakableVoxelCount = designatedBreakableVoxelCount;
            UndesignatedBreakableVoxelCount = undesignatedBreakableVoxelCount;
        }
    }

    public sealed class SecretRouteSpec
    {
        public SecretRouteId Id { get; }
        public SecretRef Secret { get; }
        public SecretRouteKind Kind { get; }
        public SecretBypassPolicy BypassPolicy { get; }
        public string SemanticAnchorRole { get; }
        public bool RequiresInteractable { get; }
        public SecretBypassEvidence BypassEvidence { get; }

        public SecretRouteSpec(
            SecretRouteId id,
            SecretRef secret,
            SecretRouteKind kind,
            SecretBypassPolicy bypassPolicy,
            string semanticAnchorRole,
            bool requiresInteractable,
            SecretBypassEvidence bypassEvidence)
        {
            Id = id;
            Secret = secret;
            Kind = kind;
            BypassPolicy = bypassPolicy;
            SemanticAnchorRole = WorldIdRules.Require(semanticAnchorRole, nameof(semanticAnchorRole));
            RequiresInteractable = requiresInteractable;
            BypassEvidence = bypassEvidence;
        }
    }

    /// <summary>
    /// Reusable semantic clue anchor emitted by generated feature/site authoring. It contains no prefab
    /// name, Transform, collider, or capture coordinate. Realization can map the role/channel later.
    /// </summary>
    public sealed class SecretClueAnchorSpec
    {
        public SecretClueAnchorId Id { get; }
        public SiteRef Site { get; }
        public SecretClueAnchorRole Role { get; }
        public IReadOnlyList<SecretClueChannel> Channels { get; }
        public bool PreSolveObservable { get; }
        public SecretHiddenVolumeRelation HiddenVolumeRelation { get; }
        public float UsefulDistanceMin { get; }
        public float UsefulDistanceMax { get; }
        public bool HasRouteDependency { get; }
        public SecretRouteId RouteDependency { get; }
        public bool HasExplainedRoute { get; }
        public SecretRouteId ExplainedRoute { get; }

        public SecretClueAnchorSpec(
            SecretClueAnchorId id,
            SiteRef site,
            SecretClueAnchorRole role,
            SecretClueChannel[] channels,
            bool preSolveObservable,
            SecretHiddenVolumeRelation hiddenVolumeRelation,
            float usefulDistanceMin,
            float usefulDistanceMax,
            SecretRouteId routeDependency = default,
            bool hasRouteDependency = false,
            SecretRouteId explainedRoute = default,
            bool hasExplainedRoute = false)
        {
            if (channels == null || channels.Length == 0)
                throw new ArgumentException("A clue anchor must support at least one semantic channel.", nameof(channels));
            if (usefulDistanceMin < 0f) throw new ArgumentOutOfRangeException(nameof(usefulDistanceMin));
            if (usefulDistanceMax < usefulDistanceMin) throw new ArgumentOutOfRangeException(nameof(usefulDistanceMax));

            Id = id;
            Site = site;
            Role = role;
            Channels = channels;
            PreSolveObservable = preSolveObservable;
            HiddenVolumeRelation = hiddenVolumeRelation;
            UsefulDistanceMin = usefulDistanceMin;
            UsefulDistanceMax = usefulDistanceMax;
            HasRouteDependency = hasRouteDependency;
            RouteDependency = routeDependency;
            HasExplainedRoute = hasExplainedRoute;
            ExplainedRoute = explainedRoute;
        }
    }

    public sealed class SecretDiscoverySpec
    {
        public SecretRef Secret { get; }
        public SecretImportance Importance { get; }
        public IReadOnlyList<SecretRouteSpec> Routes { get; }
        public IReadOnlyList<SecretClueAnchorSpec> ClueAnchors { get; }
        public int? MinimumClueOverride { get; }

        public SecretDiscoverySpec(
            SecretRef secret,
            SecretImportance importance,
            SecretRouteSpec[] routes,
            SecretClueAnchorSpec[] clueAnchors,
            int? minimumClueOverride = null)
        {
            if (routes == null || routes.Length == 0)
                throw new ArgumentException("A generated secret requires at least one legal route.", nameof(routes));
            if (minimumClueOverride.HasValue && minimumClueOverride.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumClueOverride));

            Secret = secret;
            Importance = importance;
            Routes = routes;
            ClueAnchors = clueAnchors ?? Array.Empty<SecretClueAnchorSpec>();
            MinimumClueOverride = minimumClueOverride;
        }
    }
}
