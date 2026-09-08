using System;
using System.Collections.Generic;
using System.IO;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Runtime;
using Game.Inventory.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Progression.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production composition bridge between SessionOrchestration (System 14) and Persistence
    /// (System 16). The bridge owns no gameplay state: contributors capture/restore the same
    /// authoritative module stores used by the running session, while the save store is durable
    /// across process lifetimes.
    /// </summary>
    public sealed class KentridgeMultiplayerPersistenceBridge : ISessionPersistenceBridge, ISessionSaveCatalog
    {
        private readonly Func<IReadOnlyList<ISessionSnapshotContributor>> _contributors;
        private readonly ISessionSaveStore _store;
        private readonly SessionSaveId _captureSaveId;
        private readonly Func<ulong> _authoritativeRevision;
        private readonly Func<long> _utcTicks;
        private readonly Action _completeRestore;

        public KentridgeMultiplayerPersistenceBridge(
            Func<IReadOnlyList<ISessionSnapshotContributor>> contributors,
            ISessionSaveStore store,
            SessionSaveId captureSaveId,
            Func<ulong> authoritativeRevision,
            Action completeRestore = null,
            Func<long> utcTicks = null)
        {
            _contributors = contributors ?? throw new ArgumentNullException(nameof(contributors));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (!captureSaveId.IsValid) throw new ArgumentException("Capture save id is required.", nameof(captureSaveId));
            _captureSaveId = captureSaveId;
            _authoritativeRevision = authoritativeRevision ?? throw new ArgumentNullException(nameof(authoritativeRevision));
            _completeRestore = completeRestore;
            _utcTicks = utcTicks ?? (() => DateTime.UtcNow.Ticks);
        }

        public IReadOnlyList<SessionSaveMetadata> ListSaves() =>
            CreateService(Array.Empty<ISessionSnapshotContributor>(), UnavailableRestoreFactory.Instance).ListSaves();

        public void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            IReadOnlyList<ISessionSnapshotContributor> contributors = RequireContributors();
            var service = CreateService(contributors, UnavailableRestoreFactory.Instance);
            SessionPersistenceResult result = service.CaptureAndSave(new SessionCaptureRequest(
                _captureSaveId,
                identity.SessionId,
                new SessionContentId(identity.CampaignId),
                new SessionWorldId(identity.WorldId),
                RequireUtcTicks(),
                "Multiplayer authoritative rehost"));
            RequireSuccess(result, "capture");
        }

        public void Restore(GameSessionIdentity identity, string restoreSourceId, ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var saveId = new SessionSaveId(restoreSourceId);
            var graphFactory = new CurrentRestoreGraphFactory(_contributors, _completeRestore);
            var service = CreateService(Array.Empty<ISessionSnapshotContributor>(), graphFactory);
            SessionPersistenceResult result = service.Restore(new SessionRestoreRequest(
                saveId,
                new SessionContentId(identity.CampaignId),
                new SessionWorldId(identity.WorldId)));
            RequireSuccess(result, "restore");
        }

        private SessionPersistenceService CreateService(
            IReadOnlyList<ISessionSnapshotContributor> captureContributors,
            ISessionRestoreGraphFactory graphFactory) =>
            new SessionPersistenceService(
                new RevisionCaptureBarrier(_authoritativeRevision),
                captureContributors,
                _store,
                graphFactory);

        private IReadOnlyList<ISessionSnapshotContributor> RequireContributors()
        {
            IReadOnlyList<ISessionSnapshotContributor> contributors = _contributors();
            if (contributors == null || contributors.Count == 0)
                throw new InvalidOperationException("Kentridge multiplayer persistence has no authoritative contributors.");
            return contributors;
        }

        private long RequireUtcTicks()
        {
            long ticks = _utcTicks();
            if (ticks <= 0) throw new InvalidOperationException("Persistence clock returned an invalid UTC tick value.");
            return ticks;
        }

        private static void RequireSuccess(SessionPersistenceResult result, string operation)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Kentridge multiplayer persistence " + operation + " failed: " + result.Failure + " " + result.Detail);
        }

        private sealed class RevisionCaptureBarrier : ISessionCaptureBarrier
        {
            private readonly Func<ulong> _revision;
            public RevisionCaptureBarrier(Func<ulong> revision) => _revision = revision;
            public bool TryEnter(out ISessionCaptureLease lease)
            {
                lease = new Lease(_revision());
                return true;
            }

            private sealed class Lease : ISessionCaptureLease
            {
                public Lease(ulong revision) => AuthoritativeRevision = revision;
                public ulong AuthoritativeRevision { get; }
                public void Dispose() { }
            }
        }

        private sealed class CurrentRestoreGraphFactory : ISessionRestoreGraphFactory
        {
            private readonly Func<IReadOnlyList<ISessionSnapshotContributor>> _contributors;
            private readonly Action _completeRestore;

            public CurrentRestoreGraphFactory(
                Func<IReadOnlyList<ISessionSnapshotContributor>> contributors,
                Action completeRestore)
            {
                _contributors = contributors;
                _completeRestore = completeRestore;
            }

            public bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error)
            {
                IReadOnlyList<ISessionSnapshotContributor> contributors = _contributors();
                if (contributors == null || contributors.Count == 0)
                {
                    graph = null;
                    error = "The resumed Kentridge session exposed no authoritative persistence contributors.";
                    return false;
                }
                graph = new CurrentRestoreGraph(contributors, _completeRestore);
                error = string.Empty;
                return true;
            }
        }

        private sealed class CurrentRestoreGraph : ISessionRestoreGraph
        {
            private readonly IReadOnlyList<ISessionSnapshotContributor> _contributors;
            private readonly Action _completeRestore;

            public CurrentRestoreGraph(IReadOnlyList<ISessionSnapshotContributor> contributors, Action completeRestore)
            {
                _contributors = contributors;
                _completeRestore = completeRestore;
            }

            public IReadOnlyList<ISessionSnapshotContributor> Contributors => _contributors;
            public void CompleteRestore() => _completeRestore?.Invoke();
            public void AbortRestore() { }
        }

        private sealed class UnavailableRestoreFactory : ISessionRestoreGraphFactory
        {
            public static readonly UnavailableRestoreFactory Instance = new UnavailableRestoreFactory();
            public bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error)
            {
                graph = null;
                error = "Restore graph is unavailable for this operation.";
                return false;
            }
        }
    }

    /// <summary>
    /// Kentridge's semantic save contributors. Inventory and progression are restored through their
    /// existing authoritative state ports; transport/session connection identity is intentionally absent.
    /// </summary>
    public static class KentridgeCampaignPersistenceContributors
    {
        public static IReadOnlyList<ISessionSnapshotContributor> Create(Func<KentridgeCampaignSession> session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return Array.AsReadOnly<ISessionSnapshotContributor>(new ISessionSnapshotContributor[]
            {
                new ProgressionContributor(session),
                new InventoryContributor(session)
            });
        }

        private sealed class InventoryContributor : ISessionSnapshotContributor
        {
            private const string SemanticType = "Game.Inventory.InventoryStateCapture";
            private readonly Func<KentridgeCampaignSession> _session;
            public InventoryContributor(Func<KentridgeCampaignSession> session) => _session = session;
            public string SectionId => "kentridge.inventory";
            public int SchemaVersion => 1;
            public int RestoreOrder => 20;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                try
                {
                    InventoryStateCapture state = RequireSession().InventoryState.CaptureState();
                    return SessionContributorCapture.Success(new SessionSectionSnapshot(
                        SectionId, SemanticType, SchemaVersion, authoritativeRevision, EncodeInventory(state)));
                }
                catch (Exception exception)
                {
                    return SessionContributorCapture.Reject(exception.Message);
                }
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section)
            {
                if (!TryDecodeInventory(section, out _, out string error))
                    return SessionContributorResult.Reject(error);
                return SessionContributorResult.Success();
            }

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                if (!TryDecodeInventory(section, out InventoryStateCapture state, out string error))
                    return SessionContributorResult.Reject(error);
                InventoryFailureReason failure = RequireSession().InventoryState.RestoreState(state);
                return failure == InventoryFailureReason.None
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject("Inventory restore rejected: " + failure);
            }

            private KentridgeCampaignSession RequireSession() =>
                _session() ?? throw new InvalidOperationException("Kentridge campaign session is not composed.");
        }

        private sealed class ProgressionContributor : ISessionSnapshotContributor
        {
            private const string SemanticType = "Game.Progression.ProgressionSnapshot";
            private readonly Func<KentridgeCampaignSession> _session;
            public ProgressionContributor(Func<KentridgeCampaignSession> session) => _session = session;
            public string SectionId => "kentridge.progression";
            public int SchemaVersion => 1;
            public int RestoreOrder => 10;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                try
                {
                    ProgressionSnapshot state = RequireSession().Runtime.Progression.Snapshot();
                    return SessionContributorCapture.Success(new SessionSectionSnapshot(
                        SectionId, SemanticType, SchemaVersion, authoritativeRevision, EncodeProgression(state)));
                }
                catch (Exception exception)
                {
                    return SessionContributorCapture.Reject(exception.Message);
                }
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section)
            {
                if (!TryDecodeProgression(section, out _, out string error))
                    return SessionContributorResult.Reject(error);
                return SessionContributorResult.Success();
            }

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                if (!TryDecodeProgression(section, out ProgressionSnapshot state, out string error))
                    return SessionContributorResult.Reject(error);
                KentridgeCampaignSession session = RequireSession();
                CampaignProgressSnapshot baseline = session.Runtime.CaptureProgress();
                session.Runtime.RestoreProgress(new CampaignProgressSnapshot(
                    baseline.CompletedCutscenes,
                    baseline.JoinedPartyMembers,
                    baseline.GrantedSpells,
                    state));
                return SessionContributorResult.Success();
            }

            private KentridgeCampaignSession RequireSession() =>
                _session() ?? throw new InvalidOperationException("Kentridge campaign session is not composed.");
        }

        private static byte[] EncodeInventory(InventoryStateCapture state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(state.Inventories.Count);
                for (int i = 0; i < state.Inventories.Count; i++)
                {
                    InventorySnapshot inventory = state.Inventories[i];
                    writer.Write(inventory.Id.Value);
                    writer.Write(inventory.Revision);
                    writer.Write(inventory.Entries.Count);
                    for (int e = 0; e < inventory.Entries.Count; e++)
                    {
                        writer.Write(inventory.Entries[e].Item.Id);
                        writer.Write(inventory.Entries[e].Quantity);
                    }
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static bool TryDecodeInventory(
            SessionSectionSnapshot section,
            out InventoryStateCapture state,
            out string error)
        {
            state = default;
            error = string.Empty;
            if (!ValidateSection(section, "kentridge.inventory", "Game.Inventory.InventoryStateCapture", out error))
                return false;
            try
            {
                using (var stream = new MemoryStream(section.CopyPayload(), false))
                using (var reader = new BinaryReader(stream))
                {
                    int count = ReadCount(reader, "inventory count");
                    var inventories = new InventorySnapshot[count];
                    for (int i = 0; i < count; i++)
                    {
                        var id = new InventoryId(reader.ReadString());
                        ulong revision = reader.ReadUInt64();
                        int entryCount = ReadCount(reader, "inventory entry count");
                        var entries = new InventoryEntry[entryCount];
                        for (int e = 0; e < entryCount; e++)
                            entries[e] = new InventoryEntry(new ItemRef(reader.ReadString()), reader.ReadInt32());
                        inventories[i] = new InventorySnapshot(id, revision, entries);
                    }
                    RequireEnd(stream);
                    state = new InventoryStateCapture(inventories);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                               exception is ArgumentOutOfRangeException || exception is EndOfStreamException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static byte[] EncodeProgression(ProgressionSnapshot state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(state.Revision);
                writer.Write(state.Quests.Count);
                for (int i = 0; i < state.Quests.Count; i++)
                {
                    QuestProgressSnapshot quest = state.Quests[i];
                    writer.Write(quest.Id.Value);
                    writer.Write((byte)quest.State);
                    writer.Write(quest.ActiveStepId ?? string.Empty);
                    writer.Write(quest.Revision);
                    writer.Write(quest.Steps.Count);
                    for (int s = 0; s < quest.Steps.Count; s++)
                    {
                        QuestStepProgressSnapshot step = quest.Steps[s];
                        writer.Write(step.StepId);
                        writer.Write((byte)step.State);
                        WriteObjectives(writer, step.Objectives);
                    }
                }
                WriteObjectives(writer, state.StandaloneObjectives);
                writer.Write(state.AppliedOperationIds.Count);
                for (int i = 0; i < state.AppliedOperationIds.Count; i++) writer.Write(state.AppliedOperationIds[i]);
                writer.Write(state.CompatibilitySequence);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static bool TryDecodeProgression(
            SessionSectionSnapshot section,
            out ProgressionSnapshot state,
            out string error)
        {
            state = null;
            error = string.Empty;
            if (!ValidateSection(section, "kentridge.progression", "Game.Progression.ProgressionSnapshot", out error))
                return false;
            try
            {
                using (var stream = new MemoryStream(section.CopyPayload(), false))
                using (var reader = new BinaryReader(stream))
                {
                    ulong revision = reader.ReadUInt64();
                    int questCount = ReadCount(reader, "quest count");
                    var quests = new QuestProgressSnapshot[questCount];
                    for (int i = 0; i < questCount; i++)
                    {
                        var id = new QuestId(reader.ReadString());
                        var lifecycle = (ProgressionLifecycleState)reader.ReadByte();
                        string activeStep = reader.ReadString();
                        ulong questRevision = reader.ReadUInt64();
                        int stepCount = ReadCount(reader, "quest step count");
                        var steps = new QuestStepProgressSnapshot[stepCount];
                        for (int s = 0; s < stepCount; s++)
                            steps[s] = new QuestStepProgressSnapshot(
                                reader.ReadString(),
                                (ProgressionLifecycleState)reader.ReadByte(),
                                ReadObjectives(reader));
                        quests[i] = new QuestProgressSnapshot(id, lifecycle, activeStep, steps, questRevision);
                    }
                    ObjectiveProgressSnapshot[] standalone = ReadObjectives(reader);
                    int appliedCount = ReadCount(reader, "applied operation count");
                    var applied = new string[appliedCount];
                    for (int i = 0; i < appliedCount; i++) applied[i] = reader.ReadString();
                    long compatibilitySequence = reader.ReadInt64();
                    RequireEnd(stream);
                    state = new ProgressionSnapshot(
                        revision, quests, standalone, applied, compatibilitySequence);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                               exception is ArgumentOutOfRangeException || exception is EndOfStreamException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void WriteObjectives(BinaryWriter writer, IReadOnlyList<ObjectiveProgressSnapshot> objectives)
        {
            writer.Write(objectives.Count);
            for (int i = 0; i < objectives.Count; i++)
            {
                ObjectiveProgressSnapshot objective = objectives[i];
                writer.Write(objective.Id.Value);
                writer.Write((byte)objective.State);
                writer.Write(objective.CurrentCount);
                writer.Write(objective.RequiredCount);
                writer.Write(objective.Revision);
            }
        }

        private static ObjectiveProgressSnapshot[] ReadObjectives(BinaryReader reader)
        {
            int count = ReadCount(reader, "objective count");
            var objectives = new ObjectiveProgressSnapshot[count];
            for (int i = 0; i < count; i++)
                objectives[i] = new ObjectiveProgressSnapshot(
                    new ObjectiveId(reader.ReadString()),
                    (ProgressionLifecycleState)reader.ReadByte(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadUInt64());
            return objectives;
        }

        private static int ReadCount(BinaryReader reader, string label)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 100000)
                throw new InvalidDataException(label + " is invalid: " + count);
            return count;
        }

        private static void RequireEnd(Stream stream)
        {
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Persistence section contains trailing bytes.");
        }

        private static bool ValidateSection(
            SessionSectionSnapshot section,
            string expectedId,
            string expectedSemanticType,
            out string error)
        {
            if (section == null)
            {
                error = "Persistence section is missing.";
                return false;
            }
            if (!string.Equals(section.SectionId, expectedId, StringComparison.Ordinal) ||
                !string.Equals(section.SemanticType, expectedSemanticType, StringComparison.Ordinal) ||
                section.SchemaVersion != 1)
            {
                error = "Persistence section metadata does not match the Kentridge contributor contract.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
