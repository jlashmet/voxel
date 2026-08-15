using System;
using System.Collections.Generic;
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

    public sealed class PlanningGraph
    {
        public IReadOnlyList<PlanningNode> Nodes { get; }
        internal PlanningGraph(PlanningNode[] nodes) => Nodes = nodes ?? Array.Empty<PlanningNode>();
    }

    /// <summary>
    /// First compilation boundary between authored campaign data and a future spatial
    /// orchestration adapter (for example LayerProcGen). This compiler deliberately
    /// produces engine-agnostic dependency nodes; it does not invoke generation.
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

                // Story conditions/effects are runtime state dependencies, not generation
                // dependencies. Keeping them out of this graph prevents story sequencing
                // from introducing cycles into spatial realization.

                nodes.Add(new PlanningNode(
                    NodeId("cutscene", cutscene.Ref.Id),
                    PlanningNodeKind.Cutscene,
                    dependencies.ToArray()));
            }

            return new PlanningGraph(nodes.ToArray());
        }

        private static string NodeId(string kind, string id) => $"{kind}:{id}";

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }
    }
}
