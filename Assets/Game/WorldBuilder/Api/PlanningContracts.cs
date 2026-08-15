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
        Cutscene = 8
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
    /// Physical requirements imposed by one authored cutscene on one generated site.
    /// This is an exposed planning contract: world-generation adapters may consume it, but they
    /// never reference Game.WorldBuilder.Runtime.
    /// </summary>
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

    public sealed class PlanningGraph
    {
        public IReadOnlyList<PlanningNode> Nodes { get; }
        public IReadOnlyList<CutsceneStagePlan> CutsceneStages { get; }

        public PlanningGraph(PlanningNode[] nodes, CutsceneStagePlan[] cutsceneStages)
        {
            Nodes = nodes ?? Array.Empty<PlanningNode>();
            CutsceneStages = cutsceneStages ?? Array.Empty<CutsceneStagePlan>();
        }
    }

    /// <summary>
    /// Backend-neutral realized geometry needed to stage a cutscene inside a site.
    /// Positions and dimensions are integer decimetres. EntrancePosition is the public threshold;
    /// Inward and Right are orthogonal horizontal cardinal unit axes in world space.
    /// </summary>
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

    /// <summary>
    /// Implemented by world realization/composition after a site has concrete geometry.
    /// Consumers need only Game.WorldBuilder.Api.
    /// </summary>
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
