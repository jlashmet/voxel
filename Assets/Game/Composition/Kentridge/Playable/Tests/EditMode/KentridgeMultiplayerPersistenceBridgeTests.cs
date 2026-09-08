using System;
using System.Collections.Generic;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeMultiplayerPersistenceBridgeTests
    {
        [Test]
        public void CaptureAndFreshRestoreUseSystem16StoreAndSemanticContributors()
        {
            var state = new MutableContributor(42);
            var store = new MemorySaveStore();
            int completeCalls = 0;
            var bridge = new KentridgeMultiplayerPersistenceBridge(
                () => new ISessionSnapshotContributor[] { state },
                store,
                new SessionSaveId("rehost-save"),
                () => 17,
                () => completeCalls++,
                () => 123456789);
            var identity = new GameSessionIdentity("campaign", "world", "session", "configuration");
            var graph = new FixtureGraph();

            bridge.Capture(identity, graph);
            IReadOnlyList<SessionSaveMetadata> saves = bridge.ListSaves();
            Assert.That(saves, Has.Count.EqualTo(1));
            Assert.That(saves[0].SaveId.Value, Is.EqualTo("rehost-save"));
            Assert.That(saves[0].SessionId, Is.EqualTo("session"));
            Assert.That(saves[0].AuthoritativeRevision, Is.EqualTo(17));

            state.Value = 0;
            bridge.Restore(identity, "rehost-save", graph);

            Assert.That(state.Value, Is.EqualTo(42));
            Assert.That(completeCalls, Is.EqualTo(1));
        }

        private sealed class MutableContributor : ISessionSnapshotContributor
        {
            public MutableContributor(int value) => Value = value;
            public int Value { get; set; }
            public string SectionId => "fixture-state";
            public int SchemaVersion => 1;
            public int RestoreOrder => 0;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision) =>
                SessionContributorCapture.Success(new SessionSectionSnapshot(
                    SectionId,
                    "Game.Progression.FixtureState",
                    SchemaVersion,
                    authoritativeRevision,
                    BitConverter.GetBytes(Value)));

            public SessionContributorResult Validate(SessionSectionSnapshot section) =>
                section != null && section.CopyPayload().Length == sizeof(int)
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject("Fixture section is invalid.");

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                byte[] payload = section.CopyPayload();
                if (payload.Length != sizeof(int)) return SessionContributorResult.Reject("Fixture payload is invalid.");
                Value = BitConverter.ToInt32(payload, 0);
                return SessionContributorResult.Success();
            }
        }

        private sealed class MemorySaveStore : ISessionSaveStore
        {
            private readonly Dictionary<SessionSaveId, byte[]> _staged = new Dictionary<SessionSaveId, byte[]>();
            private readonly Dictionary<SessionSaveId, byte[]> _published = new Dictionary<SessionSaveId, byte[]>();

            public bool TryStage(SessionSaveId saveId, byte[] payload, out string error)
            {
                _staged[saveId] = (byte[])payload.Clone();
                error = string.Empty;
                return true;
            }

            public bool TryPublish(SessionSaveId saveId, out string error)
            {
                if (!_staged.TryGetValue(saveId, out byte[] payload))
                {
                    error = "No staged payload.";
                    return false;
                }
                _published[saveId] = (byte[])payload.Clone();
                _staged.Remove(saveId);
                error = string.Empty;
                return true;
            }

            public bool TryReadPublished(SessionSaveId saveId, out byte[] payload, out string error)
            {
                if (!_published.TryGetValue(saveId, out byte[] stored))
                {
                    payload = null;
                    error = "Save not found.";
                    return false;
                }
                payload = (byte[])stored.Clone();
                error = string.Empty;
                return true;
            }

            public IReadOnlyList<SessionSaveId> ListPublished()
            {
                var ids = new List<SessionSaveId>(_published.Keys);
                ids.Sort();
                return ids;
            }
        }

        private sealed class FixtureGraph : ISessionRuntimeGraph
        {
            public bool GameplayBindingsReady => true;
            public IReadOnlyList<ISessionUpdateStep> UpdateSteps => Array.Empty<ISessionUpdateStep>();
            public IGameOutcomeQuery OutcomeQuery => FixtureOutcome.Instance;
            public void InitializeNewGame() { }
            public void StartCommands() { }
            public void StopCommands() { }
            public void SettleAuthoritativeState() { }
            public void DetachExternalAdapters() { }
            public void Dispose() { }
        }

        private sealed class FixtureOutcome : IGameOutcomeQuery
        {
            public static readonly FixtureOutcome Instance = new FixtureOutcome();
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
        }
    }
}
