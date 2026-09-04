using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.Persistence.Api;

namespace Game.Persistence.Runtime
{
    public sealed class SessionPersistenceService
    {
        public const int CurrentFormatVersion = 1;
        private readonly ISessionCaptureBarrier _captureBarrier;
        private readonly ISessionSnapshotContributor[] _captureContributors;
        private readonly ISessionSaveStore _store;
        private readonly ISessionRestoreGraphFactory _graphFactory;

        public SessionPersistenceService(ISessionCaptureBarrier captureBarrier, IReadOnlyList<ISessionSnapshotContributor> captureContributors, ISessionSaveStore store, ISessionRestoreGraphFactory graphFactory)
        {
            _captureBarrier = captureBarrier ?? throw new ArgumentNullException(nameof(captureBarrier));
            if (captureContributors == null) throw new ArgumentNullException(nameof(captureContributors));
            _captureContributors = CopyAndValidateContributors(captureContributors);
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _graphFactory = graphFactory ?? throw new ArgumentNullException(nameof(graphFactory));
        }

        public SessionPersistenceResult CaptureAndSave(SessionCaptureRequest request)
        {
            if (!_captureBarrier.TryEnter(out ISessionCaptureLease lease) || lease == null)
                return SessionPersistenceResult.Reject(SessionPersistenceFailure.BarrierUnavailable, "Authoritative capture barrier is unavailable.");
            using (lease)
            {
                var sections = new SessionSectionSnapshot[_captureContributors.Length];
                for (var i = 0; i < _captureContributors.Length; i++)
                {
                    ISessionSnapshotContributor contributor = _captureContributors[i];
                    SessionContributorCapture capture = contributor.Capture(lease.AuthoritativeRevision);
                    if (!capture.Succeeded || capture.Section == null)
                        return SessionPersistenceResult.Reject(SessionPersistenceFailure.ContributorFailure, contributor.SectionId + ": " + capture.Detail);
                    if (!string.Equals(capture.Section.SectionId, contributor.SectionId, StringComparison.Ordinal) || capture.Section.SchemaVersion != contributor.SchemaVersion)
                        return SessionPersistenceResult.Reject(SessionPersistenceFailure.ContributorFailure, contributor.SectionId + ": contributor returned mismatched section metadata.");
                    if (capture.Section.AuthoritativeRevision != lease.AuthoritativeRevision)
                        return SessionPersistenceResult.Reject(SessionPersistenceFailure.ContributorFailure, contributor.SectionId + ": contributor did not capture the barrier revision.");
                    sections[i] = capture.Section;
                }

                var header = new GameSessionSnapshotHeader(CurrentFormatVersion, request.SaveId, request.SessionId, request.ContentId, request.WorldId, lease.AuthoritativeRevision, request.CapturedUtcTicks, request.DisplayLabel);
                byte[] payload;
                try { payload = SessionSnapshotBinaryCodec.Encode(new GameSessionSnapshot(header, sections)); }
                catch (Exception ex) { return SessionPersistenceResult.Reject(SessionPersistenceFailure.ContributorFailure, ex.Message); }
                if (!_store.TryStage(request.SaveId, payload, out string stageError)) return SessionPersistenceResult.Reject(SessionPersistenceFailure.StorageFailure, stageError);
                if (!_store.TryPublish(request.SaveId, out string publishError)) return SessionPersistenceResult.Reject(SessionPersistenceFailure.StorageFailure, publishError);
                return SessionPersistenceResult.Success(new SessionSaveMetadata(header));
            }
        }

