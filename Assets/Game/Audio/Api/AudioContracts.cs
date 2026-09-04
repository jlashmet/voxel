using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Audio.Api
{
    public readonly struct AudioCueRef : IEquatable<AudioCueRef>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public AudioCueRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Semantic audio cue id is required.", nameof(value));
            Value = value.Trim();
        }
        public bool Equals(AudioCueRef other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AudioCueRef other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioCueRef left, AudioCueRef right) => left.Equals(right);
        public static bool operator !=(AudioCueRef left, AudioCueRef right) => !left.Equals(right);
    }

    public readonly struct AudioEventId : IEquatable<AudioEventId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public AudioEventId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Stable audio event id is required.", nameof(value));
            Value = value.Trim();
        }
        public bool Equals(AudioEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AudioEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioEventId left, AudioEventId right) => left.Equals(right);
        public static bool operator !=(AudioEventId left, AudioEventId right) => !left.Equals(right);
    }

    public readonly struct SustainedAudioKey : IEquatable<SustainedAudioKey>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public SustainedAudioKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Sustained audio key is required.", nameof(value));
            Value = value.Trim();
        }
        public bool Equals(SustainedAudioKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SustainedAudioKey other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SustainedAudioKey left, SustainedAudioKey right) => left.Equals(right);
        public static bool operator !=(SustainedAudioKey left, SustainedAudioKey right) => !left.Equals(right);
    }

    public enum AudioBusKind : byte { Sfx = 0, Music = 1, Ambience = 2, Voice = 3 }
    public enum AudioOriginKind : byte { Global = 0, Character = 1, WorldObject = 2, WorldPoint = 3 }

    public readonly struct AudioWorldPoint : IEquatable<AudioWorldPoint>
    {
        public int XDecimetres { get; }
        public int YDecimetres { get; }
        public int ZDecimetres { get; }
        public AudioWorldPoint(int xDecimetres, int yDecimetres, int zDecimetres)
        { XDecimetres = xDecimetres; YDecimetres = yDecimetres; ZDecimetres = zDecimetres; }
        public bool Equals(AudioWorldPoint other) => XDecimetres == other.XDecimetres && YDecimetres == other.YDecimetres && ZDecimetres == other.ZDecimetres;
        public override bool Equals(object obj) => obj is AudioWorldPoint other && Equals(other);
        public override int GetHashCode() => ((XDecimetres * 397) ^ YDecimetres) * 397 ^ ZDecimetres;
    }

    public readonly struct AudioSemanticOrigin : IEquatable<AudioSemanticOrigin>
    {
        public AudioOriginKind Kind { get; }
        public CharacterId CharacterId { get; }
        public string WorldObjectId { get; }
        public AudioWorldPoint WorldPoint { get; }
        private AudioSemanticOrigin(AudioOriginKind kind, CharacterId characterId, string worldObjectId, AudioWorldPoint worldPoint)
        { Kind = kind; CharacterId = characterId; WorldObjectId = worldObjectId ?? string.Empty; WorldPoint = worldPoint; }
        public static AudioSemanticOrigin Global => new AudioSemanticOrigin(AudioOriginKind.Global, default, string.Empty, default);
        public static AudioSemanticOrigin ForCharacter(CharacterId id)
        {
            if (!id.IsValid) throw new ArgumentException("Character origin requires a valid CharacterId.", nameof(id));
            return new AudioSemanticOrigin(AudioOriginKind.Character, id, string.Empty, default);
        }
        public static AudioSemanticOrigin ForWorldObject(string semanticObjectId)
        {
            if (string.IsNullOrWhiteSpace(semanticObjectId)) throw new ArgumentException("Semantic world object id is required.", nameof(semanticObjectId));
            return new AudioSemanticOrigin(AudioOriginKind.WorldObject, default, semanticObjectId.Trim(), default);
        }
        public static AudioSemanticOrigin AtWorldPoint(AudioWorldPoint point) => new AudioSemanticOrigin(AudioOriginKind.WorldPoint, default, string.Empty, point);
        public bool Equals(AudioSemanticOrigin other) => Kind == other.Kind && CharacterId == other.CharacterId && string.Equals(WorldObjectId, other.WorldObjectId, StringComparison.Ordinal) && WorldPoint.Equals(other.WorldPoint);
        public override bool Equals(object obj) => obj is AudioSemanticOrigin other && Equals(other);
        public override int GetHashCode() => (((int)Kind * 397) ^ CharacterId.GetHashCode()) * 397 ^ (WorldObjectId == null ? 0 : StringComparer.Ordinal.GetHashCode(WorldObjectId)) ^ WorldPoint.GetHashCode();
        public static bool operator ==(AudioSemanticOrigin left, AudioSemanticOrigin right) => left.Equals(right);
        public static bool operator !=(AudioSemanticOrigin left, AudioSemanticOrigin right) => !left.Equals(right);
    }

    public readonly struct AudioOneShotRequest
    {
        public AudioEventId EventId { get; }
        public AudioCueRef Cue { get; }
        public AudioSemanticOrigin Origin { get; }
        public bool AnticipatedLocally { get; }
        public AudioOneShotRequest(AudioEventId eventId, AudioCueRef cue, AudioSemanticOrigin origin, bool anticipatedLocally = false)
        {
            if (!eventId.IsValid) throw new ArgumentException("Event identity is required.", nameof(eventId));
            if (!cue.IsValid) throw new ArgumentException("Cue is required.", nameof(cue));
            EventId = eventId; Cue = cue; Origin = origin; AnticipatedLocally = anticipatedLocally;
        }
    }

    public readonly struct SustainedAudioState : IEquatable<SustainedAudioState>
    {
        public SustainedAudioKey Key { get; }
        public AudioCueRef Cue { get; }
        public AudioSemanticOrigin Origin { get; }
        public bool Active { get; }
        public SustainedAudioState(SustainedAudioKey key, AudioCueRef cue, AudioSemanticOrigin origin, bool active)
        {
            if (!key.IsValid) throw new ArgumentException("Sustained key is required.", nameof(key));
            if (!cue.IsValid) throw new ArgumentException("Cue is required.", nameof(cue));
            Key = key; Cue = cue; Origin = origin; Active = active;
        }
        public bool Equals(SustainedAudioState other) => Key == other.Key && Cue == other.Cue && Origin == other.Origin && Active == other.Active;
        public override bool Equals(object obj) => obj is SustainedAudioState other && Equals(other);
        public override int GetHashCode() => (((Key.GetHashCode() * 397) ^ Cue.GetHashCode()) * 397) ^ Origin.GetHashCode() ^ (Active ? 1 : 0);
    }

    public readonly struct AudioMixPreferences
    {
        public float Master { get; }
        public float Sfx { get; }
        public float Music { get; }
        public float Ambience { get; }
        public float Voice { get; }
        public AudioMixPreferences(float master, float sfx, float music, float ambience, float voice)
        { Master = Clamp(master); Sfx = Clamp(sfx); Music = Clamp(music); Ambience = Clamp(ambience); Voice = Clamp(voice); }
        public static AudioMixPreferences Default => new AudioMixPreferences(1f, 1f, 1f, 1f, 1f);
        public float GainFor(AudioBusKind bus)
        {
            float busGain = bus == AudioBusKind.Music ? Music : bus == AudioBusKind.Ambience ? Ambience : bus == AudioBusKind.Voice ? Voice : Sfx;
            return Master * busGain;
        }
        private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public enum AudioDispatchStatus : byte { Played = 0, DuplicateSuppressed = 1, Reconciled = 2, UnknownCue = 3, OriginUnavailable = 4, BackendFailure = 5 }
    public readonly struct AudioDispatchResult
    {
        public AudioDispatchStatus Status { get; }
        public string Diagnostic { get; }
        public bool PresentationSucceeded => Status == AudioDispatchStatus.Played || Status == AudioDispatchStatus.DuplicateSuppressed || Status == AudioDispatchStatus.Reconciled;
        public AudioDispatchResult(AudioDispatchStatus status, string diagnostic = "") { Status = status; Diagnostic = diagnostic ?? string.Empty; }
    }

    public interface IAudioPresentation
    {
        AudioDispatchResult DispatchOneShot(AudioOneShotRequest request);
        AudioDispatchResult ReconcileSustained(IReadOnlyList<SustainedAudioState> currentState);
        void ApplyMix(AudioMixPreferences preferences);
    }
}
