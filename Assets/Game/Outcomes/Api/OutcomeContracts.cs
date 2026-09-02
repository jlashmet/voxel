using System;

namespace Game.Outcomes.Api
{
    public readonly struct OutcomeRef : IEquatable<OutcomeRef>, IComparable<OutcomeRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public OutcomeRef(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Outcome ref is required.", nameof(value)); Value = value; }
        public int CompareTo(OutcomeRef other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(OutcomeRef other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OutcomeRef other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(OutcomeRef left, OutcomeRef right) => left.Equals(right);
        public static bool operator !=(OutcomeRef left, OutcomeRef right) => !left.Equals(right);
    }

    public enum GameOutcomeLifecycle : byte { Running = 0, Resolved = 1 }
    public enum GameOutcomeDisposition : byte { None = 0, Success = 1, Failure = 2 }

    public readonly struct GameOutcomeSnapshot
    {
        public GameOutcomeLifecycle Lifecycle { get; }
        public GameOutcomeDisposition Disposition { get; }
        public OutcomeRef Outcome { get; }
        public ulong Revision { get; }

        public GameOutcomeSnapshot(GameOutcomeLifecycle lifecycle, GameOutcomeDisposition disposition, OutcomeRef outcome, ulong revision)
        {
            if (lifecycle == GameOutcomeLifecycle.Running && (disposition != GameOutcomeDisposition.None || outcome.IsValid))
                throw new ArgumentException("Running outcome state cannot contain a terminal disposition or outcome ref.");
            if (lifecycle == GameOutcomeLifecycle.Resolved && (disposition == GameOutcomeDisposition.None || !outcome.IsValid))
                throw new ArgumentException("Resolved outcome state requires a disposition and outcome ref.");
            Lifecycle = lifecycle; Disposition = disposition; Outcome = outcome; Revision = revision;
        }
    }

    public interface IGameOutcomeQuery
    {
        GameOutcomeSnapshot Snapshot();
    }
}
