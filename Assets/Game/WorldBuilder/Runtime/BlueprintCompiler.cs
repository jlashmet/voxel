using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// First compilation boundary between authored campaign data and spatial orchestration.
    /// The compiler produces exposed Api planning contracts; generation adapters never need a
    /// reference to Game.WorldBuilder.Runtime.
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

            for (var i = 0; i < blueprint.Hierarchy.Regions.Count; i++)
            {
                RegionSpec region = blueprint.Hierarchy.Regions[i];
                nodes.Add(new PlanningNode(
                    NodeId("region", region.Ref.Id),
                    PlanningNodeKind.Region,
                    Array.Empty<string>()));
            }

            for (var i = 0; i < blueprint.Hierarchy.Routes.Count; i++)
            {
                RouteSpec route = blueprint.Hierarchy.Routes[i];
                nodes.Add(new PlanningNode(
                    NodeId("route", route.Ref.Id),
                    PlanningNodeKind.Route,
                    new[] { NodeId("region", route.Region.Id) }));
            }

            for (var i = 0; i < blueprint.Hierarchy.Settlements.Count; i++)
            {
                SettlementSpec settlement = blueprint.Hierarchy.Settlements[i];
                var dependencies = new List<string>
                {
                    NodeId("region", settlement.Region.Id)
                };

                for (var j = 0; j < blueprint.Hierarchy.RouteAccess.Count; j++)
                {
                    SettlementRouteAccessSpec access = blueprint.Hierarchy.RouteAccess[j];
                    if (access.Settlement.Equals(settlement.Ref))
                        AddUnique(dependencies, NodeId("route", access.Route.Id));
                }

                nodes.Add(new PlanningNode(
                    NodeId("settlement", settlement.Ref.Id),
                    PlanningNodeKind.Settlement,
                    dependencies.ToArray()));
            }

            for (var i = 0; i < blueprint.Sites.Count; i++)
            {
                SiteSpec site = blueprint.Sites[i];
                var dependencies = new List<string>();
                AddSiteOwnerDependency(blueprint.Hierarchy, site.Ref, dependencies);
                nodes.Add(new PlanningNode(
                    NodeId("site", site.Ref.Id),
                    PlanningNodeKind.Site,
                    dependencies.ToArray()));
            }

            for (var i = 0; i < blueprint.LootTables.Count; i++)
                nodes.Add(new PlanningNode(
                    NodeId("loot", blueprint.LootTables[i].Ref.Id),
                    PlanningNodeKind.LootTable,
                    Array.Empty<string>()));

            for (var i = 0; i < blueprint.Npcs.Count; i++)
            {
                NpcSpec npc = blueprint.Npcs[i];
                nodes.Add(new PlanningNode(
                    NodeId("npc", npc.Ref.Id),
                    PlanningNodeKind.Npc,
                    new[] { NodeId("site", npc.Site.Id) }));
            }

            for (var i = 0; i < blueprint.SecretPolicies.Count; i++)
            {
                SecretPolicySpec policy = blueprint.SecretPolicies[i];
                nodes.Add(new PlanningNode(
                    NodeId("secret-policy", policy.Ref.Id),
                    PlanningNodeKind.SecretPolicy,
                    new[] { NodeId("loot", policy.Reward.Id) }));
            }

            for (var i = 0; i < blueprint.Objectives.Count; i++)
            {
                ObjectiveSpec objective = blueprint.Objectives[i];
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
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                var dependencies = new List<string> { NodeId("site", cutscene.Site.Id) };

                if (cutscene.Trigger is InteractWithNpcTriggerSpec interact)
                    AddUnique(dependencies, NodeId("npc", interact.Npc.Id));

                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind == CutsceneActorTargetKind.Npc)
                        AddUnique(dependencies, NodeId("npc", binding.Target.Npc.Id));
                }

                nodes.Add(new PlanningNode(
                    NodeId("cutscene", cutscene.Ref.Id),
                    PlanningNodeKind.Cutscene,
                    dependencies.ToArray()));

                if (cutscene.Definition.StageRequirements.Count > 0)
                {
                    var requirements = new CutsceneStagePointRequirement[cutscene.Definition.StageRequirements.Count];
                    for (var j = 0; j < requirements.Length; j++)
                        requirements[j] = cutscene.Definition.StageRequirements[j];
                    stagePlans.Add(new CutsceneStagePlan(
                        cutscene.Ref,
                        cutscene.Definition,
                        cutscene.Site,
                        requirements));
                }
            }

            return new PlanningGraph(nodes.ToArray(), stagePlans.ToArray());
        }

        private static void AddSiteOwnerDependency(
            WorldHierarchyBlueprint hierarchy,
            SiteRef site,
            List<string> dependencies)
        {
            for (var i = 0; i < hierarchy.SitePlacements.Count; i++)
            {
                SitePlacementSpec placement = hierarchy.SitePlacements[i];
                if (!placement.Site.Equals(site)) continue;

                if (placement.Kind == SitePlacementKind.Region)
                    AddUnique(dependencies, NodeId("region", placement.Region.Id));
                else if (placement.Kind == SitePlacementKind.Settlement)
                    AddUnique(dependencies, NodeId("settlement", placement.Settlement.Id));
            }
        }

        private static string NodeId(string kind, string id) => kind + ":" + id;

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }
    }
}