        public SessionPersistenceResult Restore(SessionRestoreRequest request)
        {
            if (!_store.TryReadPublished(request.SaveId, out byte[] payload, out string readError)) return SessionPersistenceResult.Reject(SessionPersistenceFailure.StorageFailure, readError);
            if (!SessionSnapshotBinaryCodec.TryDecode(payload, out GameSessionSnapshot snapshot, out SessionPersistenceFailure decodeFailure, out string decodeDetail)) return SessionPersistenceResult.Reject(decodeFailure, decodeDetail);
            if (snapshot.Header.FormatVersion != CurrentFormatVersion) return SessionPersistenceResult.Reject(SessionPersistenceFailure.UnsupportedSchema, "Unsupported session format " + snapshot.Header.FormatVersion + ".");
            if (snapshot.Header.ContentId != request.ExpectedContentId) return SessionPersistenceResult.Reject(SessionPersistenceFailure.ContentMismatch, "Save content id does not match the requested content.");
            if (snapshot.Header.WorldId != request.ExpectedWorldId) return SessionPersistenceResult.Reject(SessionPersistenceFailure.WorldMismatch, "Save world id does not match the requested world.");
            if (!_graphFactory.TryCreate(snapshot.Header, out ISessionRestoreGraph graph, out string graphError) || graph == null) return SessionPersistenceResult.Reject(SessionPersistenceFailure.GraphUnavailable, graphError);

            bool completed = false;
            try
            {
                ISessionSnapshotContributor[] contributors = CopyAndValidateContributors(graph.Contributors);
                var byId = new Dictionary<string, ISessionSnapshotContributor>(StringComparer.Ordinal);
                for (var i = 0; i < contributors.Length; i++) byId.Add(contributors[i].SectionId, contributors[i]);
                for (var i = 0; i < snapshot.Sections.Count; i++)
                {
                    SessionSectionSnapshot section = snapshot.Sections[i];
                    if (!byId.TryGetValue(section.SectionId, out ISessionSnapshotContributor contributor)) return SessionPersistenceResult.Reject(SessionPersistenceFailure.MissingContributor, "No restore contributor for section " + section.SectionId + ".");
                    if (section.SchemaVersion != contributor.SchemaVersion) return SessionPersistenceResult.Reject(SessionPersistenceFailure.UnsupportedSchema, "Unsupported section schema for " + section.SectionId + ".");
                    SessionContributorResult validation = contributor.Validate(section);
                    if (!validation.Succeeded) return SessionPersistenceResult.Reject(SessionPersistenceFailure.RestoreValidationFailed, section.SectionId + ": " + validation.Detail);
                }
                for (var i = 0; i < contributors.Length; i++)
                {
                    if (!contributors[i].RequiredForRestore) continue;
                    bool found = false;
                    for (var s = 0; s < snapshot.Sections.Count; s++) if (string.Equals(snapshot.Sections[s].SectionId, contributors[i].SectionId, StringComparison.Ordinal)) { found = true; break; }
                    if (!found) return SessionPersistenceResult.Reject(SessionPersistenceFailure.MissingContributor, "Required section is missing: " + contributors[i].SectionId + ".");
                }
                Array.Sort(contributors, CompareRestoreOrder);
                for (var i = 0; i < contributors.Length; i++)
                {
                    SessionSectionSnapshot section = FindSection(snapshot, contributors[i].SectionId);
                    if (section == null) continue;
                    SessionContributorResult restore = contributors[i].Restore(section);
                    if (!restore.Succeeded) return SessionPersistenceResult.Reject(SessionPersistenceFailure.RestoreApplyFailed, section.SectionId + ": " + restore.Detail);
                }
                graph.CompleteRestore(); completed = true;
                return SessionPersistenceResult.Success(new SessionSaveMetadata(snapshot.Header));
            }
            catch (ArgumentException ex) { return SessionPersistenceResult.Reject(SessionPersistenceFailure.DuplicateContributor, ex.Message); }
            catch (Exception ex) { return SessionPersistenceResult.Reject(SessionPersistenceFailure.RestoreApplyFailed, ex.Message); }
            finally { if (!completed) graph.AbortRestore(); }
        }

