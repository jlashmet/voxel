using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum SiteArchetype
    {
        Unspecified = 0,
        Pub = 1,
        Settlement = 2,
        Ruin = 3,
        Cave = 4,
        Camp = 5,
        Fort = 6,
        Dungeon = 7
    }

    /// <summary>
    /// How an authored SiteRef role is resolved. Every role has cardinality exactly one.
    /// RequiredArchetype means the resolved site must use the declared archetype.
    /// ConstraintMatch means archetype is intentionally unconstrained and the planner may choose
    /// any generated site that satisfies every capability, ownership, topology, and spatial constraint.
    /// </summary>
    public enum SiteResolutionMode
    {
        RequiredArchetype = 0,
        ConstraintMatch = 1
    }

    public enum SiteCapabilityKind
    {
        Interior = 0,
        PlayerSpawn = 1,
        CutsceneStage = 2,
        PublicExit = 3,
        ConversationSpace = 4,
        SecretCandidateHost = 5
    }

    public enum SiteCapabilitySource
    {
        Authored = 0,
        Derived = 1
    }

    public readonly struct SiteCapabilityRequirement
    {
        public SiteCapabilityKind Kind { get; }
        public int MinimumCapacity { get; }
        public SiteCapabilitySource Source { get; }

        public SiteCapabilityRequirement(SiteCapabilityKind kind, int minimumCapacity = 1)
            : this(kind, minimumCapacity, SiteCapabilitySource.Authored)
        {
        }

        internal SiteCapabilityRequirement(
            SiteCapabilityKind kind,
            int minimumCapacity,
            SiteCapabilitySource source)
        {
            if (minimumCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
            Kind = kind;
            MinimumCapacity = minimumCapacity;
            Source = source;
        }

        internal SiteCapabilityRequirement AsDerived() =>
            new SiteCapabilityRequirement(Kind, MinimumCapacity, SiteCapabilitySource.Derived);
    }

    public static class SiteCapability
    {
        public static SiteCapabilityRequirement Interior => new SiteCapabilityRequirement(SiteCapabilityKind.Interior);
        public static SiteCapabilityRequirement PlayerSpawn(int count) => new SiteCapabilityRequirement(SiteCapabilityKind.PlayerSpawn, count);
        public static SiteCapabilityRequirement CutsceneStage => new SiteCapabilityRequirement(SiteCapabilityKind.CutsceneStage);
        public static SiteCapabilityRequirement PublicExit => new SiteCapabilityRequirement(SiteCapabilityKind.PublicExit);
        public static SiteCapabilityRequirement ConversationSpace => new SiteCapabilityRequirement(SiteCapabilityKind.ConversationSpace);
        public static SiteCapabilityRequirement SecretCandidateHost => new SiteCapabilityRequirement(SiteCapabilityKind.SecretCandidateHost);
    }

    public enum TraversalProfile
    {
        NormalParty = 0
    }

    public enum SpatialConstraintKind
    {
        DifferentSite = 0,
        ReachableFrom = 1,
        DistanceRange = 2
    }

    /// <summary>
    /// Exact interpretation of a site-to-site distance constraint.
    /// BoundaryToBoundaryEuclidean is the minimum horizontal Euclidean distance between the two
    /// realized site footprint boundaries. PublicEntranceToPublicEntranceEuclidean is straight-line
    /// horizontal distance between each site's primary public access anchor. TraversalPathLength is
    /// the shortest valid navigation-graph path between those anchors for the supplied traversal profile.
    /// </summary>
    public enum SiteDistanceMetric
    {
        BoundaryToBoundaryEuclidean = 0,
        PublicEntranceToPublicEntranceEuclidean = 1,
        TraversalPathLength = 2
    }

    public readonly struct DistanceRangeMetres
    {
        public int Minimum { get; }
        public int Maximum { get; }

        public DistanceRangeMetres(int minimum, int maximum)
        {
            if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    public sealed class SpatialConstraintSpec
    {
        public SpatialConstraintKind Kind { get; }
        public SiteRef Subject { get; }
        public SiteRef Target { get; }
        public TraversalProfile Traversal { get; }
        public DistanceRangeMetres Distance { get; }
        public SiteDistanceMetric DistanceMetric { get; }

        private SpatialConstraintSpec(
            SpatialConstraintKind kind,
            SiteRef subject,
            SiteRef target,
            TraversalProfile traversal,
            DistanceRangeMetres distance,
            SiteDistanceMetric distanceMetric)
        {
            Kind = kind;
            Subject = subject;
            Target = target;
            Traversal = traversal;
            Distance = distance;
            DistanceMetric = distanceMetric;
        }

        public static SpatialConstraintSpec DifferentSite(SiteRef subject, SiteRef target) =>
            new SpatialConstraintSpec(
                SpatialConstraintKind.DifferentSite,
                subject,
                target,
                default,
                default,
                default);

        /// <summary>
        /// Requires at least one valid path in the final traversal graph. This does not constrain
        /// path length; use TraversalDistanceRange when the authored requirement also limits travel distance.
        /// </summary>
        public static SpatialConstraintSpec ReachableFrom(
            SiteRef subject,
            SiteRef target,
            TraversalProfile traversal) =>
            new SpatialConstraintSpec(
                SpatialConstraintKind.ReachableFrom,
                subject,
                target,
                traversal,
                default,
                default);

        public static SpatialConstraintSpec BoundaryDistanceRange(
            SiteRef subject,
            SiteRef target,
            DistanceRangeMetres distance) =>
            new SpatialConstraintSpec(
                SpatialConstraintKind.DistanceRange,
                subject,
                target,
                default,
                distance,
                SiteDistanceMetric.BoundaryToBoundaryEuclidean);

        public static SpatialConstraintSpec PublicEntranceDistanceRange(
            SiteRef subject,
            SiteRef target,
            DistanceRangeMetres distance) =>
            new SpatialConstraintSpec(
                SpatialConstraintKind.DistanceRange,
                subject,
                target,
                default,
                distance,
                SiteDistanceMetric.PublicEntranceToPublicEntranceEuclidean);

        public static SpatialConstraintSpec TraversalDistanceRange(
            SiteRef subject,
            SiteRef target,
            TraversalProfile traversal,
            DistanceRangeMetres distance) =>
            new SpatialConstraintSpec(
                SpatialConstraintKind.DistanceRange,
                subject,
                target,
                traversal,
                distance,
                SiteDistanceMetric.TraversalPathLength);
    }

    /// <summary>
    /// Stable authored site role. A SiteRef always resolves to exactly one concrete site identity.
    /// Archetype is a hard constraint when supplied; otherwise ResolutionMode is ConstraintMatch and
    /// the planner has freedom to choose an archetype while satisfying the rest of the blueprint.
    /// </summary>
    public sealed class SiteSpec
    {
        public SiteRef Ref { get; }
        public SiteArchetype Archetype { get; }
        public SiteResolutionMode ResolutionMode =>
            Archetype == SiteArchetype.Unspecified
                ? SiteResolutionMode.ConstraintMatch
                : SiteResolutionMode.RequiredArchetype;
        public int RequiredCardinality => 1;
        public IReadOnlyList<SiteCapabilityRequirement> Capabilities { get; }

        internal SiteSpec(SiteRef @ref, SiteArchetype archetype, SiteCapabilityRequirement[] capabilities)
        {
            Ref = @ref;
            Archetype = archetype;
            Capabilities = capabilities ?? Array.Empty<SiteCapabilityRequirement>();
        }
    }

    public sealed class NpcSpec
    {
        public NpcRef Ref { get; }
        public SiteRef Site { get; }
        public bool RequiresConversation { get; }

        internal NpcSpec(NpcRef @ref, SiteRef site, bool requiresConversation)
        {
            Ref = @ref;
            Site = site;
            RequiresConversation = requiresConversation;
        }
    }
}
