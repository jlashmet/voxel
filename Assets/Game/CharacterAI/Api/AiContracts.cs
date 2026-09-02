using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.CharacterAI.Api
{
    public enum AiObservationKind
    {
        Fact = 0,
        Character = 1,
        WorldObject = 2,
        Site = 3,
        Encounter = 4,
        Combat = 5
    }

    public enum AiIntentKind
    {
        Idle = 0,
        Move = 1,
        Interact = 2,
        TacticalCombat = 3
    }

    public enum AiControlMode
    {
        Disabled = 0,
        Autonomous = 1,
        Tactical = 2
    }

    public readonly struct AiObservation
    {
        public AiObservationKind Kind { get; }
        public CharacterId RelatedCharacter { get; }
        public string SemanticId { get; }
        public int Value { get; }

        public AiObservation(AiObservationKind kind, CharacterId relatedCharacter, string semanticId, int value = 0)
        {
            Kind = kind;
            RelatedCharacter = relatedCharacter;
            SemanticId = semanticId ?? string.Empty;
            Value = value;
        }
    }

    public sealed class AiPerceptionSnapshot
    {
        public CharacterId Actor { get; }
        public IReadOnlyList<AiObservation> Observations { get; }

        public AiPerceptionSnapshot(CharacterId actor, IReadOnlyList<AiObservation> observations)
        {
            if (!actor.IsValid) throw new ArgumentException("AI actor is required.", nameof(actor));
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var copy = new AiObservation[observations.Count];
            for (int i = 0; i < observations.Count; i++) copy[i] = observations[i];
            Actor = actor;
            Observations = Array.AsReadOnly(copy);
        }

        public bool Has(AiObservationKind kind, string semanticId = null)
        {
            for (int i = 0; i < Observations.Count; i++)
            {
                AiObservation observation = Observations[i];
                if (observation.Kind != kind) continue;
                if (semanticId == null || string.Equals(observation.SemanticId, semanticId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    public readonly struct AiIntent : IEquatable<AiIntent>
    {
        public CharacterId Actor { get; }
        public AiIntentKind Kind { get; }
        public CharacterId TargetCharacter { get; }
        public string TargetSemanticId { get; }
        public int Priority { get; }
        public string TieBreakKey { get; }

        public AiIntent(CharacterId actor, AiIntentKind kind, CharacterId targetCharacter, string targetSemanticId, int priority, string tieBreakKey)
        {
            if (!actor.IsValid) throw new ArgumentException("AI actor is required.", nameof(actor));
            Actor = actor;
            Kind = kind;
            TargetCharacter = targetCharacter;
            TargetSemanticId = targetSemanticId ?? string.Empty;
            Priority = priority;
            TieBreakKey = tieBreakKey ?? string.Empty;
        }

        public bool Equals(AiIntent other) =>
            Actor.Equals(other.Actor) &&
            Kind == other.Kind &&
            TargetCharacter.Equals(other.TargetCharacter) &&
            string.Equals(TargetSemanticId, other.TargetSemanticId, StringComparison.Ordinal) &&
            Priority == other.Priority &&
            string.Equals(TieBreakKey, other.TieBreakKey, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AiIntent other && Equals(other);
        public override int GetHashCode() => Actor.GetHashCode() ^ ((int)Kind * 397) ^ Priority;
        public override string ToString() => Actor + ":" + Kind + ":" + TargetSemanticId;
    }

    public readonly struct AiIntentExecutionResult
    {
        public bool Accepted { get; }
        public string Reason { get; }

        private AiIntentExecutionResult(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public static AiIntentExecutionResult Accept() => new AiIntentExecutionResult(true, string.Empty);
        public static AiIntentExecutionResult Reject(string reason) => new AiIntentExecutionResult(false, reason);
    }

    public sealed class AiControlState
    {
        public CharacterId Actor { get; }
        public bool Enabled { get; }
        public AiControlMode Mode { get; }
        public AiIntent CurrentIntent { get; }
        public bool LastIntentAccepted { get; }
        public string Diagnostic { get; }

        public AiControlState(CharacterId actor, bool enabled, AiControlMode mode, AiIntent currentIntent, bool lastIntentAccepted, string diagnostic)
        {
            Actor = actor;
            Enabled = enabled;
            Mode = mode;
            CurrentIntent = currentIntent;
            LastIntentAccepted = lastIntentAccepted;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    public interface IAiPerceptionSource
    {
        AiPerceptionSnapshot Observe(CharacterId actor);
    }

    public interface IAiIntentPolicy
    {
        AiIntent SelectIntent(AiPerceptionSnapshot perception);
    }

    public interface IAiIntentExecutor
    {
        AiIntentExecutionResult TryExecute(AiIntent intent);
    }

    public interface ICharacterAiController
    {
        CharacterId Actor { get; }
        AiControlState State { get; }
        void SetEnabled(bool enabled);
        AiIntentExecutionResult Tick();
    }
}
