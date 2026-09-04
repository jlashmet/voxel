using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum SecretDiscoveryDiagnosticKind
    {
        MissingResolvedSecret = 0,
        DuplicateRouteId = 1,
        RouteSecretMismatch = 2,
        ProtectedShellBypass = 3,
        AuthoredBreakableInvalid = 4,
        DuplicateAnchorId = 5,
        MissingAnchorSite = 6,
        CircularClueDependency = 7,
        InsufficientObservableClues = 8,
        InsufficientChannelDiversity = 9
    }

    public sealed class SecretDiscoveryDiagnostic
    {
        public string Code { get; }
        public SecretDiscoveryDiagnosticKind Kind { get; }
        public SecretRef Secret { get; }
        public string SubjectId { get; }
        public string Message { get; }

        public SecretDiscoveryDiagnostic(
            string code,
            SecretDiscoveryDiagnosticKind kind,
            SecretRef secret,
            string subjectId,
            string message)
        {
            Code = WorldIdRules.Require(code, nameof(code));
            Kind = kind;
            Secret = secret;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public override string ToString() => Code + ": " + Message;
    }

    public sealed class PlannedSecretRoute
    {
        public SecretRouteId Id { get; }
        public SecretRef Secret { get; }
        public SecretRouteKind Kind { get; }
        public SecretBypassPolicy BypassPolicy { get; }
        public string SemanticAnchorRole { get; }
        public bool RequiresInteractable { get; }

        public PlannedSecretRoute(SecretRouteSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            Id = spec.Id;
            Secret = spec.Secret;
            Kind = spec.Kind;
            BypassPolicy = spec.BypassPolicy;
            SemanticAnchorRole = spec.SemanticAnchorRole;
            RequiresInteractable = spec.RequiresInteractable;
        }
    }

    public sealed class PlannedSecretClue
    {
        public SecretClueId Id { get; }
        public SecretRef Secret { get; }
        public SecretClueAnchorId Anchor { get; }
        public SiteRef AnchorSiteRole { get; }
        public ResolvedSiteId AnchorSite { get; }
        public SecretClueAnchorRole Role { get; }
        public SecretClueChannel Channel { get; }
        public bool HasExplainedRoute { get; }
        public SecretRouteId ExplainedRoute { get; }

        public PlannedSecretClue(
            SecretClueId id,
            SecretRef secret,
            SecretClueAnchorId anchor,
            SiteRef anchorSiteRole,
            ResolvedSiteId anchorSite,
            SecretClueAnchorRole role,
            SecretClueChannel channel,
            bool hasExplainedRoute,
            SecretRouteId explainedRoute)
        {
            Id = id;
            Secret = secret;
            Anchor = anchor;
            AnchorSiteRole = anchorSiteRole;
            AnchorSite = anchorSite;
            Role = role;
            Channel = channel;
            HasExplainedRoute = hasExplainedRoute;
            ExplainedRoute = explainedRoute;
        }
    }

    /// <summary>
    /// Stable planning output consumed by realization and runtime registration. The hidden destination
    /// is copied from the canonical SecretPlanner result; all legal routes retain the same SecretRef.
    /// </summary>
    public sealed class ResolvedSecretDiscoveryPlan
    {
        public SecretRef Secret { get; }
        public SecretImportance Importance { get; }
        public SecretCandidateId Candidate { get; }
        public string EntranceId { get; }
        public IReadOnlyList<PlannedSecretRoute> Routes { get; }
        public IReadOnlyList<PlannedSecretClue> Clues { get; }

        public ResolvedSecretDiscoveryPlan(
            SecretRef secret,
            SecretImportance importance,
            SecretCandidateId candidate,
            string entranceId,
            PlannedSecretRoute[] routes,
            PlannedSecretClue[] clues)
        {
            Secret = secret;
            Importance = importance;
            Candidate = candidate;
            EntranceId = WorldIdRules.Require(entranceId, nameof(entranceId));
            Routes = routes ?? Array.Empty<PlannedSecretRoute>();
            Clues = clues ?? Array.Empty<PlannedSecretClue>();
        }
    }

    public sealed class SecretDiscoveryPlanningResult
    {
        public bool IsResolved { get; }
        public ResolvedSecretDiscoveryPlan Plan { get; }
        public IReadOnlyList<SecretDiscoveryDiagnostic> Diagnostics { get; }

        public SecretDiscoveryPlanningResult(
            ResolvedSecretDiscoveryPlan plan,
            SecretDiscoveryDiagnostic[] diagnostics)
        {
            Plan = plan;
            Diagnostics = diagnostics ?? Array.Empty<SecretDiscoveryDiagnostic>();
            IsResolved = plan != null && Diagnostics.Count == 0;
        }
    }
}
