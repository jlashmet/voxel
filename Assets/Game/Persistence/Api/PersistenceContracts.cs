using System;
using System.Collections.Generic;

namespace Game.Persistence.Api
{
    public readonly struct SessionSaveId : IEquatable<SessionSaveId>, IComparable<SessionSaveId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public SessionSaveId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Save id is required.", nameof(value)); Value = value.Trim(); }
        public int CompareTo(SessionSaveId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(SessionSaveId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SessionSaveId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<unset-save>";
        public static bool operator ==(SessionSaveId left, SessionSaveId right) => left.Equals(right);
        public static bool operator !=(SessionSaveId left, SessionSaveId right) => !left.Equals(right);
    }

    public readonly struct SessionContentId : IEquatable<SessionContentId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public SessionContentId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Content id is required.", nameof(value)); Value = value.Trim(); }
        public bool Equals(SessionContentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SessionContentId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<unset-content>";
        public static bool operator ==(SessionContentId left, SessionContentId right) => left.Equals(right);
        public static bool operator !=(SessionContentId left, SessionContentId right) => !left.Equals(right);
    }

    public readonly struct SessionWorldId : IEquatable<SessionWorldId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public SessionWorldId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("World id is required.", nameof(value)); Value = value.Trim(); }
        public bool Equals(SessionWorldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SessionWorldId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<unset-world>";
        public static bool operator ==(SessionWorldId left, SessionWorldId right) => left.Equals(right);
        public static bool operator !=(SessionWorldId left, SessionWorldId right) => !left.Equals(right);
    }

    public sealed class GameSessionSnapshotHeader
    {
        public int FormatVersion { get; }
        public SessionSaveId SaveId { get; }
        public string SessionId { get; }
        public SessionContentId ContentId { get; }
        public SessionWorldId WorldId { get; }
        public ulong AuthoritativeRevision { get; }
        public long CapturedUtcTicks { get; }
        public string DisplayLabel { get; }

        public GameSessionSnapshotHeader(int formatVersion, SessionSaveId saveId, string sessionId, SessionContentId contentId, SessionWorldId worldId, ulong authoritativeRevision, long capturedUtcTicks, string displayLabel)
        {
            if (formatVersion <= 0) throw new ArgumentOutOfRangeException(nameof(formatVersion));
            if (!saveId.IsValid) throw new ArgumentException("Save id is required.", nameof(saveId));
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (!contentId.IsValid) throw new ArgumentException("Content id is required.", nameof(contentId));
            if (!worldId.IsValid) throw new ArgumentException("World id is required.", nameof(worldId));
            if (capturedUtcTicks <= 0) throw new ArgumentOutOfRangeException(nameof(capturedUtcTicks));
            FormatVersion = formatVersion; SaveId = saveId; SessionId = sessionId.Trim(); ContentId = contentId; WorldId = worldId;
            AuthoritativeRevision = authoritativeRevision; CapturedUtcTicks = capturedUtcTicks; DisplayLabel = displayLabel == null ? string.Empty : displayLabel.Trim();
        }
    }

    public sealed class SessionSectionSnapshot
    {
        private readonly byte[] _payload;
        public string SectionId { get; }
        public string SemanticType { get; }
        public int SchemaVersion { get; }
        public ulong AuthoritativeRevision { get; }
        public IReadOnlyList<byte> Payload => _payload;
        public byte[] CopyPayload() => (byte[])_payload.Clone();