        public IReadOnlyList<SessionSaveMetadata> ListSaves()
        {
            IReadOnlyList<SessionSaveId> ids = _store.ListPublished(); var result = new List<SessionSaveMetadata>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!_store.TryReadPublished(ids[i], out byte[] payload, out _)) continue;
                if (!SessionSnapshotBinaryCodec.TryDecode(payload, out GameSessionSnapshot snapshot, out _, out _)) continue;
                if (snapshot.Header.FormatVersion != CurrentFormatVersion) continue;
                result.Add(new SessionSaveMetadata(snapshot.Header));
            }
            result.Sort((left, right) => { int time = right.CapturedUtcTicks.CompareTo(left.CapturedUtcTicks); return time != 0 ? time : left.SaveId.CompareTo(right.SaveId); });
            return result;
        }

        private static SessionSectionSnapshot FindSection(GameSessionSnapshot snapshot, string sectionId) { for (var i = 0; i < snapshot.Sections.Count; i++) if (string.Equals(snapshot.Sections[i].SectionId, sectionId, StringComparison.Ordinal)) return snapshot.Sections[i]; return null; }
        private static int CompareRestoreOrder(ISessionSnapshotContributor left, ISessionSnapshotContributor right) { int order = left.RestoreOrder.CompareTo(right.RestoreOrder); return order != 0 ? order : StringComparer.Ordinal.Compare(left.SectionId, right.SectionId); }
        private static ISessionSnapshotContributor[] CopyAndValidateContributors(IReadOnlyList<ISessionSnapshotContributor> contributors)
        {
            var copy = new ISessionSnapshotContributor[contributors.Count]; var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < contributors.Count; i++)
            {
                ISessionSnapshotContributor contributor = contributors[i] ?? throw new ArgumentException("Session contributor cannot be null.", nameof(contributors));
                if (string.IsNullOrWhiteSpace(contributor.SectionId)) throw new ArgumentException("Session contributor section id is required.", nameof(contributors));
                if (contributor.SchemaVersion <= 0) throw new ArgumentException("Session contributor schema version must be positive.", nameof(contributors));
                if (!ids.Add(contributor.SectionId)) throw new ArgumentException("Duplicate session contributor: " + contributor.SectionId, nameof(contributors)); copy[i] = contributor;
            }
            Array.Sort(copy, (left, right) => StringComparer.Ordinal.Compare(left.SectionId, right.SectionId)); return copy;
        }
    }

    public static class SessionSnapshotBinaryCodec
    {
        private const int Magic = 0x53363156;
        private const int HashLength = 32;
        private const int MaxSections = 256;
        private const int MaxPayloadBytes = 64 * 1024 * 1024;

        public static byte[] Encode(GameSessionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot)); byte[] body;
            using (var bodyStream = new MemoryStream()) using (var writer = new BinaryWriter(bodyStream, Encoding.UTF8, true))
            {
                writer.Write(Magic); writer.Write(snapshot.Header.FormatVersion); writer.Write(snapshot.Header.SaveId.Value); writer.Write(snapshot.Header.SessionId); writer.Write(snapshot.Header.ContentId.Value); writer.Write(snapshot.Header.WorldId.Value); writer.Write(snapshot.Header.AuthoritativeRevision); writer.Write(snapshot.Header.CapturedUtcTicks); writer.Write(snapshot.Header.DisplayLabel ?? string.Empty); writer.Write(snapshot.Sections.Count);
                for (var i = 0; i < snapshot.Sections.Count; i++)
                {
                    SessionSectionSnapshot section = snapshot.Sections[i]; writer.Write(section.SectionId); writer.Write(section.SemanticType); writer.Write(section.SchemaVersion); writer.Write(section.AuthoritativeRevision); byte[] sectionPayload = section.CopyPayload(); writer.Write(sectionPayload.Length); writer.Write(sectionPayload);
                }
                writer.Flush(); body = bodyStream.ToArray();
            }
            byte[] hash; using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(body);
            using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) { writer.Write(body.Length); writer.Write(body); writer.Write(hash); writer.Flush(); return output.ToArray(); }
        }

        public static bool TryDecode(byte[] encoded, out GameSessionSnapshot snapshot, out SessionPersistenceFailure failure, out string detail)
        {
            snapshot = null; failure = SessionPersistenceFailure.CorruptData; detail = string.Empty;
            if (encoded == null || encoded.Length < sizeof(int) + HashLength) { detail = "Save payload is incomplete."; failure = SessionPersistenceFailure.IncompleteSave; return false; }
            try
            {
                using (var input = new MemoryStream(encoded, false)) using (var reader = new BinaryReader(input, Encoding.UTF8, true))
                {
                    int bodyLength = reader.ReadInt32();
                    if (bodyLength <= 0 || bodyLength > MaxPayloadBytes || input.Length - input.Position != bodyLength + HashLength) { detail = "Save payload length is invalid."; failure = SessionPersistenceFailure.IncompleteSave; return false; }
                    byte[] body = reader.ReadBytes(bodyLength); byte[] expectedHash = reader.ReadBytes(HashLength); byte[] actualHash; using (SHA256 sha = SHA256.Create()) actualHash = sha.ComputeHash(body);
                    if (!FixedTimeEquals(expectedHash, actualHash)) { detail = "Save checksum mismatch."; failure = SessionPersistenceFailure.CorruptData; return false; }
                    using (var bodyStream = new MemoryStream(body, false)) using (var bodyReader = new BinaryReader(bodyStream, Encoding.UTF8, true))
                    {
                        if (bodyReader.ReadInt32() != Magic) { detail = "Save magic is invalid."; return false; }
                        int formatVersion = bodyReader.ReadInt32(); var saveId = new SessionSaveId(bodyReader.ReadString()); string sessionId = bodyReader.ReadString(); var contentId = new SessionContentId(bodyReader.ReadString()); var worldId = new SessionWorldId(bodyReader.ReadString()); ulong revision = bodyReader.ReadUInt64(); long capturedUtcTicks = bodyReader.ReadInt64(); string displayLabel = bodyReader.ReadString(); int sectionCount = bodyReader.ReadInt32();
                        if (sectionCount < 0 || sectionCount > MaxSections) { detail = "Section count is invalid."; return false; }
                        var sections = new SessionSectionSnapshot[sectionCount];
                        for (var i = 0; i < sectionCount; i++)
                        {
                            string sectionId = bodyReader.ReadString(); string semanticType = bodyReader.ReadString(); int schemaVersion = bodyReader.ReadInt32(); ulong sectionRevision = bodyReader.ReadUInt64(); int payloadLength = bodyReader.ReadInt32();
                            if (payloadLength < 0 || payloadLength > MaxPayloadBytes || bodyStream.Length - bodyStream.Position < payloadLength) { detail = "Section payload length is invalid."; failure = SessionPersistenceFailure.IncompleteSave; return false; }
                            sections[i] = new SessionSectionSnapshot(sectionId, semanticType, schemaVersion, sectionRevision, bodyReader.ReadBytes(payloadLength));
                        }
                        if (bodyStream.Position != bodyStream.Length) { detail = "Save contains trailing data."; return false; }
                        snapshot = new GameSessionSnapshot(new GameSessionSnapshotHeader(formatVersion, saveId, sessionId, contentId, worldId, revision, capturedUtcTicks, displayLabel), sections); failure = SessionPersistenceFailure.None; return true;
                    }
                }
            }
            catch (ArgumentException ex) { detail = ex.Message; return false; }
            catch (EndOfStreamException) { detail = "Save payload ended unexpectedly."; failure = SessionPersistenceFailure.IncompleteSave; return false; }
            catch (IOException ex) { detail = ex.Message; return false; }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right) { if (left == null || right == null || left.Length != right.Length) return false; int difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0; }
    }

    public sealed class FileSessionSaveStore : ISessionSaveStore
    {
        private readonly string _rootDirectory;
        public FileSessionSaveStore(string rootDirectory) { if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Save root directory is required.", nameof(rootDirectory)); _rootDirectory = Path.GetFullPath(rootDirectory); Directory.CreateDirectory(_rootDirectory); }
        public bool TryStage(SessionSaveId saveId, byte[] payload, out string error)
        {
            error = string.Empty; if (!saveId.IsValid || payload == null) { error = "Valid save id and payload are required."; return false; }
            try { using (var stream = new FileStream(StagePath(saveId), FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(payload, 0, payload.Length); stream.Flush(true); } return true; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }
        public bool TryPublish(SessionSaveId saveId, out string error)
        {
            error = string.Empty; string stage = StagePath(saveId); string published = PublishedPath(saveId); string backup = BackupPath(saveId);
            try
            {
                if (!File.Exists(stage)) { error = "Staged save does not exist."; return false; }
                if (File.Exists(published))
                {
                    try { File.Replace(stage, published, backup, true); }
                    catch (PlatformNotSupportedException) { File.Copy(published, backup, true); File.Delete(published); File.Move(stage, published); }
                }
                else File.Move(stage, published);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }
        public bool TryReadPublished(SessionSaveId saveId, out byte[] payload, out string error)
        {
            payload = null; error = string.Empty;
            try { string path = PublishedPath(saveId); if (!File.Exists(path)) { error = "Published save does not exist."; return false; } payload = File.ReadAllBytes(path); return true; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }
        public IReadOnlyList<SessionSaveId> ListPublished()
        {
            var ids = new List<SessionSaveId>();
            try { string[] files = Directory.GetFiles(_rootDirectory, "*.vxsav", SearchOption.TopDirectoryOnly); for (var i = 0; i < files.Length; i++) if (TryDecodeFileName(Path.GetFileNameWithoutExtension(files[i]), out string id)) ids.Add(new SessionSaveId(id)); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            ids.Sort(); return ids;
        }
        private string PublishedPath(SessionSaveId id) => Path.Combine(_rootDirectory, EncodeFileName(id.Value) + ".vxsav");
        private string StagePath(SessionSaveId id) => Path.Combine(_rootDirectory, EncodeFileName(id.Value) + ".stage");
        private string BackupPath(SessionSaveId id) => Path.Combine(_rootDirectory, EncodeFileName(id.Value) + ".bak");
        private static string EncodeFileName(string value) { string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        private static bool TryDecodeFileName(string value, out string decoded)
        {
            decoded = null; try { string base64 = value.Replace('-', '+').Replace('_', '/'); switch (base64.Length % 4) { case 2: base64 += "=="; break; case 3: base64 += "="; break; case 1: return false; } decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64)); return !string.IsNullOrWhiteSpace(decoded); } catch (FormatException) { return false; }
        }
    }

    public sealed class DelegateSessionSnapshotContributor<TState> : ISessionSnapshotContributor
    {
        private readonly Func<TState> _capture; private readonly Func<TState, byte[]> _encode; private readonly Func<byte[], TState> _decode; private readonly Func<TState, SessionContributorResult> _validate; private readonly Func<TState, SessionContributorResult> _restore;
        public string SectionId { get; } public string SemanticType { get; } public int SchemaVersion { get; } public int RestoreOrder { get; } public bool RequiredForRestore { get; }
        public DelegateSessionSnapshotContributor(string sectionId, string semanticType, int schemaVersion, int restoreOrder, bool requiredForRestore, Func<TState> capture, Func<TState, byte[]> encode, Func<byte[], TState> decode, Func<TState, SessionContributorResult> validate, Func<TState, SessionContributorResult> restore)
        {
            if (string.IsNullOrWhiteSpace(sectionId)) throw new ArgumentException("Section id is required.", nameof(sectionId)); if (!SessionSchemaGuard.IsAllowedSemanticType(semanticType)) throw new ArgumentException("Semantic type is forbidden.", nameof(semanticType)); if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            SectionId = sectionId.Trim(); SemanticType = semanticType.Trim(); SchemaVersion = schemaVersion; RestoreOrder = restoreOrder; RequiredForRestore = requiredForRestore; _capture = capture ?? throw new ArgumentNullException(nameof(capture)); _encode = encode ?? throw new ArgumentNullException(nameof(encode)); _decode = decode ?? throw new ArgumentNullException(nameof(decode)); _validate = validate ?? throw new ArgumentNullException(nameof(validate)); _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }
        public SessionContributorCapture Capture(ulong authoritativeRevision) { try { TState state = _capture(); return SessionContributorCapture.Success(new SessionSectionSnapshot(SectionId, SemanticType, SchemaVersion, authoritativeRevision, _encode(state))); } catch (Exception ex) { return SessionContributorCapture.Reject(ex.Message); } }
        public SessionContributorResult Validate(SessionSectionSnapshot section) { if (!Matches(section)) return SessionContributorResult.Reject("Section metadata does not match contributor."); try { return _validate(_decode(section.CopyPayload())); } catch (Exception ex) { return SessionContributorResult.Reject(ex.Message); } }
        public SessionContributorResult Restore(SessionSectionSnapshot section) { if (!Matches(section)) return SessionContributorResult.Reject("Section metadata does not match contributor."); try { return _restore(_decode(section.CopyPayload())); } catch (Exception ex) { return SessionContributorResult.Reject(ex.Message); } }
        private bool Matches(SessionSectionSnapshot section) => section != null && string.Equals(section.SectionId, SectionId, StringComparison.Ordinal) && string.Equals(section.SemanticType, SemanticType, StringComparison.Ordinal) && section.SchemaVersion == SchemaVersion;
    }
}
