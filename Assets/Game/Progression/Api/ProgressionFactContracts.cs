using System;

namespace Game.Progression.Api
{
    public readonly struct ProgressionFact
    {
        public ulong Sequence { get; }
        public string Kind { get; }
        public string ActorId { get; }
        public string SubjectId { get; }
        public int StateCode { get; }

        public ProgressionFact(ulong sequence, string kind, string actorId, string subjectId, int stateCode)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Fact kind is required.", nameof(kind));
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor id is required.", nameof(actorId));
            if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("Subject id is required.", nameof(subjectId));
            Sequence = sequence;
            Kind = kind;
            ActorId = actorId;
            SubjectId = subjectId;
            StateCode = stateCode;
        }
    }

    /// <summary>Command-side semantic fact seam; query projections may consume this without WorldObjects coupling.</summary>
    public interface IProgressionFactSink
    {
        void Publish(ProgressionFact fact);
    }
}
