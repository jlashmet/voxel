using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    public enum PlanningNodeKind
    {
        Site = 0,
        Npc = 1,
        LootTable = 2,
        SecretPolicy = 3,
        Objective = 4,
        Cutscene = 5
    }

    public sealed class PlanningNode
    {
        public string Id { get; }
        public PlanningNodeKind Kind { get; }
        public IReadOnlyList<string> Dependencies { get; }

        internal PlanningNode(string id, PlanningNodeKind kind, string[] dependencies)
        {
            Id = id;
            Kind = kind;
            Dependencies = dependencies ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Typed physical requirements imposed by one cutscene on one generated site. A future
    /// generation adapter consumes these and produces a CutsceneStageBinding after realization.
    /// </summary>
    public sealed class CutsceneStagePlan
    {
        public CutsceneRef Cutscene { get; }
        public SiteRef Site { get; }
        public IReadOnlyList<CutsceneStagePointId> RequiredPoints { get; }

        internal CutsceneStagePlan(CutsceneRef cutscene, SiteRef site, CutsceneStagePointId[] requiredPoints)
        {
            Cutscene = cutscene;
            Site = site;
            RequiredPoints = requiredPoints ?? Array.Empty<CutsceneStagePointId>();
        }
    }

    public sealed class PlanningGraph
    {
        public IReadOnlyList<PlanningNode> Nodes { get; }
        public IReadOnlyList<CutsceneStagePlan> CutsceneStages { get; }

        internal PlanningGraph(PlanningNode[] nodes, CutsceneStagePlan[] cutsceneStages)
        {
            Nodes = nodes ?? Array.Empty<PlanningNode>();
            CutsceneStages = cutsceneStages ?? Array.Empty<CutsceneStagePlan>();
        }
    }

    /// <summary>
    /// First compilation boundary between authored campaign data and a future spatial
    /// orchestration adapter (for example LayerProcGen). This compiler deliberately
    /// produces engine-agnostic dependency nodes and typed stage requirements; it does not invoke generation.
    /// </summary>
    public static class BlueprintCompiler
    {
        public static PlanningGraph Compile(CampaignBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));

            var validation = BlueprintValidator.Validate(blueprint);
            if (!validation.IsValid)
                throw new InvalidOperationException("Campaign blueprint contains validation errors.");

            var nodes = new List<PlanningNode>();
            var stagePlans = new List<CutsceneStagePlan>();

            for (var i = 0; i < blueprint.Sites.Count; i++)
                nodes.Add(new PlanningNode(NodeId("site", blueprint.Sites[i].Ref.Id), PlanningNodeKind.Site, Array.Empty<string>()));

            for (var i = 0; i < blueprint.LootTables.Count; i++)
                nodes.Add(new PlanningNode(NodeId("loot", blueprint.LootTables[i].Ref.Id), PlanningNodeKind.LootTable, Array.Empty<string>()));

            for (var i = 0; i < blueprint.Npcs.Count; i++)
            {
                var npc = blueprint.Npcs[i];
                nodes.Add(new PlanningNode(
                    NodeId("npc", npc.Ref.Id),
                    PlanningNodeKind.Npc,
                    new[] { NodeId("site", npc.Site.Id) }));
            }

            for (var i = 0; i < blueprint.SecretPolicies.Count; i++)
            {
                var policy = blueprint.SecretPolicies[i];
                nodes.Add(new PlanningNode(
                    NodeId("secret-policy", policy.Ref.Id),
                    PlanningNodeKind.SecretPolicy,
                    new[] { NodeId("loot", policy.Reward.Id) }));
            }

            for (var i = 0; i < blueprint.Objectives.Count; i++)
            {
                var objective = blueprint.Objectives[i];
                var dependencies = new List<string> { NodeId("site", objective.Target.Id) };
                if (objective.Completion is InteractWithNpcTriggerSpec interact)
                    dependencies.Add(NodeId("npc", interact.Npc.Id));

                nodes.Add(new PlanningNode(
                    NodeId("objective", objective.Ref.Id),
                    PlanningNodeKind.Objective,
                    dependencies.ToArray()));
            }

            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                var cutscene = blueprint.Cutscenes[i];
                var dependencies = new List<string> { NodeId("site", cutscene.Site.Id) };

                if (cutscene.Trigger is InteractWithNpcTriggerSpec interact)
                    AddUnique(dependencies, NodeId("npc", interact.Npc.Id));

                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    var binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind == CutsceneActorTargetKind.Npc)
                        AddUnique(dependencies, NodeId("npc", binding.Target.Npc.Id));
                }

                // Story conditions/effects are runtime state dependencies, not generation
                // dependencies. Keeping them out of this graph prevents story sequencing
                // from introducing cycles into spatial realization.
                nodes.Add(new PlanningNode(
                    NodeId("cutscene", cutscene.Ref.Id),
                    PlanningNodeKind.Cutscene,
                    dependencies.ToArray()));

                if (cutscene.StageRequirements.Count > 0)
                {
                    var points = new CutsceneStagePointId[cutscene.StageRequirements.Count];
                    for (var j = 0; j < points.Length; j++)
                        points[j] = cutscene.StageRequirements[j];
                    stagePlans.Add(new CutsceneStagePlan(cutscene.Ref, cutscene.Site, points));
                }
            }

            return new PlanningGraph(nodes.ToArray(), stagePlans.ToArray());
        }

        private static string NodeId(string kind, string id) => $"{kind}:{id}";

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }
    }
}
