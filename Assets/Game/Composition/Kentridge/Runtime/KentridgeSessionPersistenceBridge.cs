using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Progression.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;

namespace Game.Composition.Kentridge.Runtime
{
    /// <summary>
    /// Composition adapter between system 14 lifecycle orchestration and system 16 persistence.
    /// It persists only the existing semantic CampaignRuntime snapshot; system 16 remains the owner
    /// of save publication, validation, restore ordering and content/world compatibility checks.
    /// </summary>
    public sealed class KentridgeSessionPersistenceBridge : ISessionPersistenceBridge
    {
        private readonly ISessionSaveStore _store;
        private readonly Func<long> _utcTicks;

        public KentridgeSessionPersistenceBridge(
            ISessionSaveStore store,
            Func<long> utcTicks = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcTicks = utcTicks ?? (() => DateTime.UtcNow.Ticks);
        }

        public void Restore(
            GameSessionIdentity identity,
            string restoreSourceId,
            ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (string.IsNullOrWhiteSpace(restoreSourceId))
                throw new ArgumentException("Restore source id is required.", nameof(restoreSourceId));

            KentridgeSessionRuntimeGraph kentridge = RequireGraph(graph);
            CampaignProgressContributor contributor =
                new CampaignProgressContributor(kentridge.Session.Runtime);
            SessionPersistenceService service = CreateService(kentridge, contributor);
            SessionPersistenceResult result = service.Restore(new SessionRestoreRequest(
                new SessionSaveId(restoreSourceId),
                new SessionContentId(identity.CampaignId),
                new SessionWorldId(identity.WorldId)));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "System 16 restore rejected Kentridge campaign state: " +
                    result.Failure + " " + result.Detail);
        }

        public void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            KentridgeSessionRuntimeGraph kentridge = RequireGraph(graph);
            kentridge.SettleAuthoritativeState();

