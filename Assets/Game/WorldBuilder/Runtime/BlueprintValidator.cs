using System;
using System.Collections.Generic;
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
            var sites = CollectIds(blueprint.Sites, spec => spec.Ref.Id, "site", diagnostics);
            var npcs = CollectIds(blueprint.Npcs, spec => spec.Ref.Id, "NPC", diagnostics);
            var objectives = CollectIds(blueprint.Objectives, spec => spec.Ref.Id, "objective", diagnostics);
            var cutscenes = CollectIds(blueprint.Cutscenes, spec => spec.Ref.Id, "cutscene", diagnostics);
            var lootTables = CollectIds(blueprint.LootTables, spec => spec.Ref.Id, "loot table", diagnostics);
            CollectIds(blueprint.SecretPolicies, spec => spec.Ref.Id, "secret policy", diagnostics);

            for (var i = 0; i < blueprint.Sites.Count; i++)
            {
                var site = blueprint.Sites[i];
                if (site.Archetype == SiteArchetype.Unspecified)
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB1001",
                        BlueprintDiagnosticSeverity.Warning,
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
                RequireExists(sites, cutscene.Site.Id, "WB2201", $"Cutscene '{cutscene.Ref}' is bound to unknown site '{cutscene.Site}'.", diagnostics);

                ValidateTrigger(cutscene.Ref, cutscene.Trigger, npcs, diagnostics);

                for (var j = 0; j < cutscene.Conditions.Count; j++)
                    ValidateCondition(cutscene.Ref, cutscene.Conditions[j], objectives, cutscenes, diagnostics);

                for (var j = 0; j < cutscene.Effects.Count; j++)
                    ValidateEffect(cutscene.Ref, cutscene.Effects[j], objectives, cutscenes, diagnostics);
            }

            for (var i = 0; i < blueprint.SecretPolicies.Count; i++)
            {
                var policy = blueprint.SecretPolicies[i];
                RequireExists(lootTables, policy.Reward.Id, "WB2301", $"Secret policy '{policy.Ref}' references unknown loot table '{policy.Reward}'.", diagnostics);

                if (!policy.RequiresHiddenSpace)
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB1301",
                        BlueprintDiagnosticSeverity.Warning,
                        $"Secret policy '{policy.Ref}' does not require hidden space; false-wall secrets may not be topologically hidden."));
                }
            }

            return new BlueprintValidationResult(diagnostics.ToArray());
        }

        private static HashSet<string> CollectIds<T>(
            IReadOnlyList<T> items,
            Func<T, string> selectId,
            string label,
            List<BlueprintDiagnostic> diagnostics)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var id = selectId(items[i]);
                if (!ids.Add(id))
                {
                    diagnostics.Add(new BlueprintDiagnostic(
                        "WB0001",
                        BlueprintDiagnosticSeverity.Error,
                        $"Duplicate {label} id '{id}'."));
                }
            }
            return ids;
        }

        private static void ValidateTrigger(
            CutsceneRef cutscene,
            IStoryTriggerSpec trigger,
            HashSet<string> npcs,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (trigger is InteractWithNpcTriggerSpec interact)
                RequireExists(npcs, interact.Npc.Id, "WB2202", $"Cutscene '{cutscene}' is triggered by unknown NPC '{interact.Npc}'.", diagnostics);
        }

        private static void ValidateCondition(
            CutsceneRef cutscene,
            IStoryConditionSpec condition,
            HashSet<string> objectives,
            HashSet<string> cutscenes,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (condition is ObjectiveActiveConditionSpec active)
                RequireExists(objectives, active.Objective.Id, "WB2203", $"Cutscene '{cutscene}' depends on unknown objective '{active.Objective}'.", diagnostics);
            else if (condition is CutsceneNotCompletedConditionSpec notCompleted)
                RequireExists(cutscenes, notCompleted.Cutscene.Id, "WB2204", $"Cutscene '{cutscene}' depends on unknown cutscene '{notCompleted.Cutscene}'.", diagnostics);
        }

        private static void ValidateEffect(
            CutsceneRef cutscene,
            IStoryEffectSpec effect,
            HashSet<string> objectives,
            HashSet<string> cutscenes,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (effect is StartObjectiveEffectSpec start)
                RequireExists(objectives, start.Objective.Id, "WB2205", $"Cutscene '{cutscene}' starts unknown objective '{start.Objective}'.", diagnostics);
            else if (effect is PlayCutsceneEffectSpec play)
                RequireExists(cutscenes, play.Cutscene.Id, "WB2206", $"Cutscene '{cutscene}' plays unknown cutscene '{play.Cutscene}'.", diagnostics);
        }

        private static void RequireExists(
            HashSet<string> ids,
            string id,
            string code,
            string message,
            List<BlueprintDiagnostic> diagnostics)
        {
            if (!ids.Contains(id))
                diagnostics.Add(new BlueprintDiagnostic(code, BlueprintDiagnosticSeverity.Error, message));
        }
    }
}
