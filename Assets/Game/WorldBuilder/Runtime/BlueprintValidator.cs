using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    public enum BlueprintDiagnosticSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class BlueprintDiagnostic
    {
        public string Code { get; }
        public BlueprintDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public BlueprintDiagnostic(string code, BlueprintDiagnosticSeverity severity, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public override string ToString() => $"{Severity} {Code}: {Message}";
    }

    public sealed class BlueprintValidationResult
    {
        public IReadOnlyList<BlueprintDiagnostic> Diagnostics { get; }
        public bool IsValid { get; }

        internal BlueprintValidationResult(BlueprintDiagnostic[] diagnostics)
        {
            Diagnostics = diagnostics ?? Array.Empty<BlueprintDiagnostic>();
            IsValid = true;
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Severity == BlueprintDiagnosticSeverity.Error)
                {
                    IsValid = false;
                    break;
                }
            }
        }
    }

    public static class BlueprintValidator
    {
        public static BlueprintValidationResult Validate(CampaignBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));

            var diagnostics = new List<BlueprintDiagnostic>();
            var regions = CollectIds(blueprint.Hierarchy.Regions, spec => spec.Ref.Id, "region", diagnostics);
            var routes = CollectIds(blueprint.Hierarchy.Routes, spec => spec.Ref.Id, "route", diagnostics);
            var settlements = CollectIds(blueprint.Hierarchy.Settlements, spec => spec.Ref.Id, "settlement", diagnostics);
            var sites = CollectIds(blueprint.Sites, spec => spec.Ref.Id, "site", diagnostics);
            var npcs = CollectIds(blueprint.Npcs, spec => spec.Ref.Id, "NPC", diagnostics);
            var objectives = CollectIds(blueprint.Objectives, spec => spec.Ref.Id, "objective", diagnostics);
            var cutscenes = CollectIds(blueprint.Cutscenes, spec => spec.Ref.Id, "cutscene", diagnostics);
            CollectIds(blueprint.StoryRules, spec => spec.Ref.Id, "story rule", diagnostics);
            var lootTables = CollectIds(blueprint.LootTables, spec => spec.Ref.Id, "loot table", diagnostics);
            CollectIds(blueprint.SecretPolicies, spec => spec.Ref.Id, "secret policy", diagnostics);
            CollectIds(blueprint.RequiredSecrets, spec => spec.Ref.Id, "required secret", diagnostics);

            ValidateHierarchy(blueprint, regions, routes, settlements, sites, diagnostics);

            for (var i = 0; i < blueprint.Sites.Count; i++)
            {
                var site = blueprint.Sites[i];
                if (site.Archetype == SiteArchetype.Unspecified)
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB1001", BlueprintDiagnosticSeverity.Warning,
                        $"Site '{site.Ref}' has no concrete archetype yet. It can participate in story constraints, but cannot be physically realized until a site archetype or selector is supplied."));
                }
            }

            for (var i = 0; i < blueprint.Npcs.Count; i++)
            {
                var npc = blueprint.Npcs[i];
                RequireExists(sites, npc.Site.Id, "WB2001", $"NPC '{npc.Ref}' is placed at unknown site '{npc.Site}'.", diagnostics);
            }

            for (var i = 0; i < blueprint.SpatialConstraints.Count; i++)
            {
                var constraint = blueprint.SpatialConstraints[i];
                RequireExists(sites, constraint.Subject.Id, "WB2002", $"Spatial constraint subject '{constraint.Subject}' does not exist.", diagnostics);
                RequireExists(sites, constraint.Target.Id, "WB2003", $"Spatial constraint target '{constraint.Target}' does not exist.", diagnostics);
            }

            for (var i = 0; i < blueprint.Objectives.Count; i++)
            {
                var objective = blueprint.Objectives[i];
                RequireExists(sites, objective.Target.Id, "WB2101", $"Objective '{objective.Ref}' targets unknown site '{objective.Target}'.", diagnostics);
                if (objective.Completion is InteractWithNpcTriggerSpec interact)
                    RequireExists(npcs, interact.Npc.Id, "WB2102", $"Objective '{objective.Ref}' completes by interacting with unknown NPC '{interact.Npc}'.", diagnostics);
            }

            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                var cutscene = blueprint.Cutscenes[i];
                var siteExists = sites.Contains(cutscene.Site.Id);
                RequireExists(sites, cutscene.Site.Id, "WB2201", $"Cutscene '{cutscene.Ref}' is bound to unknown site '{cutscene.Site}'.", diagnostics);

                if (siteExists && cutscene.StageRequirements.Count > 0 && !SiteHasCapability(blueprint, cutscene.Site, SiteCapabilityKind.CutsceneStage))
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB2207", BlueprintDiagnosticSeverity.Error,
                        $"Cutscene '{cutscene.Ref}' requires {cutscene.StageRequirements.Count} semantic stage point(s), but site '{cutscene.Site}' does not declare CutsceneStage capability."));
                }

                ValidateActorBindings(cutscene, npcs, diagnostics);
            }

            for (var i = 0; i < blueprint.StoryRules.Count; i++)
            {
                StoryRuleSpec rule = blueprint.StoryRules[i];
                ValidateRuleTrigger(rule.Ref, rule.Trigger, npcs, cutscenes, diagnostics);
                for (var j = 0; j < rule.Conditions.Count; j++)
                    ValidateRuleCondition(rule.Ref, rule.Conditions[j], objectives, cutscenes, diagnostics);
                for (var j = 0; j < rule.Effects.Count; j++)
                    ValidateRuleEffect(rule.Ref, rule.Effects[j], objectives, cutscenes, diagnostics);
            }

            for (var i = 0; i < blueprint.SecretPolicies.Count; i++)
            {
                var policy = blueprint.SecretPolicies[i];
                RequireExists(lootTables, policy.Reward.Id, "WB2301", $"Secret policy '{policy.Ref}' references unknown loot table '{policy.Reward}'.", diagnostics);
                if (!policy.RequiresHiddenSpace)
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB1301", BlueprintDiagnosticSeverity.Warning,
                        $"Secret policy '{policy.Ref}' does not require hidden space; false-wall secrets may not be topologically hidden."));
                }
            }

            for (var i = 0; i < blueprint.RequiredSecrets.Count; i++)
            {
                RequiredSecretSpec secret = blueprint.RequiredSecrets[i];
                bool siteExists = sites.Contains(secret.Site.Id);
                RequireExists(sites, secret.Site.Id, "WB2310", $"Required secret '{secret.Ref}' targets unknown site '{secret.Site}'.", diagnostics);
                RequireExists(lootTables, secret.Reward.Id, "WB2311", $"Required secret '{secret.Ref}' references unknown loot table '{secret.Reward}'.", diagnostics);

                if (siteExists && !SiteHasCapability(blueprint, secret.Site, SiteCapabilityKind.SecretCandidateHost))
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB2312", BlueprintDiagnosticSeverity.Error,
                        $"Required secret '{secret.Ref}' targets site '{secret.Site}', but that site does not declare SecretCandidateHost capability."));
                }

                if (secret.Entrance == SecretEntranceType.DestroyableFalseWall && !secret.RequiresHiddenSpace)
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB2313", BlueprintDiagnosticSeverity.Error,
                        $"Required secret '{secret.Ref}' uses a destroyable false wall but does not require topologically hidden space."));
                }
            }

            return new BlueprintValidationResult(diagnostics.ToArray());
        }

        private static void ValidateHierarchy(
            CampaignBlueprint blueprint,
            HashSet<string> regions,
            HashSet<string> routes,
            HashSet<string> settlements,
            HashSet<string> sites,
            List<BlueprintDiagnostic> diagnostics)
        {
            var routeRegions = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < blueprint.Hierarchy.Routes.Count; i++)
            {
                RouteSpec route = blueprint.Hierarchy.Routes[i];
                RequireExists(regions, route.Region.Id, "WB2401", $"Route '{route.Ref}' belongs to unknown region '{route.Region}'.", diagnostics);
                routeRegions[route.Ref.Id] = route.Region.Id;
            }

            var settlementRegions = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < blueprint.Hierarchy.Settlements.Count; i++)
            {
                SettlementSpec settlement = blueprint.Hierarchy.Settlements[i];
                RequireExists(regions, settlement.Region.Id, "WB2402", $"Settlement '{settlement.Ref}' belongs to unknown region '{settlement.Region}'.", diagnostics);
                settlementRegions[settlement.Ref.Id] = settlement.Region.Id;
                if (settlement.Archetype == SettlementArchetype.Unspecified)
                    diagnostics.Add(new BlueprintDiagnostic("WB1401", BlueprintDiagnosticSeverity.Warning,
                        $"Settlement '{settlement.Ref}' has no archetype. Placement can be planned, but settlement generation does not yet know what class of settlement to build."));
            }

            var accessKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < blueprint.Hierarchy.RouteAccess.Count; i++)
            {
                SettlementRouteAccessSpec access = blueprint.Hierarchy.RouteAccess[i];
                RequireExists(settlements, access.Settlement.Id, "WB2403", $"Route access references unknown settlement '{access.Settlement}'.", diagnostics);
                RequireExists(routes, access.Route.Id, "WB2404", $"Route access references unknown route '{access.Route}'.", diagnostics);
                string key = access.Settlement.Id + "->" + access.Route.Id;
                if (!accessKeys.Add(key))
                    diagnostics.Add(new BlueprintDiagnostic("WB2405", BlueprintDiagnosticSeverity.Error,
                        $"Settlement '{access.Settlement}' declares route access to '{access.Route}' more than once."));
                if (settlementRegions.TryGetValue(access.Settlement.Id, out string settlementRegion)
                    && routeRegions.TryGetValue(access.Route.Id, out string routeRegion)
                    && !string.Equals(settlementRegion, routeRegion, StringComparison.Ordinal))
                    diagnostics.Add(new BlueprintDiagnostic("WB2406", BlueprintDiagnosticSeverity.Error,
                        $"Settlement '{access.Settlement}' and connected route '{access.Route}' belong to different regions."));
            }

            var placedSites = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < blueprint.Hierarchy.SitePlacements.Count; i++)
            {
                SitePlacementSpec placement = blueprint.Hierarchy.SitePlacements[i];
                RequireExists(sites, placement.Site.Id, "WB2410", $"Site placement references unknown site '{placement.Site}'.", diagnostics);
                if (!placedSites.Add(placement.Site.Id))
                    diagnostics.Add(new BlueprintDiagnostic("WB2411", BlueprintDiagnosticSeverity.Error,
                        $"Site '{placement.Site}' has more than one spatial owner."));
                if (placement.Kind == SitePlacementKind.Region)
                    RequireExists(regions, placement.Region.Id, "WB2412", $"Site '{placement.Site}' is assigned to unknown region '{placement.Region}'.", diagnostics);
                else if (placement.Kind == SitePlacementKind.Settlement)
                    RequireExists(settlements, placement.Settlement.Id, "WB2413", $"Site '{placement.Site}' is assigned to unknown settlement '{placement.Settlement}'.", diagnostics);
            }

            for (var i = 0; i < blueprint.Sites.Count; i++)
            {
                if (placedSites.Contains(blueprint.Sites[i].Ref.Id)) continue;
                diagnostics.Add(new BlueprintDiagnostic("WB1402", BlueprintDiagnosticSeverity.Warning,
                    $"Site '{blueprint.Sites[i].Ref}' has no region or settlement owner. This is allowed during migration but cannot be spatially placed by the hierarchical planner."));
            }
        }

        private static void ValidateActorBindings(CutsceneSpec cutscene, HashSet<string> npcs, List<BlueprintDiagnostic> diagnostics)
        {
            var required = new HashSet<CutsceneActorId>(cutscene.Definition.RequiredActors);
            var bound = new HashSet<CutsceneActorId>();
            for (var i = 0; i < cutscene.ActorBindings.Count; i++)
            {
                var binding = cutscene.ActorBindings[i];
                if (!bound.Add(binding.Actor))
                    diagnostics.Add(new BlueprintDiagnostic("WB2208", BlueprintDiagnosticSeverity.Error,
                        $"Cutscene '{cutscene.Ref}' binds actor '{binding.Actor}' more than once."));
                if (!required.Contains(binding.Actor))
                    diagnostics.Add(new BlueprintDiagnostic("WB2209", BlueprintDiagnosticSeverity.Error,
                        $"Cutscene '{cutscene.Ref}' binds actor '{binding.Actor}', but that actor is not used by the cutscene definition."));
                if (binding.Target.Kind == CutsceneActorTargetKind.Npc)
                    RequireExists(npcs, binding.Target.Npc.Id, "WB2210",
                        $"Cutscene '{cutscene.Ref}' binds actor '{binding.Actor}' to unknown NPC '{binding.Target.Npc}'.", diagnostics);
            }
            foreach (CutsceneActorId actor in required)
                if (!bound.Contains(actor))
                    diagnostics.Add(new BlueprintDiagnostic("WB2211", BlueprintDiagnosticSeverity.Error,
                        $"Cutscene '{cutscene.Ref}' requires actor '{actor}', but WorldBuilder has no actor binding for it."));
        }

        private static bool SiteHasCapability(CampaignBlueprint blueprint, SiteRef siteRef, SiteCapabilityKind capability)
        {
            for (var i = 0; i < blueprint.Sites.Count; i++)
            {
                var site = blueprint.Sites[i];
                if (!site.Ref.Equals(siteRef)) continue;
                for (var j = 0; j < site.Capabilities.Count; j++) if (site.Capabilities[j].Kind == capability) return true;
                return false;
            }
            return false;
        }

        private static HashSet<string> CollectIds<T>(IReadOnlyList<T> items, Func<T, string> selectId, string label, List<BlueprintDiagnostic> diagnostics)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var id = selectId(items[i]);
                if (!ids.Add(id)) diagnostics.Add(new BlueprintDiagnostic("WB0001", BlueprintDiagnosticSeverity.Error, $"Duplicate {label} id '{id}'."));
            }
            return ids;
        }

        private static void ValidateRuleTrigger(
            StoryRuleRef rule,
            IStoryTriggerSpec trigger,
            HashSet<string> npcs,
            HashSet<string> cutscenes,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (trigger is InteractWithNpcTriggerSpec interact)
                RequireExists(npcs, interact.Npc.Id, "WB2501", $"Story rule '{rule}' is triggered by unknown NPC '{interact.Npc}'.", diagnostics);
            else if (trigger is CutsceneCompletedTriggerSpec completed)
                RequireExists(cutscenes, completed.Cutscene.Id, "WB2502", $"Story rule '{rule}' is triggered by completion of unknown cutscene '{completed.Cutscene}'.", diagnostics);
        }

        private static void ValidateRuleCondition(
            StoryRuleRef rule,
            IStoryConditionSpec condition,
            HashSet<string> objectives,
            HashSet<string> cutscenes,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (condition is ObjectiveActiveConditionSpec active)
                RequireExists(objectives, active.Objective.Id, "WB2503", $"Story rule '{rule}' depends on unknown objective '{active.Objective}'.", diagnostics);
            else if (condition is CutsceneNotCompletedConditionSpec notCompleted)
                RequireExists(cutscenes, notCompleted.Cutscene.Id, "WB2504", $"Story rule '{rule}' depends on unknown cutscene '{notCompleted.Cutscene}'.", diagnostics);
        }

        private static void ValidateRuleEffect(
            StoryRuleRef rule,
            IStoryEffectSpec effect,
            HashSet<string> objectives,
            HashSet<string> cutscenes,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (effect is StartObjectiveEffectSpec start)
                RequireExists(objectives, start.Objective.Id, "WB2505", $"Story rule '{rule}' starts unknown objective '{start.Objective}'.", diagnostics);
            else if (effect is PlayCutsceneEffectSpec play)
                RequireExists(cutscenes, play.Cutscene.Id, "WB2506", $"Story rule '{rule}' plays unknown cutscene '{play.Cutscene}'.", diagnostics);
        }

        private static void RequireExists(HashSet<string> ids, string id, string code, string message, List<BlueprintDiagnostic> diagnostics)
        {
            if (!ids.Contains(id)) diagnostics.Add(new BlueprintDiagnostic(code, BlueprintDiagnosticSeverity.Error, message));
        }
    }
}