            CampaignProgressContributor contributor =
                new CampaignProgressContributor(kentridge.Session.Runtime);
            SessionPersistenceService service = CreateService(kentridge, contributor);
            SessionPersistenceResult result = service.CaptureAndSave(new SessionCaptureRequest(
                new SessionSaveId(identity.SessionId),
                identity.SessionId,
                new SessionContentId(identity.CampaignId),
                new SessionWorldId(identity.WorldId),
                _utcTicks(),
                "Kentridge campaign"));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "System 16 capture rejected Kentridge campaign state: " +
                    result.Failure + " " + result.Detail);
        }

        private SessionPersistenceService CreateService(
            KentridgeSessionRuntimeGraph graph,
            CampaignProgressContributor contributor)
        {
            return new SessionPersistenceService(
                new CampaignCaptureBarrier(graph.Session.Runtime),
                new ISessionSnapshotContributor[] { contributor },
                _store,
                new ExistingGraphRestoreFactory(graph, contributor));
        }

        private static KentridgeSessionRuntimeGraph RequireGraph(ISessionRuntimeGraph graph)
        {
            KentridgeSessionRuntimeGraph kentridge = graph as KentridgeSessionRuntimeGraph;
            if (kentridge == null || kentridge.IsDisposed)
                throw new InvalidOperationException(
                    "Kentridge persistence requires the active Kentridge session runtime graph.");
            return kentridge;
        }

        private sealed class CampaignCaptureBarrier : ISessionCaptureBarrier
        {
            private readonly CampaignRuntime _runtime;

            public CampaignCaptureBarrier(CampaignRuntime runtime) =>
                _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

            public bool TryEnter(out ISessionCaptureLease lease)
            {
                CampaignProgressSnapshot snapshot = _runtime.CaptureProgress();
                ulong revision = snapshot.Progression == null ? 0UL : snapshot.Progression.Revision;
                lease = new CaptureLease(revision);
                return true;
            }
        }

        private sealed class CaptureLease : ISessionCaptureLease
        {
            public ulong AuthoritativeRevision { get; }
            public CaptureLease(ulong revision) => AuthoritativeRevision = revision;
            public void Dispose() { }
        }

        private sealed class ExistingGraphRestoreFactory : ISessionRestoreGraphFactory
        {
            private readonly KentridgeSessionRuntimeGraph _graph;
            private readonly CampaignProgressContributor _contributor;

            public ExistingGraphRestoreFactory(
                KentridgeSessionRuntimeGraph graph,
                CampaignProgressContributor contributor)
            {
                _graph = graph ?? throw new ArgumentNullException(nameof(graph));
                _contributor = contributor ?? throw new ArgumentNullException(nameof(contributor));
            }

            public bool TryCreate(
                GameSessionSnapshotHeader header,
                out ISessionRestoreGraph graph,
                out string error)
            {
                if (_graph.IsDisposed)
                {
                    graph = null;
                    error = "Kentridge runtime graph was disposed before restore.";
                    return false;
                }
                graph = new ExistingGraphRestoreGraph(_graph, _contributor);
                error = string.Empty;
                return true;
            }
        }

        private sealed class ExistingGraphRestoreGraph : ISessionRestoreGraph
        {
            private readonly KentridgeSessionRuntimeGraph _graph;
            private readonly ISessionSnapshotContributor[] _contributors;

            public ExistingGraphRestoreGraph(
                KentridgeSessionRuntimeGraph graph,
                ISessionSnapshotContributor contributor)
            {
                _graph = graph ?? throw new ArgumentNullException(nameof(graph));
                _contributors = new[] { contributor ?? throw new ArgumentNullException(nameof(contributor)) };
            }

            public IReadOnlyList<ISessionSnapshotContributor> Contributors => _contributors;

            public void CompleteRestore() => _graph.SettleAuthoritativeState();
            public void AbortRestore() { }
        }

        private sealed class CampaignProgressContributor : ISessionSnapshotContributor
        {
            private const string CampaignSectionId = "campaign.progress";
            private const string CampaignSemanticType = "CampaignProgress";
            private readonly CampaignRuntime _runtime;

            public CampaignProgressContributor(CampaignRuntime runtime) =>
                _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

            public string SectionId => CampaignSectionId;
            public int SchemaVersion => CampaignProgressCodec.SchemaVersion;
            public int RestoreOrder => 200;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                try
                {
                    byte[] payload = CampaignProgressCodec.Encode(_runtime.CaptureProgress());
                    return SessionContributorCapture.Success(new SessionSectionSnapshot(
                        SectionId,
                        CampaignSemanticType,
                        SchemaVersion,
                        authoritativeRevision,
                        payload));
                }
                catch (Exception exception)
                {
                    return SessionContributorCapture.Reject(exception.Message);
                }
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section)
            {
                if (section == null)
                    return SessionContributorResult.Reject("Campaign progress section is missing.");
                if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
                    return SessionContributorResult.Reject("Campaign progress section id is invalid.");
                if (section.SchemaVersion != SchemaVersion)
                    return SessionContributorResult.Reject("Campaign progress schema is unsupported.");
                try
                {
                    CampaignProgressCodec.Decode(section.CopyPayload());
                    return SessionContributorResult.Success();
                }
                catch (Exception exception)
                {
                    return SessionContributorResult.Reject(exception.Message);
                }
            }

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                try
                {
                    _runtime.RestoreProgress(CampaignProgressCodec.Decode(section.CopyPayload()));
                    return SessionContributorResult.Success();
                }
                catch (Exception exception)
                {
                    return SessionContributorResult.Reject(exception.Message);
                }
            }
        }
    }

    internal static class CampaignProgressCodec
    {
        public const int SchemaVersion = 1;
        private const int MaxEntries = 16384;

        public static byte[] Encode(CampaignProgressSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteCutscenes(writer, snapshot.CompletedCutscenes);
                WriteStrings(writer, snapshot.JoinedPartyMembers);
                WriteStrings(writer, snapshot.GrantedSpells);
                writer.Write(snapshot.Progression != null);
                if (snapshot.Progression != null)
                    WriteProgression(writer, snapshot.Progression);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static CampaignProgressSnapshot Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                CutsceneRef[] cutscenes = ReadCutscenes(reader);
                string[] members = ReadStrings(reader, "joined party members");
                string[] spells = ReadStrings(reader, "granted spells");
                ProgressionSnapshot progression = reader.ReadBoolean()
                    ? ReadProgression(reader)
                    : null;
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Campaign progress payload contains trailing data.");
                return new CampaignProgressSnapshot(cutscenes, members, spells, progression);
            }
        }

        private static void WriteCutscenes(
            BinaryWriter writer,
            IReadOnlyList<CutsceneRef> values)
        {
            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++) writer.Write(values[i].Id);
        }

        private static CutsceneRef[] ReadCutscenes(BinaryReader reader)
        {
            int count = ReadCount(reader, "completed cutscenes");
            var values = new CutsceneRef[count];
            for (int i = 0; i < count; i++) values[i] = new CutsceneRef(reader.ReadString());
            return values;
        }

        private static void WriteStrings(BinaryWriter writer, IReadOnlyList<string> values)
        {
            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++) writer.Write(values[i]);
        }

        private static string[] ReadStrings(BinaryReader reader, string label)
        {
            int count = ReadCount(reader, label);
            var values = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadString();
                if (string.IsNullOrWhiteSpace(values[i]))
                    throw new InvalidDataException(label + " contains an empty semantic id.");
            }
            return values;
        }

        private static void WriteProgression(BinaryWriter writer, ProgressionSnapshot snapshot)
        {
            writer.Write(snapshot.Revision);
            writer.Write(snapshot.CompatibilitySequence);
            WriteStrings(writer, snapshot.AppliedOperationIds);

            writer.Write(snapshot.StandaloneObjectives.Count);
            for (int i = 0; i < snapshot.StandaloneObjectives.Count; i++)
                WriteObjective(writer, snapshot.StandaloneObjectives[i]);

            writer.Write(snapshot.Quests.Count);
            for (int i = 0; i < snapshot.Quests.Count; i++)
            {
                QuestProgressSnapshot quest = snapshot.Quests[i];
                writer.Write(quest.Id.Value);
                writer.Write((byte)quest.State);
                writer.Write(quest.Revision);
                bool hasSteps = quest.Steps.Count > 0;
                writer.Write(hasSteps);
                if (hasSteps)
                {
                    writer.Write(quest.ActiveStepId ?? string.Empty);
                    writer.Write(quest.Steps.Count);
                    for (int s = 0; s < quest.Steps.Count; s++)
                    {
                        QuestStepProgressSnapshot step = quest.Steps[s];
                        writer.Write(step.StepId);
                        writer.Write((byte)step.State);
                        writer.Write(step.Objectives.Count);
                        for (int o = 0; o < step.Objectives.Count; o++)
                            WriteObjective(writer, step.Objectives[o]);
                    }
                }
                else
                {
                    writer.Write(quest.Objectives.Count);
                    for (int o = 0; o < quest.Objectives.Count; o++)
                        WriteObjective(writer, quest.Objectives[o]);
                }
            }
        }

        private static ProgressionSnapshot ReadProgression(BinaryReader reader)
        {
            ulong revision = reader.ReadUInt64();
            long compatibilitySequence = reader.ReadInt64();
            string[] operations = ReadStrings(reader, "applied progression operations");

            int standaloneCount = ReadCount(reader, "standalone objectives");
            var standalone = new ObjectiveProgressSnapshot[standaloneCount];
            for (int i = 0; i < standaloneCount; i++) standalone[i] = ReadObjective(reader);

            int questCount = ReadCount(reader, "quests");
            var quests = new QuestProgressSnapshot[questCount];
            for (int i = 0; i < questCount; i++)
            {
                var questId = new QuestId(reader.ReadString());
                ProgressionLifecycleState state = ReadState(reader);
                ulong questRevision = reader.ReadUInt64();
                bool hasSteps = reader.ReadBoolean();
                if (hasSteps)
                {
                    string activeStepId = reader.ReadString();
                    int stepCount = ReadCount(reader, "quest steps");
                    var steps = new QuestStepProgressSnapshot[stepCount];
                    for (int s = 0; s < stepCount; s++)
                    {
                        string stepId = reader.ReadString();
                        ProgressionLifecycleState stepState = ReadState(reader);
                        int objectiveCount = ReadCount(reader, "quest step objectives");
                        var objectives = new ObjectiveProgressSnapshot[objectiveCount];
                        for (int o = 0; o < objectiveCount; o++) objectives[o] = ReadObjective(reader);
                        steps[s] = new QuestStepProgressSnapshot(stepId, stepState, objectives);
                    }
                    quests[i] = new QuestProgressSnapshot(
                        questId, state, activeStepId, steps, questRevision);
                }
                else
                {
                    int objectiveCount = ReadCount(reader, "quest objectives");
                    var objectives = new ObjectiveProgressSnapshot[objectiveCount];
                    for (int o = 0; o < objectiveCount; o++) objectives[o] = ReadObjective(reader);
                    quests[i] = new QuestProgressSnapshot(
                        questId, state, objectives, questRevision);
                }
            }

            return new ProgressionSnapshot(
                revision,
                quests,
                standalone,
                operations,
                compatibilitySequence);
        }

        private static void WriteObjective(BinaryWriter writer, ObjectiveProgressSnapshot objective)
        {
            writer.Write(objective.Id.Value);
            writer.Write((byte)objective.State);
            writer.Write(objective.CurrentCount);
            writer.Write(objective.RequiredCount);
            writer.Write(objective.Revision);
        }

        private static ObjectiveProgressSnapshot ReadObjective(BinaryReader reader)
        {
            return new ObjectiveProgressSnapshot(
                new ObjectiveId(reader.ReadString()),
                ReadState(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadUInt64());
        }

        private static ProgressionLifecycleState ReadState(BinaryReader reader)
        {
            byte raw = reader.ReadByte();
            if (raw > (byte)ProgressionLifecycleState.Failed)
                throw new InvalidDataException("Campaign progress contains an invalid progression state.");
            return (ProgressionLifecycleState)raw;
        }

        private static int ReadCount(BinaryReader reader, string label)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
                throw new InvalidDataException(label + " count is invalid: " + count + ".");
            return count;
        }
    }
}
