using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Stable identity of one concrete site exposed by a generated-world candidate set.
    /// This is deliberately distinct from SiteRef: SiteRef is the authored semantic role,
    /// while ResolvedSiteId identifies the generated site selected to fulfil that role.
    /// </summary>
    public readonly struct ResolvedSiteId : IEquatable<ResolvedSiteId>
    {
        public string Value { get; }

        public ResolvedSiteId(string value) =>
            Value = WorldIdRules.Require(value, nameof(value));

        public bool Equals(ResolvedSiteId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ResolvedSiteId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct SiteCapabilityOffer
    {
        public SiteCapabilityKind Kind { get; }
        public int Capacity { get; }

        public SiteCapabilityOffer(SiteCapabilityKind kind, int capacity = 1)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Kind = kind;
            Capacity = capacity;
        }
    }

    public sealed class SiteCandidate
    {
        public ResolvedSiteId Id { get; }
        public SiteArchetype Archetype { get; }
        public IReadOnlyList<SiteCapabilityOffer> Capabilities { get; }

        public SiteCandidate(
            ResolvedSiteId id,
            SiteArchetype archetype,
            SiteCapabilityOffer[] capabilities)
        {
            if (archetype == SiteArchetype.Unspecified)
                throw new ArgumentException(
                    "A generated site candidate must have a concrete archetype.",
                    nameof(archetype));

            Id = id;
            Archetype = archetype;
            Capabilities = capabilities ?? Array.Empty<SiteCapabilityOffer>();
        }
    }

    /// <summary>
    /// Read-only facts supplied by the world/spatial planner. WorldBuilder owns the semantic
    /// constraint solving, but does not know how regions, settlements, navigation graphs, or
    /// generated geometry are represented by the backend.
    /// </summary>
    public interface ISiteCandidateFacts
    {
        IReadOnlyList<SiteCandidate> Candidates { get; }

        bool IsInRegion(ResolvedSiteId candidate, RegionRef region);
        bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement);

        bool IsReachable(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal);

        int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target);
        int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target);
        int TraversalDistanceMetres(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal);
    }

    public sealed class SiteRoleBinding
    {
        public SiteRef Role { get; }
        public ResolvedSiteId Site { get; }

        public SiteRoleBinding(SiteRef role, ResolvedSiteId site)
        {
            Role = role;
            Site = site;
        }
    }

    public enum SiteResolutionDiagnosticKind
    {
        NoCandidateForRole = 0,
        ArchetypeUnsatisfied = 1,
        CapabilityUnsatisfied = 2,
        HierarchyUnsatisfied = 3,
        DifferentSiteUnsatisfied = 4,
        ReachabilityUnsatisfied = 5,
        DistanceUnsatisfied = 6
    }

    public sealed class SiteResolutionDiagnostic
    {
        public string Code { get; }
        public SiteResolutionDiagnosticKind Kind { get; }
        public SiteRef Role { get; }
        public SiteRef OtherRole { get; }
        public string Message { get; }

        public SiteResolutionDiagnostic(
            string code,
            SiteResolutionDiagnosticKind kind,
            SiteRef role,
            SiteRef otherRole,
            string message)
        {
            Code = WorldIdRules.Require(code, nameof(code));
            Kind = kind;
            Role = role;
            OtherRole = otherRole;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public override string ToString() => Code + ": " + Message;
    }

    public sealed class SiteResolutionResult
    {
        public bool IsResolved { get; }
        public IReadOnlyList<SiteRoleBinding> Bindings { get; }
        public IReadOnlyList<SiteResolutionDiagnostic> Diagnostics { get; }

        public SiteResolutionResult(
            SiteRoleBinding[] bindings,
            SiteResolutionDiagnostic[] diagnostics)
        {
            Bindings = bindings ?? Array.Empty<SiteRoleBinding>();
            Diagnostics = diagnostics ?? Array.Empty<SiteResolutionDiagnostic>();
            IsResolved = Diagnostics.Count == 0;
        }
    }
}
