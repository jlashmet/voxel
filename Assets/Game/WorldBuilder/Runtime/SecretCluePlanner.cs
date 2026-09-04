using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Resolves authored clue recipes only after the canonical site, NPC and secret planners have
    /// produced authoritative assignments. This planner never chooses a hidden-space candidate.
    /// </summary>
    public static class SecretCluePlanner
    {
        private readonly struct SourceCandidate
        {
            public SecretClueSourceKind Kind { get; }
            public SiteRef Role { get; }
            public NpcRef Npc { get; }
            public ResolvedSiteId Site { get; }
            public string StableKey { get; }

            public SourceCandidate(
                SecretClueSourceKind kind,
                SiteRef role,
                NpcRef npc,
                ResolvedSiteId site,
                string stableKey)
            {
                Kind = kind;
                Role = role;
                Npc = npc;
                Site = site;
                StableKey = stableKey;
            }
        }

        public static SecretCluePlanningResult Resolve(
            int worldSeed,
            IReadOnlyList<SecretClueSpec> specs,
            IReadOnlyList<ResolvedSecretPlan> secretPlans,
            IReadOnlyList<SiteRoleBinding> siteBindings,
            IReadOnlyList<NpcSiteAssignment> npcAssignments)
        {
            specs = specs ?? Array.Empty<SecretClueSpec>();
            secretPlans = secretPlans ?? Array.Empty<ResolvedSecretPlan>();
            siteBindings = siteBindings ?? Array.Empty<SiteRoleBinding>();
            npcAssignments = npcAssignments ?? Array.Empty<NpcSiteAssignment>();

            var diagnostics = new List<SecretClueDiagnostic>();
            var resolved = new List<ResolvedSecretCluePlan>();
            var clueIds = new HashSet<string>(StringComparer.Ordinal);
            var stages = new HashSet<string>(StringComparer.Ordinal);

            var ordered = new List<SecretClueSpec>(specs.Count);
            for (var i = 0; i < specs.Count; i++)
                if (specs[i] != null)
                    ordered.Add(specs[i]);
            ordered.Sort(CompareSpecs);

            for (var i = 0; i < ordered.Count; i++)
            {
                SecretClueSpec spec = ordered[i];
                if (!clueIds.Add(spec.Id.Id))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-clue/duplicate-id",
                        SecretClueDiagnosticKind.DuplicateClueId,
                        spec,
                        "Clue id '" + spec.Id + "' is authored more than once."));
                    continue;
                }

                string stageKey = spec.Secret.Id + "\n" + spec.Stage;
                if (!stages.Add(stageKey))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-clue/duplicate-stage",
                        SecretClueDiagnosticKind.DuplicateStage,
                        spec,
                        "Secret '" + spec.Secret + "' has more than one clue at stage " + spec.Stage + "."));
                    continue;
                }

                ResolvedSecretPlan secretPlan;
                if (!TryFindRequiredSecretPlan(secretPlans, spec.Secret, out secretPlan))
                {
                    if (spec.Requirement == SecretClueRequirement.Required)
                    {
                        diagnostics.Add(Diagnostic(
                            "secret-clue/missing-secret",
                            SecretClueDiagnosticKind.MissingResolvedSecret,
                            spec,
                            "Required clue cannot resolve because secret '" + spec.Secret + "' has no authoritative resolved plan."));
                    }
                    continue;
                }

                SiteRef targetRole = spec.HasTargetSite ? spec.TargetSite : secretPlan.Site;
                ResolvedSiteId targetSite;
                if (!TryFindSite(siteBindings, targetRole, out targetSite))
                {
                    if (spec.Requirement == SecretClueRequirement.Required)
                    {
                        diagnostics.Add(Diagnostic(
                            "secret-clue/missing-target-site",
                            SecretClueDiagnosticKind.MissingTargetSite,
                            spec,
                            "Required clue target site role '" + targetRole + "' was not resolved by WorldBuilder."));
                    }
                    continue;
                }

                var candidates = new List<SourceCandidate>(spec.Sources.Count);
                bool authoredRumorSource = false;
                for (var sourceIndex = 0; sourceIndex < spec.Sources.Count; sourceIndex++)
                {
                    SecretClueSourceSpec source = spec.Sources[sourceIndex];
                    if (source.Kind == SecretClueSourceKind.Site)
                    {
                        ResolvedSiteId site;
                        if (TryFindSite(siteBindings, source.Site, out site))
                            candidates.Add(new SourceCandidate(
                                SecretClueSourceKind.Site,
                                source.Site,
                                default,
                                site,
                                "site:" + source.Site.Id + ":" + site.Value));
                        continue;
                    }

                    if (source.Kind == SecretClueSourceKind.Npc)
                    {
                        authoredRumorSource = true;
                        NpcSiteAssignment assignment;
                        if (TryFindNpc(npcAssignments, source.Npc, out assignment) &&
                            (spec.Kind != SecretClueKind.Rumor || assignment.RequiresConversation))
                        {
                            candidates.Add(new SourceCandidate(
                                SecretClueSourceKind.Npc,
                                assignment.SiteRole,
                                assignment.Npc,
                                assignment.Site,
                                "npc:" + assignment.Npc.Id + ":" + assignment.Site.Value));
                        }
                    }
                }

                if (spec.Kind == SecretClueKind.Rumor && !authoredRumorSource)
                {
                    diagnostics.Add(Diagnostic(
                        "secret-clue/rumor-requires-npc",
                        SecretClueDiagnosticKind.InvalidRumorSource,
                        spec,
                        "Rumor clue '" + spec.Id + "' requires at least one NPC source."));
                    continue;
                }

                if (candidates.Count == 0)
                {
                    if (spec.Requirement == SecretClueRequirement.Required)
                    {
                        diagnostics.Add(Diagnostic(
                            "secret-clue/missing-required-source",
                            SecretClueDiagnosticKind.MissingRequiredSource,
                            spec,
                            "Required clue '" + spec.Id + "' has no source resolved by authoritative site/NPC planning."));
                    }
                    continue;
                }

                candidates.Sort((a, b) => string.CompareOrdinal(a.StableKey, b.StableKey));
                uint hash = StableHash(worldSeed, spec.Secret.Id, spec.Id.Id, spec.Stage);
                SourceCandidate selected = candidates[(int)(hash % (uint)candidates.Count)];
                string memoryTopic = string.IsNullOrWhiteSpace(spec.MemoryTopic)
                    ? SecretClues.MemoryTopic(spec.Secret)
                    : spec.MemoryTopic;

                resolved.Add(new ResolvedSecretCluePlan(
                    spec.Id,
                    spec.Secret,
                    spec.Stage,
                    spec.Kind,
                    selected.Kind,
                    selected.Role,
                    selected.Npc,
                    selected.Site,
                    targetRole,
                    targetSite,
                    secretPlan.Candidate,
                    secretPlan.EntranceId,
                    spec.ContentKey,
                    memoryTopic));
            }

            resolved.Sort(CompareResolved);
            return new SecretCluePlanningResult(resolved.ToArray(), diagnostics.ToArray());
        }

        private static int CompareSpecs(SecretClueSpec a, SecretClueSpec b)
        {
            int secret = string.CompareOrdinal(a.Secret.Id, b.Secret.Id);
            if (secret != 0) return secret;
            int stage = a.Stage.CompareTo(b.Stage);
            if (stage != 0) return stage;
            return string.CompareOrdinal(a.Id.Id, b.Id.Id);
        }

        private static int CompareResolved(ResolvedSecretCluePlan a, ResolvedSecretCluePlan b)
        {
            int secret = string.CompareOrdinal(a.Secret.Id, b.Secret.Id);
            if (secret != 0) return secret;
            int stage = a.Stage.CompareTo(b.Stage);
            if (stage != 0) return stage;
            return string.CompareOrdinal(a.Id.Id, b.Id.Id);
        }

        private static bool TryFindRequiredSecretPlan(
            IReadOnlyList<ResolvedSecretPlan> plans,
            SecretRef secret,
            out ResolvedSecretPlan match)
        {
            for (var i = 0; i < plans.Count; i++)
            {
                ResolvedSecretPlan plan = plans[i];
                if (plan != null &&
                    plan.SourceKind == SecretResolutionSourceKind.RequiredSecret &&
                    plan.RequiredSecret.Equals(secret))
                {
                    match = plan;
                    return true;
                }
            }
            match = null;
            return false;
        }

        private static bool TryFindSite(
            IReadOnlyList<SiteRoleBinding> bindings,
            SiteRef role,
            out ResolvedSiteId site)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                SiteRoleBinding binding = bindings[i];
                if (binding != null && binding.Role.Equals(role))
                {
                    site = binding.Site;
                    return true;
                }
            }
            site = default;
            return false;
        }

        private static bool TryFindNpc(
            IReadOnlyList<NpcSiteAssignment> assignments,
            NpcRef npc,
            out NpcSiteAssignment match)
        {
            for (var i = 0; i < assignments.Count; i++)
            {
                NpcSiteAssignment assignment = assignments[i];
                if (assignment != null && assignment.Npc.Equals(npc))
                {
                    match = assignment;
                    return true;
                }
            }
            match = null;
            return false;
        }

        private static SecretClueDiagnostic Diagnostic(
            string code,
            SecretClueDiagnosticKind kind,
            SecretClueSpec spec,
            string message) =>
            new SecretClueDiagnostic(code, kind, spec.Secret, spec.Id, message);

        private static uint StableHash(int worldSeed, string secret, string clue, int stage)
        {
            unchecked
            {
                uint hash = 2166136261u;
                HashInt(ref hash, worldSeed);
                HashString(ref hash, secret);
                HashString(ref hash, clue);
                HashInt(ref hash, stage);
                return hash;
            }
        }

        private static void HashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value; hash *= 16777619u;
                hash ^= (byte)(value >> 8); hash *= 16777619u;
                hash ^= (byte)(value >> 16); hash *= 16777619u;
                hash ^= (byte)(value >> 24); hash *= 16777619u;
            }
        }

        private static void HashString(ref uint hash, string value)
        {
            unchecked
            {
                value = value ?? string.Empty;
                for (var i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    hash ^= (byte)c; hash *= 16777619u;
                    hash ^= (byte)(c >> 8); hash *= 16777619u;
                }
            }
        }
    }
}
