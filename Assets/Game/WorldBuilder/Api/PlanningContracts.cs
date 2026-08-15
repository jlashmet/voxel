using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.WorldBuilder.Api
{
    public enum PlanningNodeKind
    {
        Region = 0,
        Route = 1,
        Settlement = 2,
        Site = 3,
        Npc = 4,
        LootTable = 5,
        SecretPolicy = 6,
        Objective = 7,
        Cutscene = 8,
        RequiredSecret = 9
    }

    public sealed class PlanningNode
    {
        public string Id { get; }
        public PlanningNodeKind Kind { get; }
        public IReadOnlyList<string> Dependencies { get; }

        public PlanningNode(string id, PlanningNodeKind kind, string[] dependencies)
        {
            Id = WorldIdRules.Require(id, nameof(id));
            Kind = kind;
            Dependencies = dependencies ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Typed solver input for one stable authored SiteRef role. Every role resolves to exactly one
    /// concrete site. ConstraintMatch intentionally leaves archetype free; RequiredArchetype makes
    /// Archetype a hard requirement. Capabilities include both explicit and content-derived needs.
    /// </summary>
    public sealed class SiteRolePlan
    {
        public SiteRef Role { get; }
        public SiteResolutionMode ResolutionMode { get; }
        public SiteArchetype Archetype { get; }
        public int RequiredCardinality { get; }
        public IReadOnlyList<SiteCapabilityRequirement> Capabilities { get; }

        public SiteRolePlan(
            SiteRef role,
            SiteResolutionMode resolutionMode,
            SiteArchetype archetype,
            int requiredCardinality,
            SiteCapabilityRequirement[] capabilities)
        {
            if (requiredCardinality != 1)
                throw new ArgumentOutOfRangeException(nameof(requiredCardinality),
                    "A SiteRef role currently resolves to exactly one concrete site.");
            if (resolutionMode == SiteResolutionMode.RequiredArchetype
                && archetype == SiteArchetype.Unspecified)
                throw new ArgumentException(
                    "RequiredArchetype site role must declare an archetype.", nameof(archetype));
            if (resolutionMode == SiteResolutionMode.ConstraintMatch
                && archetype != SiteArchetype.Unspecified)
                throw new ArgumentException(
                    "ConstraintMatch site role must leave archetype unconstrained.", nameof(archetype));

            Role = role;
            ResolutionMode = resolutionMode;
            Archetype = archetype;
            RequiredCardinality = requiredCardinality;
            Capabilities = capabilities ?? Array.Empty<SiteCapabilityRequirement>();
        }
    }

    public sealed class CutsceneStagePlan
    {
        public CutsceneRef Cutscene { get; }
        public CutsceneDefinition Definition { get; }
        public SiteRef Site { get; }
        public IReadOnlyList<CutsceneStagePointRequirement> Requirements { get; }

        public CutsceneStagePlan(
            CutsceneRef cutscene,
            CutsceneDefinition definition,
            SiteRef site,
            CutsceneStagePointRequirement[] requirements)
        {
            Cutscene = cutscene;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Site = site;
            Requirements = requirements ?? Array.Empty<CutsceneStagePointRequirement>();
        }
    }

    /// <summary>
    /// Requirements a site generator must satisfy so a procedural secret policy has real topology
    /// to select from. MinimumCandidateCount is hard; PreferredCandidateCount is a soft target.
    /// </summary>
    public sealed class SecretCandidatePlan
    {
        public SecretPolicyRef Policy { get; }
        public SiteRef Site { get; }
        public bool RequiresHiddenSpace { get; }
        public int MinimumCandidateCount { get; }
        public int PreferredCandidateCount { get; }
        public IReadOnlyList<SecretEntranceType> AllowedEntrances { get; }

        public SecretCandidatePlan(
            SecretPolicyRef policy,
            SiteRef site,
            bool requiresHiddenSpace,
            int minimumCandidateCount,
            int preferredCandidateCount,
            SecretEntranceType[] allowedEntrances)
        {
            if (minimumCandidateCount < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumCandidateCount));
            if (preferredCandidateCount < minimumCandidateCount)
                throw new ArgumentOutOfRangeException(nameof(preferredCandidateCount));

            Policy = policy;
            Site = site;
            RequiresHiddenSpace = requiresHiddenSpace;
            MinimumCandidateCount = minimumCandidateCount;
            PreferredCandidateCount = preferredCandidateCount;
            AllowedEntrances = allowedEntrances ?? Array.Empty<SecretEntranceType>();
        }
    }

    /// <summary>
    /// Hard generation request for one authored secret. Exactly one valid candidate must be available
    /// at the specified site; failure to provide one makes world generation invalid.
    /// </summary>
    public sealed class RequiredSecretCandidatePlan
    {
        public SecretRef Secret { get; }
        public SiteRef Site { get; }
        public bool RequiresHiddenSpace { get; }
        public SecretEntranceType Entrance { get; }

        public RequiredSecretCandidatePlan(
            SecretRef secret,
            SiteRef site,
            bool requiresHiddenSpace,
            SecretEntranceType entrance)
        {
            Secret = secret;
            Site = site;
            RequiresHiddenSpace = requiresHiddenSpace;
            Entrance = entrance;
        }
    }

    /// <summary>
    /// Engine-independent output of blueprint compilation. Dependency Nodes give generation ordering;
    /// typed collections carry the actual solver inputs so a spatial planner does not have to recover
    /// semantics from node names.
    /// </summary>
    public sealed class PlanningGraph
    {
        public IReadOnlyList<PlanningNode> Nodes { get; }
        public IReadOnlyList<SiteRolePlan> SiteRoles { get; }
        public WorldHierarchyBlueprint Hierarchy { get; }
        public IReadOnlyList<SpatialConstraintSpec> SpatialConstraints { get; }
        public IReadOnlyList<NpcPlacementPlan> NpcPlacements { get; }
        public IReadOnlyList<CutsceneStagePlan> CutsceneStages { get; }
        public IReadOnlyList<SecretCandidatePlan> SecretCandidates { get; }
        public IReadOnlyList<RequiredSecretCandidatePlan> RequiredSecrets { get; }

        public PlanningGraph(PlanningNode[] nodes, CutsceneStagePlan[] cutsceneStages)
            : this(
                nodes,
                Array.Empty<SiteRolePlan>(),
                EmptyHierarchy(),
                Array.Empty<SpatialConstraintSpec>(),
                Array.Empty<NpcPlacementPlan>(),
                cutsceneStages,
                null,
                null)
        {
        }

        public PlanningGraph(
            PlanningNode[] nodes,
            CutsceneStagePlan[] cutsceneStages,
            SecretCandidatePlan[] secretCandidates)
            : this(
                nodes,
                Array.Empty<SiteRolePlan>(),
                EmptyHierarchy(),
                Array.Empty<SpatialConstraintSpec>(),
                Array.Empty<NpcPlacementPlan>(),
                cutsceneStages,
                secretCandidates,
                null)
        {
        }

        public PlanningGraph(
            PlanningNode[] nodes,
            CutsceneStagePlan[] cutsceneStages,
            SecretCandidatePlan[] secretCandidates,
            RequiredSecretCandidatePlan[] requiredSecrets)
            : this(
                nodes,
                Array.Empty<SiteRolePlan>(),
                EmptyHierarchy(),
                Array.Empty<SpatialConstraintSpec>(),
                Array.Empty<NpcPlacementPlan>(),
                cutsceneStages,
                secretCandidates,
                requiredSecrets)
        {
        }

        public PlanningGraph(
            PlanningNode[] nodes,
            SiteRolePlan[] siteRoles,
            WorldHierarchyBlueprint hierarchy,
            SpatialConstraintSpec[] spatialConstraints,
            CutsceneStagePlan[] cutsceneStages,
            SecretCandidatePlan[] secretCandidates,
            RequiredSecretCandidatePlan[] requiredSecrets)
            : this(
                nodes,
                siteRoles,
                hierarchy,
                spatialConstraints,
                Array.Empty<NpcPlacementPlan>(),
                cutsceneStages,
                secretCandidates,
                requiredSecrets)
        {
        }

        public PlanningGraph(
            PlanningNode[] nodes,
            SiteRolePlan[] siteRoles,
            WorldHierarchyBlueprint hierarchy,
            SpatialConstraintSpec[] spatialConstraints,
            NpcPlacementPlan[] npcPlacements,
            CutsceneStagePlan[] cutsceneStages,
            SecretCandidatePlan[] secretCandidates,
            RequiredSecretCandidatePlan[] requiredSecrets)
        {
            Nodes = nodes ?? Array.Empty<PlanningNode>();
            SiteRoles = siteRoles ?? Array.Empty<SiteRolePlan>();
            Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
            SpatialConstraints = spatialConstraints ?? Array.Empty<SpatialConstraintSpec>();
            NpcPlacements = npcPlacements ?? Array.Empty<NpcPlacementPlan>();
            CutsceneStages = cutsceneStages ?? Array.Empty<CutsceneStagePlan>();
            SecretCandidates = secretCandidates ?? Array.Empty<SecretCandidatePlan>();
            RequiredSecrets = requiredSecrets ?? Array.Empty<RequiredSecretCandidatePlan>();
        }

        private static WorldHierarchyBlueprint EmptyHierarchy() =>
            new WorldHierarchyBlueprint(null, null, null, null, null);
    }

    public readonly struct CutsceneSiteGeometry
    {
        public CutsceneInt3 EntrancePosition { get; }
        public CutsceneInt3 Inward { get; }
        public CutsceneInt3 Right { get; }
        public int InteriorHalfWidthDecimetres { get; }
        public int InteriorDepthDecimetres { get; }

        public CutsceneSiteGeometry(
            CutsceneInt3 entrancePosition,
            CutsceneInt3 inward,
            CutsceneInt3 right,
            int interiorHalfWidthDecimetres,
            int interiorDepthDecimetres)
        {
            if (!IsHorizontalUnit(inward))
                throw new ArgumentException("Cutscene site inward axis must be a horizontal cardinal unit vector.", nameof(inward));
            if (!IsHorizontalUnit(right))
                throw new ArgumentException("Cutscene site right axis must be a horizontal cardinal unit vector.", nameof(right));
            if (Dot(inward, right) != 0)
                throw new ArgumentException("Cutscene site inward and right axes must be orthogonal.", nameof(right));
            if (interiorHalfWidthDecimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(interiorHalfWidthDecimetres));
            if (interiorDepthDecimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(interiorDepthDecimetres));

            EntrancePosition = entrancePosition;
            Inward = inward;
            Right = right;
            InteriorHalfWidthDecimetres = interiorHalfWidthDecimetres;
            InteriorDepthDecimetres = interiorDepthDecimetres;
        }

        private static bool IsHorizontalUnit(CutsceneInt3 value) =>
            value.Y == 0 && Math.Abs(value.X) + Math.Abs(value.Z) == 1;

        private static int Dot(CutsceneInt3 a, CutsceneInt3 b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    public interface ICutsceneSiteGeometryProvider
    {
        bool TryResolve(SiteRef site, out CutsceneSiteGeometry geometry);
    }

    public sealed class CutsceneStageRealization
    {
        public CutsceneRef Cutscene { get; }
        public SiteRef Site { get; }
        public CutsceneStageBinding Binding { get; }

        public CutsceneStageRealization(
            CutsceneRef cutscene,
            SiteRef site,
            CutsceneStageBinding binding)
        {
            Cutscene = cutscene;
            Site = site;
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        }
    }
}
