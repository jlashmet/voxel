using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministically binds authored SiteRef roles to generated site candidates.
    /// Roles may share a generated site unless an explicit DifferentSite constraint forbids it.
    /// Story/runtime state is intentionally absent from this solver.
    /// </summary>
    public static class SiteRoleResolver
    {
        public static SiteResolutionResult Resolve(
            PlanningGraph graph,
            ISiteCandidateFacts facts)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            SiteRolePlan[] roles = CopyAndSortRoles(graph.SiteRoles);
            SiteCandidate[] candidates = CopyAndSortCandidates(facts.Candidates);

            var candidatesByRole = new Dictionary<SiteRef, SiteCandidate[]>();
            for (var i = 0; i < roles.Length; i++)
            {
                SiteRolePlan role = roles[i];
                SiteCandidate[] unaryMatches;
                SiteResolutionDiagnostic diagnostic;
                if (!TryBuildUnaryCandidateSet(
                        graph,
                        facts,
                        role,
                        candidates,
                        out unaryMatches,
                        out diagnostic))
                {
                    return Failure(diagnostic);
                }

                candidatesByRole.Add(role.Role, unaryMatches);
            }

            var assignments = new Dictionary<SiteRef, SiteCandidate>();
            SiteResolutionDiagnostic firstRelationalFailure = null;
            if (!TryAssign(
                    graph,
                    facts,
                    roles,
                    candidatesByRole,
                    assignments,
                    0,
                    ref firstRelationalFailure))
            {
                return Failure(firstRelationalFailure ?? new SiteResolutionDiagnostic(
                    "WB3001",
                    SiteResolutionDiagnosticKind.NoCandidateForRole,
                    roles.Length > 0 ? roles[0].Role : default,
                    default,
                    "No complete assignment satisfies all authored site-role constraints."));
            }

            var bindings = new SiteRoleBinding[roles.Length];
            for (var i = 0; i < roles.Length; i++)
            {
                SiteRolePlan role = roles[i];
                bindings[i] = new SiteRoleBinding(role.Role, assignments[role.Role].Id);
            }

            return new SiteResolutionResult(bindings, Array.Empty<SiteResolutionDiagnostic>());
        }

        private static bool TryBuildUnaryCandidateSet(
            PlanningGraph graph,
            ISiteCandidateFacts facts,
            SiteRolePlan role,
            SiteCandidate[] candidates,
            out SiteCandidate[] matches,
            out SiteResolutionDiagnostic diagnostic)
        {
            if (candidates.Length == 0)
            {
                matches = Array.Empty<SiteCandidate>();
                diagnostic = new SiteResolutionDiagnostic(
                    "WB3001",
                    SiteResolutionDiagnosticKind.NoCandidateForRole,
                    role.Role,
                    default,
                    $"Site role '{role.Role}' cannot resolve because the generated world exposes no site candidates.");
                return false;
            }

            var stage = new List<SiteCandidate>(candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                SiteCandidate candidate = candidates[i];
                if (role.ResolutionMode != SiteResolutionMode.RequiredArchetype
                    || candidate.Archetype == role.Archetype)
                {
                    stage.Add(candidate);
                }
            }

            if (stage.Count == 0)
            {
                matches = Array.Empty<SiteCandidate>();
                diagnostic = new SiteResolutionDiagnostic(
                    "WB3002",
                    SiteResolutionDiagnosticKind.ArchetypeUnsatisfied,
                    role.Role,
                    default,
                    $"Site role '{role.Role}' requires archetype '{role.Archetype}', but no generated site candidate has that archetype.");
                return false;
            }

            WorldSitePlacementPlan placement;
            if (TryFindPlacement(graph.HierarchyPlan, role.Role, out placement))
            {
                var hierarchyMatches = new List<SiteCandidate>(stage.Count);
                for (var i = 0; i < stage.Count; i++)
                {
                    SiteCandidate candidate = stage[i];
                    bool matchesOwner = placement.Kind == SitePlacementKind.Region
                        ? facts.IsInRegion(candidate.Id, placement.Region)
                        : facts.IsInSettlement(candidate.Id, placement.Settlement);
                    if (matchesOwner) hierarchyMatches.Add(candidate);
                }

                stage = hierarchyMatches;
                if (stage.Count == 0)
                {
                    matches = Array.Empty<SiteCandidate>();
                    string owner = placement.Kind == SitePlacementKind.Region
                        ? $"region '{placement.Region}'"
                        : $"settlement '{placement.Settlement}'";
                    diagnostic = new SiteResolutionDiagnostic(
                        "WB3003",
                        SiteResolutionDiagnosticKind.HierarchyUnsatisfied,
                        role.Role,
                        default,
                        $"Site role '{role.Role}' must resolve inside {owner}, but no generated candidate satisfies that ownership constraint.");
                    return false;
                }
            }

            var capabilityMatches = new List<SiteCandidate>(stage.Count);
            for (var i = 0; i < stage.Count; i++)
            {
                SiteCandidate candidate = stage[i];
                if (SatisfiesCapabilities(candidate, role.Capabilities))
                    capabilityMatches.Add(candidate);
            }

            if (capabilityMatches.Count == 0)
            {
                matches = Array.Empty<SiteCandidate>();
                diagnostic = new SiteResolutionDiagnostic(
                    "WB3004",
                    SiteResolutionDiagnosticKind.CapabilityUnsatisfied,
                    role.Role,
                    default,
                    $"Site role '{role.Role}' has generated candidates in the correct archetype/owner scope, but none satisfy all required capability capacities.");
                return false;
            }

            if (HasCutsceneStagePlan(graph, role.Role))
            {
                var stageFacts = facts as ICutsceneStageCandidateFacts;
                var cutsceneMatches = new List<SiteCandidate>(capabilityMatches.Count);
                if (stageFacts != null)
                {
                    for (var i = 0; i < capabilityMatches.Count; i++)
                    {
                        SiteCandidate candidate = capabilityMatches[i];
                        if (SatisfiesCutsceneStages(graph, stageFacts, role.Role, candidate.Id))
                            cutsceneMatches.Add(candidate);
                    }
                }

                if (cutsceneMatches.Count == 0)
                {
                    matches = Array.Empty<SiteCandidate>();
                    diagnostic = new SiteResolutionDiagnostic(
                        "WB3005",
                        SiteResolutionDiagnosticKind.CapabilityUnsatisfied,
                        role.Role,
                        default,
                        $"Site role '{role.Role}' hosts authored cutscene staging, but no generated candidate exposes a stage envelope large enough for every bound cutscene.");
                    return false;
                }

                capabilityMatches = cutsceneMatches;
            }

            matches = capabilityMatches.ToArray();
            diagnostic = null;
            return true;
        }

        private static bool HasCutsceneStagePlan(PlanningGraph graph, SiteRef role)
        {
            for (var i = 0; i < graph.CutsceneStages.Count; i++)
                if (graph.CutsceneStages[i].Site.Equals(role))
                    return true;
            return false;
        }

        private static bool SatisfiesCutsceneStages(
            PlanningGraph graph,
            ICutsceneStageCandidateFacts facts,
            SiteRef role,
            ResolvedSiteId candidate)
        {
            CutsceneStageEnvelope envelope;
            if (!facts.TryGetCutsceneStageEnvelope(candidate, out envelope))
                return false;

            for (var i = 0; i < graph.CutsceneStages.Count; i++)
            {
                CutsceneStagePlan stage = graph.CutsceneStages[i];
                if (!stage.Site.Equals(role)) continue;
                if (!CutsceneStageFeasibility.CanFit(stage, envelope))
                    return false;
            }

            return true;
        }

        private static bool TryAssign(
            PlanningGraph graph,
            ISiteCandidateFacts facts,
            SiteRolePlan[] roles,
            Dictionary<SiteRef, SiteCandidate[]> candidatesByRole,
            Dictionary<SiteRef, SiteCandidate> assignments,
            int roleIndex,
            ref SiteResolutionDiagnostic firstRelationalFailure)
        {
            if (roleIndex >= roles.Length)
                return true;

            SiteRolePlan role = roles[roleIndex];
            SiteCandidate[] candidates = candidatesByRole[role.Role];
            for (var i = 0; i < candidates.Length; i++)
            {
                assignments[role.Role] = candidates[i];

                SiteResolutionDiagnostic relationFailure;
                if (RelationsSatisfied(graph, facts, assignments, out relationFailure))
                {
                    if (TryAssign(
                            graph,
                            facts,
                            roles,
                            candidatesByRole,
                            assignments,
                            roleIndex + 1,
                            ref firstRelationalFailure))
                    {
                        return true;
                    }
                }
                else if (firstRelationalFailure == null)
                {
                    firstRelationalFailure = relationFailure;
                }

                assignments.Remove(role.Role);
            }

            return false;
        }

        private static bool RelationsSatisfied(
            PlanningGraph graph,
            ISiteCandidateFacts facts,
            Dictionary<SiteRef, SiteCandidate> assignments,
            out SiteResolutionDiagnostic diagnostic)
        {
            for (var i = 0; i < graph.SpatialConstraints.Count; i++)
            {
                SpatialConstraintSpec constraint = graph.SpatialConstraints[i];
                SiteCandidate subject;
                SiteCandidate target;
                if (!assignments.TryGetValue(constraint.Subject, out subject)
                    || !assignments.TryGetValue(constraint.Target, out target))
                {
                    continue;
                }

                if (constraint.Kind == SpatialConstraintKind.DifferentSite)
                {
                    if (subject.Id.Equals(target.Id))
                    {
                        diagnostic = new SiteResolutionDiagnostic(
                            "WB3010",
                            SiteResolutionDiagnosticKind.DifferentSiteUnsatisfied,
                            constraint.Subject,
                            constraint.Target,
                            $"Site roles '{constraint.Subject}' and '{constraint.Target}' must resolve to different generated sites, but both resolve to '{subject.Id}'.");
                        return false;
                    }
                    continue;
                }

                if (constraint.Kind == SpatialConstraintKind.ReachableFrom)
                {
                    if (!facts.IsReachable(subject.Id, target.Id, constraint.Traversal))
                    {
                        diagnostic = new SiteResolutionDiagnostic(
                            "WB3011",
                            SiteResolutionDiagnosticKind.ReachabilityUnsatisfied,
                            constraint.Subject,
                            constraint.Target,
                            $"Site role '{constraint.Subject}' must be reachable from '{constraint.Target}' for traversal profile '{constraint.Traversal}', but generated sites '{subject.Id}' and '{target.Id}' are not connected by a valid path.");
                        return false;
                    }
                    continue;
                }

                if (constraint.Kind == SpatialConstraintKind.DistanceRange)
                {
                    int distance = ResolveDistance(facts, constraint, subject.Id, target.Id);
                    if (distance < constraint.Distance.Minimum || distance > constraint.Distance.Maximum)
                    {
                        diagnostic = new SiteResolutionDiagnostic(
                            "WB3012",
                            SiteResolutionDiagnosticKind.DistanceUnsatisfied,
                            constraint.Subject,
                            constraint.Target,
                            $"Site roles '{constraint.Subject}' and '{constraint.Target}' resolve to '{subject.Id}' and '{target.Id}', whose {constraint.DistanceMetric} distance is {distance}m; required range is {constraint.Distance.Minimum}..{constraint.Distance.Maximum}m.");
                        return false;
                    }
                }
            }

            diagnostic = null;
            return true;
        }

        private static int ResolveDistance(
            ISiteCandidateFacts facts,
            SpatialConstraintSpec constraint,
            ResolvedSiteId subject,
            ResolvedSiteId target)
        {
            switch (constraint.DistanceMetric)
            {
                case SiteDistanceMetric.BoundaryToBoundaryEuclidean:
                    return facts.BoundaryDistanceMetres(subject, target);
                case SiteDistanceMetric.PublicEntranceToPublicEntranceEuclidean:
                    return facts.PublicEntranceDistanceMetres(subject, target);
                case SiteDistanceMetric.TraversalPathLength:
                    return facts.TraversalDistanceMetres(subject, target, constraint.Traversal);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported site distance metric '{constraint.DistanceMetric}'.");
            }
        }

        private static bool SatisfiesCapabilities(
            SiteCandidate candidate,
            IReadOnlyList<SiteCapabilityRequirement> requirements)
        {
            for (var i = 0; i < requirements.Count; i++)
            {
                SiteCapabilityRequirement requirement = requirements[i];
                bool satisfied = false;
                for (var j = 0; j < candidate.Capabilities.Count; j++)
                {
                    SiteCapabilityOffer offer = candidate.Capabilities[j];
                    if (offer.Kind == requirement.Kind
                        && offer.Capacity >= requirement.MinimumCapacity)
                    {
                        satisfied = true;
                        break;
                    }
                }

                if (!satisfied) return false;
            }

            return true;
        }

        private static bool TryFindPlacement(
            WorldHierarchyPlan hierarchy,
            SiteRef role,
            out WorldSitePlacementPlan placement)
        {
            for (var i = 0; i < hierarchy.SitePlacements.Count; i++)
            {
                if (hierarchy.SitePlacements[i].Site.Equals(role))
                {
                    placement = hierarchy.SitePlacements[i];
                    return true;
                }
            }

            placement = null;
            return false;
        }

        private static SiteRolePlan[] CopyAndSortRoles(IReadOnlyList<SiteRolePlan> source)
        {
            var result = new SiteRolePlan[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            Array.Sort(result, (a, b) => StringComparer.Ordinal.Compare(a.Role.Id, b.Role.Id));
            return result;
        }

        private static SiteCandidate[] CopyAndSortCandidates(IReadOnlyList<SiteCandidate> source)
        {
            if (source == null) return Array.Empty<SiteCandidate>();
            var result = new SiteCandidate[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            Array.Sort(result, (a, b) => StringComparer.Ordinal.Compare(a.Id.Value, b.Id.Value));
            return result;
        }

        private static SiteResolutionResult Failure(SiteResolutionDiagnostic diagnostic) =>
            new SiteResolutionResult(
                Array.Empty<SiteRoleBinding>(),
                new[] { diagnostic });
    }
}
