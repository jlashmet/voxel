using System;

namespace Game.Outcomes.Api
{
    public readonly struct OutcomeRef : IEquatable<OutcomeRef>, IComparable<OutcomeRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public OutcomeRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Outcome ref is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(OutcomeRef other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(OutcomeRef other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OutcomeRef other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(OutcomeRef left, OutcomeRef right) => left.Equals(right);
        public static bool operator !=(OutcomeRef left, OutcomeRef right) => !left.Equals(right);
    }

    public readonly struct OutcomeAuthorityRef : IEquatable<OutcomeAuthorityRef>, IComparable<OutcomeAuthorityRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public OutcomeAuthorityRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Outcome authority ref is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(OutcomeAuthorityRef other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(OutcomeAuthorityRef other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OutcomeAuthorityRef other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(OutcomeAuthorityRef left, OutcomeAuthorityRef right) => left.Equals(right);
        public static bool operator !=(OutcomeAuthorityRef left, OutcomeAuthorityRef right) => !left.Equals(right);
    }

    public readonly struct OutcomeResolutionId : IEquatable<OutcomeResolutionId>, IComparable<OutcomeResolutionId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public OutcomeResolutionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Outcome resolution id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(OutcomeResolutionId other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(OutcomeResolutionId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OutcomeResolutionId other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(OutcomeResolutionId left, OutcomeResolutionId right) => left.Equals(right);
        public static bool operator !=(OutcomeResolutionId left, OutcomeResolutionId right) => !left.Equals(right);
    }

    public readonly struct OutcomeConditionRef : IEquatable<OutcomeConditionRef>, IComparable<OutcomeConditionRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public OutcomeConditionRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Outcome condition ref is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(OutcomeConditionRef other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(OutcomeConditionRef other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OutcomeConditionRef other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(OutcomeConditionRef left, OutcomeConditionRef right) => left.Equals(right);
        public static bool operator !=(OutcomeConditionRef left, OutcomeConditionRef right) => !left.Equals(right);
    }

    public enum GameOutcomeLifecycle : byte
    {
        Running = 0,
        Resolved = 1
    }

    public enum GameOutcomeDisposition : byte
    {
        None = 0,
        Success = 1,
        Failure = 2
    }

    public enum GameOutcomeResolutionStatus : byte
    {
        Accepted = 0,
        Idempotent = 1,
        RejectedUnauthorized = 2,
        RejectedAlreadyResolved = 3
    }

    public readonly struct GameOutcomeResolutionRequest
    {
        public OutcomeResolutionId ResolutionId { get; }
        public OutcomeAuthorityRef Authority { get; }
        public GameOutcomeDisposition Disposition { get; }
        public OutcomeRef Outcome { get; }

        public GameOutcomeResolutionRequest(
            OutcomeResolutionId resolutionId,
            OutcomeAuthorityRef authority,
            GameOutcomeDisposition disposition,
            OutcomeRef outcome)
        {
            if (!resolutionId.IsValid)
                throw new ArgumentException("Resolution id is required.", nameof(resolutionId));
            if (!authority.IsValid)
                throw new ArgumentException("Outcome authority is required.", nameof(authority));
            if (disposition == GameOutcomeDisposition.None)
                throw new ArgumentException("Terminal outcome disposition is required.", nameof(disposition));
            if (!outcome.IsValid)
                throw new ArgumentException("Terminal outcome ref is required.", nameof(outcome));

            ResolutionId = resolutionId;
            Authority = authority;
            Disposition = disposition;
            Outcome = outcome;
        }
    }

    public readonly struct GameOutcomeSnapshot
    {
        public GameOutcomeLifecycle Lifecycle { get; }
        public GameOutcomeDisposition Disposition { get; }
        public OutcomeRef Outcome { get; }
        public OutcomeResolutionId ResolutionId { get; }
        public OutcomeAuthorityRef Authority { get; }
        public ulong Revision { get; }

        public GameOutcomeSnapshot(
            GameOutcomeLifecycle lifecycle,
            GameOutcomeDisposition disposition,
            OutcomeRef outcome,
            ulong revision)
            : this(
                lifecycle,
                disposition,
                outcome,
                lifecycle == GameOutcomeLifecycle.Resolved && outcome.IsValid
                    ? new OutcomeResolutionId("legacy:" + outcome.Value)
                    : default,
                lifecycle == GameOutcomeLifecycle.Resolved && outcome.IsValid
                    ? new OutcomeAuthorityRef("legacy")
                    : default,
                revision)
        {
        }

        public GameOutcomeSnapshot(
            GameOutcomeLifecycle lifecycle,
            GameOutcomeDisposition disposition,
            OutcomeRef outcome,
            OutcomeResolutionId resolutionId,
            OutcomeAuthorityRef authority,
            ulong revision)
        {
            if (lifecycle == GameOutcomeLifecycle.Running &&
                (disposition != GameOutcomeDisposition.None || outcome.IsValid ||
                 resolutionId.IsValid || authority.IsValid))
            {
                throw new ArgumentException(
                    "Running outcome state cannot contain terminal disposition, outcome, resolution, or authority data.");
            }

            if (lifecycle == GameOutcomeLifecycle.Resolved &&
                (disposition == GameOutcomeDisposition.None || !outcome.IsValid ||
                 !resolutionId.IsValid || !authority.IsValid))
            {
                throw new ArgumentException(
                    "Resolved outcome state requires disposition, outcome, resolution, and authority data.");
            }

            Lifecycle = lifecycle;
            Disposition = disposition;
            Outcome = outcome;
            ResolutionId = resolutionId;
            Authority = authority;
            Revision = revision;
        }

        public static GameOutcomeSnapshot Running(ulong revision = 0) =>
            new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Running,
                GameOutcomeDisposition.None,
                default,
                default,
                default,
                revision);
    }

    public readonly struct GameOutcomeResolved
    {
        public OutcomeResolutionId ResolutionId { get; }
        public OutcomeAuthorityRef Authority { get; }
        public GameOutcomeDisposition Disposition { get; }
        public OutcomeRef Outcome { get; }
        public ulong Revision { get; }

        public GameOutcomeResolved(GameOutcomeSnapshot snapshot)
        {
            if (snapshot.Lifecycle != GameOutcomeLifecycle.Resolved)
                throw new ArgumentException("Resolved outcome event requires terminal snapshot state.", nameof(snapshot));

            ResolutionId = snapshot.ResolutionId;
            Authority = snapshot.Authority;
            Disposition = snapshot.Disposition;
            Outcome = snapshot.Outcome;
            Revision = snapshot.Revision;
        }
    }

    public readonly struct GameOutcomeResolutionResult
    {
        public GameOutcomeResolutionStatus Status { get; }
        public GameOutcomeSnapshot Snapshot { get; }
        public bool Committed => Status == GameOutcomeResolutionStatus.Accepted;
        public bool Succeeded =>
            Status == GameOutcomeResolutionStatus.Accepted ||
            Status == GameOutcomeResolutionStatus.Idempotent;

        public GameOutcomeResolutionResult(
            GameOutcomeResolutionStatus status,
            GameOutcomeSnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot;
        }
    }

    public interface IGameOutcomeQuery
    {
        GameOutcomeSnapshot Snapshot();
    }

    public interface IGameOutcomeResolver
    {
        GameOutcomeResolutionResult RequestResolution(GameOutcomeResolutionRequest request);
    }

    public interface IGameOutcomeEvents
    {
        event Action<GameOutcomeResolved> OutcomeResolved;
    }

    public interface IGameOutcomeService : IGameOutcomeQuery, IGameOutcomeResolver, IGameOutcomeEvents
    {
    }
}
