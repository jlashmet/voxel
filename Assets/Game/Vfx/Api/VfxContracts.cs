using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.WorldObjects.Api;

namespace Game.Vfx.Api
{
    public readonly struct VfxCueRef : IEquatable<VfxCueRef>, IComparable<VfxCueRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public VfxCueRef(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("VFX cue ref is required.", nameof(value)); Value = value; }
        public int CompareTo(VfxCueRef other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(VfxCueRef other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VfxCueRef other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VfxCueRef left, VfxCueRef right) => left.Equals(right);
        public static bool operator !=(VfxCueRef left, VfxCueRef right) => !left.Equals(right);
    }

    public readonly struct VfxEventId : IEquatable<VfxEventId>, IComparable<VfxEventId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public VfxEventId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("VFX event id is required.", nameof(value)); Value = value; }
        public int CompareTo(VfxEventId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(VfxEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VfxEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VfxEventId left, VfxEventId right) => left.Equals(right);
        public static bool operator !=(VfxEventId left, VfxEventId right) => !left.Equals(right);
    }

    public readonly struct VfxTreatmentId : IEquatable<VfxTreatmentId>, IComparable<VfxTreatmentId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public VfxTreatmentId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("VFX treatment id is required.", nameof(value)); Value = value; }
        public int CompareTo(VfxTreatmentId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(VfxTreatmentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VfxTreatmentId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VfxTreatmentId left, VfxTreatmentId right) => left.Equals(right);
        public static bool operator !=(VfxTreatmentId left, VfxTreatmentId right) => !left.Equals(right);
    }

    public readonly struct VfxWorldPoint : IEquatable<VfxWorldPoint>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public VfxWorldPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool Equals(VfxWorldPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is VfxWorldPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(VfxWorldPoint left, VfxWorldPoint right) => left.Equals(right);
        public static bool operator !=(VfxWorldPoint left, VfxWorldPoint right) => !left.Equals(right);
    }

    public enum VfxOriginKind : byte { None = 0, Character = 1, WorldObject = 2, WorldPoint = 3 }

    public readonly struct VfxSemanticOrigin
    {
        public VfxOriginKind Kind { get; }
        public CharacterId CharacterId { get; }
        public WorldObjectId WorldObjectId { get; }
        public VfxWorldPoint Point { get; }

        private VfxSemanticOrigin(VfxOriginKind kind, CharacterId characterId, WorldObjectId worldObjectId, VfxWorldPoint point)
        { Kind = kind; CharacterId = characterId; WorldObjectId = worldObjectId; Point = point; }

        public static VfxSemanticOrigin None() => new VfxSemanticOrigin(VfxOriginKind.None, default, default, default);
        public static VfxSemanticOrigin Character(CharacterId id)
        { if (!id.IsValid) throw new ArgumentException("Character id is required.", nameof(id)); return new VfxSemanticOrigin(VfxOriginKind.Character, id, default, default); }
        public static VfxSemanticOrigin WorldObject(WorldObjectId id)
        { if (!id.IsValid) throw new ArgumentException("World object id is required.", nameof(id)); return new VfxSemanticOrigin(VfxOriginKind.WorldObject, default, id, default); }
        public static VfxSemanticOrigin WorldPoint(float x, float y, float z) => new VfxSemanticOrigin(VfxOriginKind.WorldPoint, default, default, new VfxWorldPoint(x, y, z));
    }

    public enum VfxCuePhase : byte { Predicted = 0, Confirmed = 1 }

    public readonly struct VfxCueRequest
    {
        public VfxCueRef Cue { get; }
        public VfxEventId EventId { get; }
        public VfxSemanticOrigin Origin { get; }
        public VfxCuePhase Phase { get; }
        public VfxCueRequest(VfxCueRef cue, VfxEventId eventId, VfxSemanticOrigin origin, VfxCuePhase phase)
        {
            if (!cue.IsValid) throw new ArgumentException("Cue is required.", nameof(cue));
            if (!eventId.IsValid) throw new ArgumentException("Stable event id is required.", nameof(eventId));
            Cue = cue; EventId = eventId; Origin = origin; Phase = phase;
        }
    }

    public readonly struct VfxPersistentTreatmentDescriptor
    {
        public VfxTreatmentId TreatmentId { get; }
        public VfxCueRef Cue { get; }
        public VfxSemanticOrigin Origin { get; }
        public VfxPersistentTreatmentDescriptor(VfxTreatmentId treatmentId, VfxCueRef cue, VfxSemanticOrigin origin)
        {
            if (!treatmentId.IsValid) throw new ArgumentException("Treatment id is required.", nameof(treatmentId));
            if (!cue.IsValid) throw new ArgumentException("Cue is required.", nameof(cue));
            TreatmentId = treatmentId; Cue = cue; Origin = origin;
        }
    }

    public enum VfxSubmitResult : byte { Played = 0, Deduplicated = 1, MissingMapping = 2, MissingBinding = 3, Invalid = 4 }
    public enum VfxDiagnosticCode : byte { MissingCueMapping = 0, MissingOriginBinding = 1, InvalidRequest = 2 }

    public readonly struct VfxDiagnostic
    {
        public VfxDiagnosticCode Code { get; }
        public VfxCueRef Cue { get; }
        public VfxEventId EventId { get; }
        public string Message { get; }
        public VfxDiagnostic(VfxDiagnosticCode code, VfxCueRef cue, VfxEventId eventId, string message)
        { Code = code; Cue = cue; EventId = eventId; Message = message ?? string.Empty; }
    }

    public interface IVfxCueSink { VfxSubmitResult Submit(VfxCueRequest request); }
    public interface IVfxTreatmentSink { void Reconcile(IReadOnlyList<VfxPersistentTreatmentDescriptor> currentTreatments); }
    public interface IVfxDiagnosticsSink { void Report(VfxDiagnostic diagnostic); }
    public interface IVfxPresentationBindingResolver { bool TryResolve(VfxSemanticOrigin origin, out VfxWorldPoint point); }
}