        public SessionSectionSnapshot(string sectionId, string semanticType, int schemaVersion, ulong authoritativeRevision, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(sectionId)) throw new ArgumentException("Section id is required.", nameof(sectionId));
            if (!SessionSchemaGuard.IsAllowedSemanticType(semanticType)) throw new ArgumentException("Persisted section must declare an authoritative semantic type.", nameof(semanticType));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            SectionId = sectionId.Trim(); SemanticType = semanticType.Trim(); SchemaVersion = schemaVersion; AuthoritativeRevision = authoritativeRevision; _payload = (byte[])payload.Clone();
        }
    }

    public sealed class GameSessionSnapshot
    {
        private readonly SessionSectionSnapshot[] _sections;
        public GameSessionSnapshotHeader Header { get; }
        public IReadOnlyList<SessionSectionSnapshot> Sections => _sections;
        public GameSessionSnapshot(GameSessionSnapshotHeader header, IReadOnlyList<SessionSectionSnapshot> sections)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            _sections = new SessionSectionSnapshot[sections.Count];
            for (var i = 0; i < sections.Count; i++) _sections[i] = sections[i] ?? throw new ArgumentException("Session section cannot be null.", nameof(sections));
            Array.Sort(_sections, (left, right) => StringComparer.Ordinal.Compare(left.SectionId, right.SectionId));
            for (var i = 1; i < _sections.Length; i++) if (string.Equals(_sections[i - 1].SectionId, _sections[i].SectionId, StringComparison.Ordinal)) throw new ArgumentException("Duplicate session section id: " + _sections[i].SectionId, nameof(sections));
        }
    }

    public readonly struct SessionSaveMetadata
    {
        public SessionSaveId SaveId { get; }
        public string SessionId { get; }
        public SessionContentId ContentId { get; }
        public SessionWorldId WorldId { get; }
        public ulong AuthoritativeRevision { get; }
        public long CapturedUtcTicks { get; }
        public string DisplayLabel { get; }
        public SessionSaveMetadata(GameSessionSnapshotHeader header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            SaveId = header.SaveId; SessionId = header.SessionId; ContentId = header.ContentId; WorldId = header.WorldId;
            AuthoritativeRevision = header.AuthoritativeRevision; CapturedUtcTicks = header.CapturedUtcTicks; DisplayLabel = header.DisplayLabel;
        }
    }

    public enum SessionPersistenceFailure : byte
    {
        None = 0, BarrierUnavailable = 1, ContributorFailure = 2, MissingContributor = 3, DuplicateContributor = 4,
        CorruptData = 5, IncompleteSave = 6, UnsupportedSchema = 7, ContentMismatch = 8, WorldMismatch = 9,
        StorageFailure = 10, RestoreValidationFailed = 11, RestoreApplyFailed = 12, GraphUnavailable = 13
    }

    public readonly struct SessionPersistenceResult
    {
        public bool Succeeded => Failure == SessionPersistenceFailure.None;
        public SessionPersistenceFailure Failure { get; }
        public string Detail { get; }
        public SessionSaveMetadata Metadata { get; }
        public bool HasMetadata { get; }
        private SessionPersistenceResult(SessionPersistenceFailure failure, string detail, bool hasMetadata, SessionSaveMetadata metadata) { Failure = failure; Detail = detail ?? string.Empty; HasMetadata = hasMetadata; Metadata = metadata; }
        public static SessionPersistenceResult Success(SessionSaveMetadata metadata) => new SessionPersistenceResult(SessionPersistenceFailure.None, string.Empty, true, metadata);
        public static SessionPersistenceResult Reject(SessionPersistenceFailure failure, string detail = null) { if (failure == SessionPersistenceFailure.None) throw new ArgumentException("Rejected result requires a failure.", nameof(failure)); return new SessionPersistenceResult(failure, detail, false, default); }
    }

    public readonly struct SessionCaptureRequest
    {
        public SessionSaveId SaveId { get; } public string SessionId { get; } public SessionContentId ContentId { get; } public SessionWorldId WorldId { get; }
        public long CapturedUtcTicks { get; } public string DisplayLabel { get; }
        public SessionCaptureRequest(SessionSaveId saveId, string sessionId, SessionContentId contentId, SessionWorldId worldId, long capturedUtcTicks, string displayLabel = "")
        {
            if (!saveId.IsValid) throw new ArgumentException("Save id is required.", nameof(saveId)); if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (!contentId.IsValid) throw new ArgumentException("Content id is required.", nameof(contentId)); if (!worldId.IsValid) throw new ArgumentException("World id is required.", nameof(worldId)); if (capturedUtcTicks <= 0) throw new ArgumentOutOfRangeException(nameof(capturedUtcTicks));
            SaveId = saveId; SessionId = sessionId.Trim(); ContentId = contentId; WorldId = worldId; CapturedUtcTicks = capturedUtcTicks; DisplayLabel = displayLabel ?? string.Empty;
        }
    }

    public readonly struct SessionRestoreRequest
    {
        public SessionSaveId SaveId { get; } public SessionContentId ExpectedContentId { get; } public SessionWorldId ExpectedWorldId { get; }
        public SessionRestoreRequest(SessionSaveId saveId, SessionContentId expectedContentId, SessionWorldId expectedWorldId)
        {
            if (!saveId.IsValid) throw new ArgumentException("Save id is required.", nameof(saveId)); if (!expectedContentId.IsValid) throw new ArgumentException("Expected content id is required.", nameof(expectedContentId)); if (!expectedWorldId.IsValid) throw new ArgumentException("Expected world id is required.", nameof(expectedWorldId));
            SaveId = saveId; ExpectedContentId = expectedContentId; ExpectedWorldId = expectedWorldId;
        }
    }

    public readonly struct SessionContributorResult
    {
        public bool Succeeded { get; } public string Detail { get; }
        private SessionContributorResult(bool succeeded, string detail) { Succeeded = succeeded; Detail = detail ?? string.Empty; }
        public static SessionContributorResult Success() => new SessionContributorResult(true, string.Empty);
        public static SessionContributorResult Reject(string detail) => new SessionContributorResult(false, detail);
    }

    public readonly struct SessionContributorCapture
    {
        public bool Succeeded { get; } public SessionSectionSnapshot Section { get; } public string Detail { get; }
        private SessionContributorCapture(bool succeeded, SessionSectionSnapshot section, string detail) { Succeeded = succeeded; Section = section; Detail = detail ?? string.Empty; }
        public static SessionContributorCapture Success(SessionSectionSnapshot section) => new SessionContributorCapture(true, section ?? throw new ArgumentNullException(nameof(section)), string.Empty);
        public static SessionContributorCapture Reject(string detail) => new SessionContributorCapture(false, null, detail);
    }

    public interface ISessionSnapshotContributor
    {
        string SectionId { get; } int SchemaVersion { get; } int RestoreOrder { get; } bool RequiredForRestore { get; }
        SessionContributorCapture Capture(ulong authoritativeRevision); SessionContributorResult Validate(SessionSectionSnapshot section); SessionContributorResult Restore(SessionSectionSnapshot section);
    }
    public interface ISessionCaptureLease : IDisposable { ulong AuthoritativeRevision { get; } }
    public interface ISessionCaptureBarrier { bool TryEnter(out ISessionCaptureLease lease); }
    public interface ISessionSaveStore
    {
        bool TryStage(SessionSaveId saveId, byte[] payload, out string error); bool TryPublish(SessionSaveId saveId, out string error);
        bool TryReadPublished(SessionSaveId saveId, out byte[] payload, out string error); IReadOnlyList<SessionSaveId> ListPublished();
    }
    public interface ISessionRestoreGraph { IReadOnlyList<ISessionSnapshotContributor> Contributors { get; } void CompleteRestore(); void AbortRestore(); }
    public interface ISessionRestoreGraphFactory { bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error); }

    public static class SessionSchemaGuard
    {
        private static readonly string[] ForbiddenTypeTokens = { "UnityEngine", "GameObject", "Transform", "Scene", "Transport", "ConnectionId", "SteamId", "UIState", "Audio", "VFX", "AiScratch" };
        public static bool IsAllowedSemanticType(string semanticType)
        {
            if (string.IsNullOrWhiteSpace(semanticType)) return false;
            for (var i = 0; i < ForbiddenTypeTokens.Length; i++) if (semanticType.IndexOf(ForbiddenTypeTokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }
    }
}
