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

    public enum SiteCapabilityKind
    {
        Interior = 0,
        PlayerSpawn = 1,
        CutsceneStage = 2,
        PublicExit = 3,
        ConversationSpace = 4,
        SecretCandidateHost = 5
    }

    public readonly struct SiteCapabilityRequirement
    {
        public SiteCapabilityKind Kind { get; }
        public int MinimumCapacity { get; }

        public SiteCapabilityRequirement(SiteCapabilityKind kind, int minimumCapacity = 1)
        {
            if (minimumCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
            Kind = kind;
            MinimumCapacity = minimumCapacity;
        }
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

        private SpatialConstraintSpec(
            SpatialConstraintKind kind,
            SiteRef subject,
            SiteRef target,
            TraversalProfile traversal,
            DistanceRangeMetres distance)
        {
            Kind = kind;
            Subject = subject;
            Target = target;
            Traversal = traversal;
            Distance = distance;
        }

        public static SpatialConstraintSpec DifferentSite(SiteRef subject, SiteRef target) =>
            new SpatialConstraintSpec(SpatialConstraintKind.DifferentSite, subject, target, default, default);

        public static SpatialConstraintSpec ReachableFrom(SiteRef subject, SiteRef target, TraversalProfile traversal) =>
            new SpatialConstraintSpec(SpatialConstraintKind.ReachableFrom, subject, target, traversal, default);

        public static SpatialConstraintSpec DistanceRange(SiteRef subject, SiteRef target, DistanceRangeMetres distance) =>
            new SpatialConstraintSpec(SpatialConstraintKind.DistanceRange, subject, target, default, distance);
    }

    public sealed class SiteSpec
    {
        public SiteRef Ref { get; }
        public SiteArchetype Archetype { get; }
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
