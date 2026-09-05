using System;
using System.Collections.Generic;

namespace Game.Residency.Api
{
    public enum ResidencyFidelity : byte
    {
        Dormant = 0,
        Coarse = 1,
        Detailed = 2
    }

    public enum ResidencyTargetKind : byte
    {
        SpatialRegion = 0,
        Character = 1,
        WorldObject = 2,
        Encounter = 3,
        Site = 4,
        Semantic = 5
    }

    public readonly struct ResidencyTarget : IEquatable<ResidencyTarget>, IComparable<ResidencyTarget>
    {
        public ResidencyTargetKind Kind { get; }
        public string Id { get; }

        public ResidencyTarget(ResidencyTargetKind kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Residency target id is required.", nameof(id));
            Kind = kind;
            Id = id.Trim();
        }

        public int CompareTo(ResidencyTarget other)
        {
            int kind = Kind.CompareTo(other.Kind);
            return kind != 0 ? kind : StringComparer.Ordinal.Compare(Id ?? string.Empty, other.Id ?? string.Empty);
        }

        public bool Equals(ResidencyTarget other) => Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ResidencyTarget other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id));
        public override string ToString() => Kind + ":" + (Id ?? string.Empty);
        public static bool operator ==(ResidencyTarget left, ResidencyTarget right) => left.Equals(right);
        public static bool operator !=(ResidencyTarget left, ResidencyTarget right) => !left.Equals(right);
    }

    public readonly struct ResidencyRegion : IEquatable<ResidencyRegion>, IComparable<ResidencyRegion>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public uint TerrainSeed { get; }
        public byte RequestedMipLevel { get; }

        public ResidencyRegion(int x, int y, int z, uint terrainSeed, byte requestedMipLevel = 0)
        {
            X = x; Y = y; Z = z; TerrainSeed = terrainSeed; RequestedMipLevel = requestedMipLevel;
        }

        public int CompareTo(ResidencyRegion other)
        {
            int x = X.CompareTo(other.X); if (x != 0) return x;
            int y = Y.CompareTo(other.Y); if (y != 0) return y;
            int z = Z.CompareTo(other.Z); if (z != 0) return z;
            int seed = TerrainSeed.CompareTo(other.TerrainSeed); if (seed != 0) return seed;
            return RequestedMipLevel.CompareTo(other.RequestedMipLevel);
        }

        public bool Equals(ResidencyRegion other) => X == other.X && Y == other.Y && Z == other.Z && TerrainSeed == other.TerrainSeed && RequestedMipLevel == other.RequestedMipLevel;
        public override bool Equals(object obj) => obj is ResidencyRegion other && Equals(other);
        public override int GetHashCode() => (((X * 397) ^ Y) * 397 ^ Z) * 397 ^ TerrainSeed.GetHashCode() ^ RequestedMipLevel;
        public override string ToString() => X + "," + Y + "," + Z + "@" + TerrainSeed + "/" + RequestedMipLevel;
    }

    public readonly struct ResidencyDemandRequest
    {
        public ResidencyTarget Target { get; }
        public ResidencyFidelity MinimumFidelity { get; }
        public string RequesterId { get; }
        public string Category { get; }
        public string Reason { get; }

        public ResidencyDemandRequest(ResidencyTarget target, ResidencyFidelity minimumFidelity, string requesterId, string category, string reason)
        {
            if (string.IsNullOrWhiteSpace(requesterId)) throw new ArgumentException("Requester id is required.", nameof(requesterId));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Demand category is required.", nameof(category));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Demand reason is required.", nameof(reason));
            Target = target;
            MinimumFidelity = minimumFidelity;
            RequesterId = requesterId.Trim();
            Category = category.Trim();
            Reason = reason.Trim();
        }
    }

    public interface IResidencyDemandLease : IDisposable
    {
        ulong LeaseId { get; }
        ResidencyDemandRequest Demand { get; }
    }

    public enum ResidencyTransitionPhase : byte
    {
        Stable = 0,
        WaitingForWorld = 1,
        Promoting = 2,
        Demoting = 3,
        Failed = 4
    }

    public readonly struct ResidencyTargetSnapshot
    {
        public ResidencyTarget Target { get; }
        public ResidencyFidelity Current { get; }
        public ResidencyFidelity Desired { get; }
        public ResidencyTransitionPhase Phase { get; }
        public string Diagnostic { get; }
        public ulong Revision { get; }

        public ResidencyTargetSnapshot(ResidencyTarget target, ResidencyFidelity current, ResidencyFidelity desired, ResidencyTransitionPhase phase, string diagnostic, ulong revision)
        {
            Target = target; Current = current; Desired = desired; Phase = phase; Diagnostic = diagnostic ?? string.Empty; Revision = revision;
        }
    }

    public readonly struct ResidencyDemandSnapshot
    {
        public ulong LeaseId { get; }
        public ResidencyDemandRequest Demand { get; }
        public ResidencyDemandSnapshot(ulong leaseId, ResidencyDemandRequest demand) { LeaseId = leaseId; Demand = demand; }
    }

    public readonly struct ResidencyTransitionRecord
    {
        public ulong Sequence { get; }
        public ResidencyTarget Target { get; }
        public ResidencyFidelity From { get; }
        public ResidencyFidelity To { get; }
        public ResidencyTransitionPhase Phase { get; }
        public string Diagnostic { get; }

        public ResidencyTransitionRecord(ulong sequence, ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to, ResidencyTransitionPhase phase, string diagnostic)
        {
            Sequence = sequence; Target = target; From = from; To = to; Phase = phase; Diagnostic = diagnostic ?? string.Empty;
        }
    }

    public sealed class ResidencyDiagnosticsSnapshot
    {
        public int DormantCount { get; }
        public int CoarseCount { get; }
        public int DetailedCount { get; }
        public int PendingTransitionCount { get; }
        public IReadOnlyList<ResidencyDemandSnapshot> Demands { get; }
        public IReadOnlyList<ResidencyTransitionRecord> TransitionHistory { get; }

        public ResidencyDiagnosticsSnapshot(int dormantCount, int coarseCount, int detailedCount, int pendingTransitionCount, IReadOnlyList<ResidencyDemandSnapshot> demands, IReadOnlyList<ResidencyTransitionRecord> transitionHistory)
        {
            DormantCount = dormantCount; CoarseCount = coarseCount; DetailedCount = detailedCount; PendingTransitionCount = pendingTransitionCount;
            Demands = demands ?? throw new ArgumentNullException(nameof(demands));
            TransitionHistory = transitionHistory ?? throw new ArgumentNullException(nameof(transitionHistory));
        }
    }

    public enum ResidencyAdapterStatus : byte
    {
        Completed = 0,
        Pending = 1,
        Failed = 2
    }

    public readonly struct ResidencyAdapterResult
    {
        public ResidencyAdapterStatus Status { get; }
        public string Diagnostic { get; }
        private ResidencyAdapterResult(ResidencyAdapterStatus status, string diagnostic) { Status = status; Diagnostic = diagnostic ?? string.Empty; }
        public static ResidencyAdapterResult Completed(string diagnostic = "") => new ResidencyAdapterResult(ResidencyAdapterStatus.Completed, diagnostic);
        public static ResidencyAdapterResult Pending(string diagnostic) => new ResidencyAdapterResult(ResidencyAdapterStatus.Pending, diagnostic);
        public static ResidencyAdapterResult Failed(string diagnostic) => new ResidencyAdapterResult(ResidencyAdapterStatus.Failed, diagnostic);
    }

    /// <summary>Owner adapter changes simulation realization only; it never transfers domain-state ownership to Residency.</summary>
    public interface IResidencyTargetAdapter
    {
        ResidencyTargetKind Kind { get; }
        bool TryGetDetailedRegion(ResidencyTarget target, out ResidencyRegion region);
        ResidencyAdapterResult Promote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to);
        ResidencyAdapterResult Demote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to);
    }

    public interface IGameplayResidencyCoordinator
    {
        IResidencyDemandLease Acquire(ResidencyDemandRequest demand);
        int ReleaseRequester(string requesterId);
        void Reconcile();
        bool TryGetState(ResidencyTarget target, out ResidencyTargetSnapshot snapshot);
        IReadOnlyList<ResidencyTargetSnapshot> GetStates();
        ResidencyDiagnosticsSnapshot GetDiagnostics();
    }
}
