using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Plans legal routes and semantic pre-solve clues around an already-resolved secret destination.
    /// It never chooses secret geometry and therefore cannot diverge from SecretPlanner ownership.
    /// </summary>
    public static class SecretDiscoveryPlanner
    {
        private readonly struct ClueCandidate
        {
            public SecretClueAnchorSpec Anchor { get; }
            public ResolvedSiteId Site { get; }
            public SecretClueChannel Channel { get; }
            public uint TieBreak { get; }

            public ClueCandidate(
                SecretClueAnchorSpec anchor,
                ResolvedSiteId site,
                SecretClueChannel channel,
                uint tieBreak)
            {
                Anchor = anchor;
                Site = site;
                Channel = channel;
                TieBreak = tieBreak;
            }
        }

        public static SecretDiscoveryPlanningResult Resolve(
            int worldSeed,
            SecretDiscoverySpec spec,
            IReadOnlyList<ResolvedSecretPlan> secretPlans,
            IReadOnlyList<SiteRoleBinding> siteBindings)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            secretPlans = secretPlans ?? Array.Empty<ResolvedSecretPlan>();
            siteBindings = siteBindings ?? Array.Empty<SiteRoleBinding>();

            var diagnostics = new List<SecretDiscoveryDiagnostic>();
            ResolvedSecretPlan canonical;
            if (!TryFindRequiredSecretPlan(secretPlans, spec.Secret, out canonical))
            {
                diagnostics.Add(Diagnostic(
                    "secret-discovery/missing-secret",
                    SecretDiscoveryDiagnosticKind.MissingResolvedSecret,
                    spec.Secret,
                    spec.Secret.Id,
                    "Secret discovery planning requires the canonical resolved secret destination."));
                return new SecretDiscoveryPlanningResult(null, diagnostics.ToArray());
            }

            PlannedSecretRoute[] routes = ResolveRoutes(spec, diagnostics);
            PlannedSecretClue[] clues = ResolveClues(worldSeed, spec, siteBindings, diagnostics);

            if (diagnostics.Count != 0)
                return new SecretDiscoveryPlanningResult(null, diagnostics.ToArray());

            return new SecretDiscoveryPlanningResult(
                new ResolvedSecretDiscoveryPlan(
                    spec.Secret,
                    spec.Importance,
                    canonical.Candidate,
                    canonical.EntranceId,
                    routes,
                    clues),
                Array.Empty<SecretDiscoveryDiagnostic>());
        }

        private static PlannedSecretRoute[] ResolveRoutes(
            SecretDiscoverySpec spec,
            List<SecretDiscoveryDiagnostic> diagnostics)
        {
            var result = new List<PlannedSecretRoute>(spec.Routes.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < spec.Routes.Count; i++)
            {
                SecretRouteSpec route = spec.Routes[i];
                if (route == null)
                    continue;

                if (!ids.Add(route.Id.Id))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/duplicate-route",
                        SecretDiscoveryDiagnosticKind.DuplicateRouteId,
                        spec.Secret,
                        route.Id.Id,
                        "Route id '" + route.Id + "' is authored more than once."));
                    continue;
                }

                if (!route.Secret.Equals(spec.Secret))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/route-secret-mismatch",
                        SecretDiscoveryDiagnosticKind.RouteSecretMismatch,
                        spec.Secret,
                        route.Id.Id,
                        "Route '" + route.Id + "' points at a different secret identity."));
                    continue;
                }

                ValidateBypassPolicy(spec.Secret, route, diagnostics);
                result.Add(new PlannedSecretRoute(route));
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Id.Id, b.Id.Id));
            return result.ToArray();
        }

        private static void ValidateBypassPolicy(
            SecretRef secret,
            SecretRouteSpec route,
            List<SecretDiscoveryDiagnostic> diagnostics)
        {
            SecretBypassEvidence evidence = route.BypassEvidence;
            if (route.BypassPolicy == SecretBypassPolicy.ProtectedShell)
            {
                if (evidence.HasTrivialUnintendedBypass || evidence.UndesignatedBreakableVoxelCount > 0)
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/protected-shell-bypass",
                        SecretDiscoveryDiagnosticKind.ProtectedShellBypass,
                        secret,
                        route.Id.Id,
                        "Protected route '" + route.Id + "' exposes an unintended voxel bypass."));
                }
                return;
            }

            if (route.BypassPolicy == SecretBypassPolicy.AuthoredBreakablesOnly &&
                (evidence.HasTrivialUnintendedBypass ||
                 evidence.DesignatedBreakableVoxelCount <= 0 ||
                 evidence.UndesignatedBreakableVoxelCount > 0))
            {
                diagnostics.Add(Diagnostic(
                    "secret-discovery/authored-breakable-invalid",
                    SecretDiscoveryDiagnosticKind.AuthoredBreakableInvalid,
                    secret,
                    route.Id.Id,
                    "Authored-breakable route '" + route.Id +
                    " must contain designated breakables without leaking destructibility or trivial bypasses."));
            }
        }

        private static PlannedSecretClue[] ResolveClues(
            int worldSeed,
            SecretDiscoverySpec spec,
            IReadOnlyList<SiteRoleBinding> siteBindings,
            List<SecretDiscoveryDiagnostic> diagnostics)
        {
            int requiredCount = spec.MinimumClueOverride ?? DefaultMinimumClues(spec.Importance);
            bool requireIndependentChannels = spec.Importance == SecretImportance.Major && requiredCount >= 2;
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<ClueCandidate>();

            for (var i = 0; i < spec.ClueAnchors.Count; i++)
            {
                SecretClueAnchorSpec anchor = spec.ClueAnchors[i];
                if (anchor == null)
                    continue;

                if (!anchorIds.Add(anchor.Id.Id))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/duplicate-anchor",
                        SecretDiscoveryDiagnosticKind.DuplicateAnchorId,
                        spec.Secret,
                        anchor.Id.Id,
                        "Clue anchor id '" + anchor.Id + "' is authored more than once."));
                    continue;
                }

                ResolvedSiteId site;
                if (!TryFindSite(siteBindings, anchor.Site, out site))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/missing-anchor-site",
                        SecretDiscoveryDiagnosticKind.MissingAnchorSite,
                        spec.Secret,
                        anchor.Id.Id,
                        "Clue anchor '" + anchor.Id + "' references an unresolved semantic site role."));
                    continue;
                }

                if (anchor.HasRouteDependency && anchor.HasExplainedRoute &&
                    anchor.RouteDependency.Equals(anchor.ExplainedRoute))
                {
                    diagnostics.Add(Diagnostic(
                        "secret-discovery/circular-clue",
                        SecretDiscoveryDiagnosticKind.CircularClueDependency,
                        spec.Secret,
                        anchor.Id.Id,
                        "Clue anchor '" + anchor.Id + "' depends on solving the same route it explains."));
                    continue;
                }

                // Required planning candidates must be observable before solving and may not live inside
                // the hidden volume they are supposed to reveal. Non-observable anchors remain authoring
                // candidates for optional realization but never satisfy readability policy.
                if (!anchor.PreSolveObservable || anchor.HiddenVolumeRelation == SecretHiddenVolumeRelation.Inside)
                    continue;

                for (var channelIndex = 0; channelIndex < anchor.Channels.Count; channelIndex++)
                {
                    SecretClueChannel channel = anchor.Channels[channelIndex];
                    candidates.Add(new ClueCandidate(
                        anchor,
                        site,
                        channel,
                        StableHash(worldSeed, spec.Secret.Id, anchor.Id.Id, (int)channel)));
                }
            }

            candidates.Sort(CompareCandidates);

            if (requiredCount == 0)
                return Array.Empty<PlannedSecretClue>();

            var selected = new List<ClueCandidate>(requiredCount);
            var selectedAnchors = new HashSet<string>(StringComparer.Ordinal);
            var selectedChannels = new HashSet<SecretClueChannel>();

            // First pass maximizes independent channels. It matters for Major secrets and also improves
            // readability of authored overrides without changing the minimum policy.
            for (var i = 0; i < candidates.Count && selected.Count < requiredCount; i++)
            {
                ClueCandidate candidate = candidates[i];
                if (selectedAnchors.Contains(candidate.Anchor.Id.Id) || selectedChannels.Contains(candidate.Channel))
                    continue;
                selected.Add(candidate);
                selectedAnchors.Add(candidate.Anchor.Id.Id);
                selectedChannels.Add(candidate.Channel);
            }

            // Second pass fills any remaining count from distinct anchors even if channels repeat.
            for (var i = 0; i < candidates.Count && selected.Count < requiredCount; i++)
            {
                ClueCandidate candidate = candidates[i];
                if (!selectedAnchors.Add(candidate.Anchor.Id.Id))
                    continue;
                selected.Add(candidate);
                selectedChannels.Add(candidate.Channel);
            }

            if (selected.Count < requiredCount)
            {
                diagnostics.Add(Diagnostic(
                    "secret-discovery/insufficient-observable-clues",
                    SecretDiscoveryDiagnosticKind.InsufficientObservableClues,
                    spec.Secret,
                    spec.Secret.Id,
                    "Secret '" + spec.Secret + "' requires " + requiredCount +
                    " pre-solve observable clue(s), but only " + selected.Count + " compatible anchor(s) resolved."));
                return Array.Empty<PlannedSecretClue>();
            }

            if (requireIndependentChannels && selectedChannels.Count < 2)
            {
                diagnostics.Add(Diagnostic(
                    "secret-discovery/insufficient-channel-diversity",
                    SecretDiscoveryDiagnosticKind.InsufficientChannelDiversity,
                    spec.Secret,
                    spec.Secret.Id,
                    "Major secret '" + spec.Secret + "' requires clues across at least two independent channels."));
                return Array.Empty<PlannedSecretClue>();
            }

            selected.Sort((a, b) => string.CompareOrdinal(a.Anchor.Id.Id, b.Anchor.Id.Id));
            var result = new PlannedSecretClue[selected.Count];
            for (var i = 0; i < selected.Count; i++)
            {
                ClueCandidate candidate = selected[i];
                result[i] = new PlannedSecretClue(
                    new SecretClueId(spec.Secret.Id + "/clue/" + candidate.Anchor.Id.Id),
                    spec.Secret,
                    candidate.Anchor.Id,
                    candidate.Anchor.Site,
                    candidate.Site,
                    candidate.Anchor.Role,
                    candidate.Channel,
                    candidate.Anchor.HasExplainedRoute,
                    candidate.Anchor.ExplainedRoute);
            }
            return result;
        }

        private static int DefaultMinimumClues(SecretImportance importance)
        {
            switch (importance)
            {
                case SecretImportance.Minor: return 0;
                case SecretImportance.Standard: return 1;
                case SecretImportance.Major: return 2;
                default: throw new ArgumentOutOfRangeException(nameof(importance), importance, null);
            }
        }

        private static int CompareCandidates(ClueCandidate a, ClueCandidate b)
        {
            // Prefer exterior/approach/route-adjacent evidence over weaker abstract hints, then use a
            // seed-based stable tie break and semantic IDs. No collection or generation order leaks in.
            int role = RolePriority(a.Anchor.Role).CompareTo(RolePriority(b.Anchor.Role));
            if (role != 0) return role;
            int tie = a.TieBreak.CompareTo(b.TieBreak);
            if (tie != 0) return tie;
            int anchor = string.CompareOrdinal(a.Anchor.Id.Id, b.Anchor.Id.Id);
            if (anchor != 0) return anchor;
            return a.Channel.CompareTo(b.Channel);
        }

        private static int RolePriority(SecretClueAnchorRole role)
        {
            switch (role)
            {
                case SecretClueAnchorRole.ApproachEvidence: return 0;
                case SecretClueAnchorRole.ExteriorEvidence: return 1;
                case SecretClueAnchorRole.RouteAdjacentEvidence: return 2;
                case SecretClueAnchorRole.TraversalHint: return 3;
                case SecretClueAnchorRole.SightlineHint: return 4;
                case SecretClueAnchorRole.AcousticHint: return 5;
                case SecretClueAnchorRole.NarrativeHint: return 6;
                default: return 100;
            }
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

        private static SecretDiscoveryDiagnostic Diagnostic(
            string code,
            SecretDiscoveryDiagnosticKind kind,
            SecretRef secret,
            string subjectId,
            string message) =>
            new SecretDiscoveryDiagnostic(code, kind, secret, subjectId, message);

        private static uint StableHash(int worldSeed, string secret, string anchor, int channel)
        {
            unchecked
            {
                uint hash = 2166136261u;
                HashInt(ref hash, worldSeed);
                HashString(ref hash, secret);
                HashString(ref hash, anchor);
                HashInt(ref hash, channel);
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
