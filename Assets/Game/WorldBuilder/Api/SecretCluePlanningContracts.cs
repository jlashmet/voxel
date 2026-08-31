using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum SecretClueDiagnosticKind
    {
        DuplicateClueId = 0,
        DuplicateStage = 1,
        MissingResolvedSecret = 2,
        MissingRequiredSource = 3,
        MissingTargetSite = 4,
        InvalidRumorSource = 5
    }

    public sealed class SecretClueDiagnostic
    {
        public string Code { get; }
        public SecretClueDiagnosticKind Kind { get; }
        public SecretRef Secret { get; }
        public SecretClueId Clue { get; }
        public string Message { get; }

        public SecretClueDiagnostic(
            string code,
            SecretClueDiagnosticKind kind,
            SecretRef secret,
            SecretClueId clue,
            string message)
        {
            Code = WorldIdRules.Require(code, nameof(code));
            Kind = kind;
            Secret = secret;
            Clue = clue;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public override string ToString() => Code + ": " + Message;
    }

    /// <summary>
    /// Deterministically resolved clue step. SourceSite is a concrete generated site, while SourceRole/Npc
    /// preserve semantic provenance. TargetCandidate and TargetEntrance come directly from the canonical
    /// resolved secret plan, preventing clue planning from choosing a second hidden location.
    /// </summary>
    public sealed class ResolvedSecretCluePlan
    {
        public SecretClueId Id { get; }
        public SecretRef Secret { get; }
        public int Stage { get; }
        public SecretClueKind Kind { get; }
        public SecretClueSourceKind SourceKind { get; }
        public SiteRef SourceRole { get; }
        public NpcRef SourceNpc { get; }
        public ResolvedSiteId SourceSite { get; }
        public SecretCandidateId TargetCandidate { get; }
        public string TargetEntrance { get; }
        public string ContentKey { get; }
        public string MemoryTopic { get; }

        public ResolvedSecretCluePlan(
            SecretClueId id,
            SecretRef secret,
            int stage,
            SecretClueKind kind,
            SecretClueSourceKind sourceKind,
            SiteRef sourceRole,
            NpcRef sourceNpc,
            ResolvedSiteId sourceSite,
            SecretCandidateId targetCandidate,
            string targetEntrance,
            string contentKey,
            string memoryTopic)
        {
            Id = id;
            Secret = secret;
            Stage = stage;
            Kind = kind;
            SourceKind = sourceKind;
            SourceRole = sourceRole;
            SourceNpc = sourceNpc;
            SourceSite = sourceSite;
            TargetCandidate = targetCandidate;
            TargetEntrance = WorldIdRules.Require(targetEntrance, nameof(targetEntrance));
            ContentKey = contentKey ?? throw new ArgumentNullException(nameof(contentKey));
            MemoryTopic = memoryTopic ?? string.Empty;
        }
    }

    public sealed class SecretCluePlanningResult
    {
        public bool IsResolved { get; }
        public IReadOnlyList<ResolvedSecretCluePlan> Clues { get; }
        public IReadOnlyList<SecretClueDiagnostic> Diagnostics { get; }

        public SecretCluePlanningResult(
            ResolvedSecretCluePlan[] clues,
            SecretClueDiagnostic[] diagnostics)
        {
            Clues = clues ?? Array.Empty<ResolvedSecretCluePlan>();
            Diagnostics = diagnostics ?? Array.Empty<SecretClueDiagnostic>();
            IsResolved = Diagnostics.Count == 0;
        }
    }

    public sealed class SecretDiscoverySnapshot
    {
        public IReadOnlyList<string> ObservedClueIds { get; }
        public IReadOnlyList<string> DiscoveredSecretIds { get; }

        public SecretDiscoverySnapshot(string[] observedClueIds, string[] discoveredSecretIds)
        {
            ObservedClueIds = observedClueIds ?? Array.Empty<string>();
            DiscoveredSecretIds = discoveredSecretIds ?? Array.Empty<string>();
        }
    }
}
