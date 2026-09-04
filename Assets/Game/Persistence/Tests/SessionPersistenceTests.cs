using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Characters.Api;
using Game.Inventory.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Progression.Api;
using Game.Vitality.Api;
using Game.WorldObjects.Api;
using NUnit.Framework;

namespace Game.Persistence.Tests
{
    public sealed class SessionPersistenceTests
    {
        private const ulong Revision = 41UL;
        private static readonly SessionContentId ContentId = new SessionContentId("fixture:content-v1");
        private static readonly SessionWorldId WorldId = new SessionWorldId("fixture:world-a");

        [Test]
        public void MidRunSave_RestoresFreshGraphWithEquivalentSemanticTruth()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            SessionPersistenceResult save = service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("slot-a"), "fixture:session", ContentId, WorldId, 638900000000000000L, "Mid run"));
            Assert.That(save.Succeeded, Is.True, save.Detail);
            for (var i = 0; i < source.Count; i++) source[i].State = "mutated-after-save";
            SessionPersistenceResult restore = service.Restore(new SessionRestoreRequest(new SessionSaveId("slot-a"), ContentId, WorldId));
            Assert.That(restore.Succeeded, Is.True, restore.Detail); Assert.That(factory.LastGraph, Is.Not.Null); Assert.That(factory.LastGraph.Completed, Is.True); Assert.That(factory.LastGraph.Aborted, Is.False);
            Assert.That(factory.LastGraph.Find("characters").State, Is.EqualTo("character=fixture:hero;slot=2"));
            Assert.That(factory.LastGraph.Find("vitality").State, Is.EqualTo("fixture:hero=7/10"));
            Assert.That(factory.LastGraph.Find("inventory").State, Is.EqualTo("inventory=fixture:hero-bag;ore=3"));
            Assert.That(factory.LastGraph.Find("progression").State, Is.EqualTo("quest=fixture:road;step=2;applied=op-7"));
            Assert.That(factory.LastGraph.Find("world").State, Is.EqualTo("world=fixture:world-a;voxel-revision=41"));
            Assert.That(factory.LastGraph.Find("encounters").State, Is.EqualTo("encounter=fixture:bridge;state=active"));
            Assert.That(factory.LastGraph.Find("outcomes").State, Is.EqualTo("outcome=fixture:bridge;state=pending"));
        }

        [Test]
        public void Restore_ValidatesEverySectionBeforeApplyingAnyState()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var saveFactory = new FreshGraphFactory(source); var service = CreateService(store, source, saveFactory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("validate-first"), "fixture:session", ContentId, WorldId, 638900000000000001L)).Succeeded, Is.True);
            var restoreFactory = new FreshGraphFactory(source, "progression"); var restoreService = CreateService(store, source, restoreFactory);
            SessionPersistenceResult result = restoreService.Restore(new SessionRestoreRequest(new SessionSaveId("validate-first"), ContentId, WorldId));
            Assert.That(result.Succeeded, Is.False); Assert.That(result.Failure, Is.EqualTo(SessionPersistenceFailure.RestoreValidationFailed)); Assert.That(restoreFactory.LastGraph.Completed, Is.False); Assert.That(restoreFactory.LastGraph.Aborted, Is.True);
            for (var i = 0; i < restoreFactory.LastGraph.MutableContributors.Count; i++) Assert.That(restoreFactory.LastGraph.MutableContributors[i].ApplyCount, Is.Zero, "No contributor may apply before all validation passes.");
        }

        [Test]
        public void ActiveEncounterRestore_DoesNotReplayHistoricalOneShotEvents()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("active"), "fixture:session", ContentId, WorldId, 638900000000000002L)).Succeeded, Is.True);
            SessionPersistenceResult result = service.Restore(new SessionRestoreRequest(new SessionSaveId("active"), ContentId, WorldId));
            Assert.That(result.Succeeded, Is.True, result.Detail); MutableContributor encounter = factory.LastGraph.Find("encounters"); Assert.That(encounter.State, Does.Contain("state=active")); Assert.That(encounter.OneShotReplayCount, Is.Zero);
        }

        [Test]
        public void ResolvedOutcome_RemainsResolvedAndImmutableAfterRestore()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); source.Find("outcomes").State = "outcome=fixture:bridge;state=resolved:victory"; source.Find("outcomes").ImmutableWhenResolved = true;
            var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("resolved"), "fixture:session", ContentId, WorldId, 638900000000000003L)).Succeeded, Is.True);
            SessionPersistenceResult result = service.Restore(new SessionRestoreRequest(new SessionSaveId("resolved"), ContentId, WorldId));
            Assert.That(result.Succeeded, Is.True, result.Detail); MutableContributor outcome = factory.LastGraph.Find("outcomes"); Assert.That(outcome.State, Does.Contain("resolved:victory")); Assert.That(outcome.TrySetState("outcome=fixture:bridge;state=pending"), Is.False); Assert.That(outcome.State, Does.Contain("resolved:victory"));
        }

        [Test]
        public void CorruptOrIncompleteSave_IsRejectedBeforeGraphCreation()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("corrupt"), "fixture:session", ContentId, WorldId, 638900000000000004L)).Succeeded, Is.True); store.Corrupt(new SessionSaveId("corrupt"));
            SessionPersistenceResult corrupt = service.Restore(new SessionRestoreRequest(new SessionSaveId("corrupt"), ContentId, WorldId));
            Assert.That(corrupt.Succeeded, Is.False); Assert.That(corrupt.Failure, Is.EqualTo(SessionPersistenceFailure.CorruptData)); Assert.That(factory.CreateCount, Is.Zero);
            store.PutPublished(new SessionSaveId("short"), new byte[] { 1, 2, 3 });
            SessionPersistenceResult incomplete = service.Restore(new SessionRestoreRequest(new SessionSaveId("short"), ContentId, WorldId)); Assert.That(incomplete.Succeeded, Is.False); Assert.That(incomplete.Failure, Is.EqualTo(SessionPersistenceFailure.IncompleteSave)); Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void SchemaAndContentMismatch_SurfaceExplicitCompatibilityFailures()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            var header = new GameSessionSnapshotHeader(99, new SessionSaveId("future"), "fixture:session", ContentId, WorldId, Revision, 638900000000000005L, "");
            store.PutPublished(new SessionSaveId("future"), SessionSnapshotBinaryCodec.Encode(new GameSessionSnapshot(header, Array.Empty<SessionSectionSnapshot>())));
            SessionPersistenceResult unsupported = service.Restore(new SessionRestoreRequest(new SessionSaveId("future"), ContentId, WorldId)); Assert.That(unsupported.Failure, Is.EqualTo(SessionPersistenceFailure.UnsupportedSchema)); Assert.That(factory.CreateCount, Is.Zero);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("content"), "fixture:session", ContentId, WorldId, 638900000000000006L)).Succeeded, Is.True);
            SessionPersistenceResult mismatch = service.Restore(new SessionRestoreRequest(new SessionSaveId("content"), new SessionContentId("fixture:other-content"), WorldId)); Assert.That(mismatch.Failure, Is.EqualTo(SessionPersistenceFailure.ContentMismatch)); Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void StagedFile_IsNeverListedUntilAtomicPublication()
        {
            string root = Path.Combine(Path.GetTempPath(), "voxel-persistence-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileSessionSaveStore(root); var id = new SessionSaveId("atomic-slot");
                Assert.That(store.TryStage(id, new byte[] { 9, 8, 7 }, out string stageError), Is.True, stageError); Assert.That(store.ListPublished(), Is.Empty);
                Assert.That(store.TryPublish(id, out string publishError), Is.True, publishError); Assert.That(store.ListPublished(), Has.Count.EqualTo(1)); Assert.That(store.ListPublished()[0], Is.EqualTo(id));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ListingUsesHeaderMetadataWithoutCreatingGameplayGraph()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); var factory = new FreshGraphFactory(source); var service = CreateService(store, source, factory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("list-me"), "fixture:session", ContentId, WorldId, 638900000000000007L, "Continue here")).Succeeded, Is.True);
            IReadOnlyList<SessionSaveMetadata> saves = service.ListSaves(); Assert.That(saves, Has.Count.EqualTo(1)); Assert.That(saves[0].SaveId, Is.EqualTo(new SessionSaveId("list-me"))); Assert.That(saves[0].DisplayLabel, Is.EqualTo("Continue here")); Assert.That(saves[0].AuthoritativeRevision, Is.EqualTo(Revision)); Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void MultiplayerRehost_PreservesGameplayIdsButNotTransportConnection()
        {
            var store = new MemoryStore(); var source = CreateMixedContributors(); source.Find("characters").State = "character=fixture:hero;slot=2;inventory=fixture:hero-bag";
            var factory = new FreshGraphFactory(source) { NewTransportConnectionId = "connection:new-host" }; var service = CreateService(store, source, factory);
            Assert.That(service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("rehost"), "fixture:session", ContentId, WorldId, 638900000000000008L)).Succeeded, Is.True);
            SessionPersistenceResult result = service.Restore(new SessionRestoreRequest(new SessionSaveId("rehost"), ContentId, WorldId));
            Assert.That(result.Succeeded, Is.True, result.Detail); Assert.That(factory.LastGraph.Find("characters").State, Does.Contain("character=fixture:hero")); Assert.That(factory.LastGraph.Find("characters").State, Does.Contain("slot=2")); Assert.That(factory.LastGraph.Find("characters").State, Does.Contain("inventory=fixture:hero-bag")); Assert.That(factory.LastGraph.TransportConnectionId, Is.EqualTo("connection:new-host")); Assert.That(factory.LastGraph.Find("characters").State, Does.Not.Contain("connection:"));
        }

        [Test]
        public void SchemaGuard_RejectsUnityTransportAndPresentationTypes()
        {
            Assert.That(SessionSchemaGuard.IsAllowedSemanticType("Game.Inventory.Api.InventoryStateCapture"), Is.True); Assert.That(SessionSchemaGuard.IsAllowedSemanticType("UnityEngine.GameObject"), Is.False); Assert.That(SessionSchemaGuard.IsAllowedSemanticType("Game.Network.TransportConnectionId"), Is.False); Assert.That(SessionSchemaGuard.IsAllowedSemanticType("Game.UIState.SavePanel"), Is.False); Assert.That(SessionSchemaGuard.IsAllowedSemanticType("Game.Audio.OneShotState"), Is.False);
            Assert.Throws<ArgumentException>(() => new SessionSectionSnapshot("bad", "UnityEngine.Transform", 1, Revision, Array.Empty<byte>()));
        }

        [Test]
        public void AuthoritativeSubsystemPublicSnapshotTypes_AreSemanticPersistenceInputs()
        {
            Assert.That(SessionSchemaGuard.IsAllowedSemanticType(typeof(CharacterRegistryState).FullName), Is.True); Assert.That(SessionSchemaGuard.IsAllowedSemanticType(typeof(VitalitySnapshot).FullName), Is.True); Assert.That(SessionSchemaGuard.IsAllowedSemanticType(typeof(InventoryStateCapture).FullName), Is.True); Assert.That(SessionSchemaGuard.IsAllowedSemanticType(typeof(ProgressionSnapshot).FullName), Is.True); Assert.That(SessionSchemaGuard.IsAllowedSemanticType(typeof(WorldObjectStateSnapshot).FullName), Is.True);
        }

        [Test]
        public void CaptureBarrierMismatch_IsRejectedInsteadOfPublishingMixedRevision()
        {
            var store = new MemoryStore(); var contributors = CreateMixedContributors(); contributors.Find("inventory").CapturedRevisionOverride = Revision - 1; var factory = new FreshGraphFactory(contributors); var service = CreateService(store, contributors, factory);
            SessionPersistenceResult result = service.CaptureAndSave(new SessionCaptureRequest(new SessionSaveId("mixed"), "fixture:session", ContentId, WorldId, 638900000000000009L)); Assert.That(result.Succeeded, Is.False); Assert.That(result.Failure, Is.EqualTo(SessionPersistenceFailure.ContributorFailure)); Assert.That(store.ListPublished(), Is.Empty);
        }

        private static SessionPersistenceService CreateService(MemoryStore store, IReadOnlyList<MutableContributor> contributors, FreshGraphFactory factory) => new SessionPersistenceService(new FixedBarrier(Revision), ToInterfaceList(contributors), store, factory);
        private static List<ISessionSnapshotContributor> ToInterfaceList(IReadOnlyList<MutableContributor> contributors) { var list = new List<ISessionSnapshotContributor>(contributors.Count); for (var i = 0; i < contributors.Count; i++) list.Add(contributors[i]); return list; }
        private static ContributorList CreateMixedContributors() => new ContributorList
        {
            New("characters", "Game.Characters.Api.CharacterRegistryState", 10, "character=fixture:hero;slot=2"),
            New("vitality", "Game.Vitality.Api.VitalitySnapshotSet", 20, "fixture:hero=7/10"),
            New("inventory", "Game.Inventory.Api.InventoryStateCapture", 30, "inventory=fixture:hero-bag;ore=3"),
            New("progression", "Game.Progression.Api.ProgressionSnapshot", 40, "quest=fixture:road;step=2;applied=op-7"),
            New("world", "Game.World.SemanticWorldSnapshot", 50, "world=fixture:world-a;voxel-revision=41"),
            New("encounters", "Game.Encounters.Api.EncounterStateSet", 60, "encounter=fixture:bridge;state=active"),
            New("outcomes", "Game.Sessions.Api.SessionOutcomeState", 70, "outcome=fixture:bridge;state=pending")
        };
        private static MutableContributor New(string id, string semanticType, int order, string state) => new MutableContributor(id, semanticType, order, state);

        private sealed class FixedBarrier : ISessionCaptureBarrier
        {
            private readonly ulong _revision; public FixedBarrier(ulong revision) { _revision = revision; }
            public bool TryEnter(out ISessionCaptureLease lease) { lease = new Lease(_revision); return true; }
            private sealed class Lease : ISessionCaptureLease { public ulong AuthoritativeRevision { get; } public Lease(ulong revision) { AuthoritativeRevision = revision; } public void Dispose() { } }
        }

        private sealed class MemoryStore : ISessionSaveStore
        {
            private readonly Dictionary<SessionSaveId, byte[]> _staged = new Dictionary<SessionSaveId, byte[]>(); private readonly Dictionary<SessionSaveId, byte[]> _published = new Dictionary<SessionSaveId, byte[]>();
            public bool TryStage(SessionSaveId saveId, byte[] payload, out string error) { error = string.Empty; _staged[saveId] = (byte[])payload.Clone(); return true; }
            public bool TryPublish(SessionSaveId saveId, out string error) { error = string.Empty; if (!_staged.TryGetValue(saveId, out byte[] payload)) { error = "missing stage"; return false; } _published[saveId] = (byte[])payload.Clone(); _staged.Remove(saveId); return true; }
            public bool TryReadPublished(SessionSaveId saveId, out byte[] payload, out string error) { error = string.Empty; if (!_published.TryGetValue(saveId, out byte[] stored)) { payload = null; error = "missing"; return false; } payload = (byte[])stored.Clone(); return true; }
            public IReadOnlyList<SessionSaveId> ListPublished() { var ids = new List<SessionSaveId>(_published.Keys); ids.Sort(); return ids; }
            public void PutPublished(SessionSaveId id, byte[] payload) => _published[id] = (byte[])payload.Clone();
            public void Corrupt(SessionSaveId id) { byte[] copy = (byte[])_published[id].Clone(); copy[copy.Length / 2] ^= 0x5A; _published[id] = copy; }
        }

        private sealed class MutableContributor : ISessionSnapshotContributor
        {
            public string SectionId { get; } public string SemanticType { get; } public int SchemaVersion => 1; public int RestoreOrder { get; } public bool RequiredForRestore => true; public string State { get; set; } public bool FailValidation { get; set; } public int ValidateCount { get; private set; } public int ApplyCount { get; private set; } public int OneShotReplayCount { get; private set; } public ulong? CapturedRevisionOverride { get; set; } public bool ImmutableWhenResolved { get; set; }
            public MutableContributor(string sectionId, string semanticType, int restoreOrder, string state) { SectionId = sectionId; SemanticType = semanticType; RestoreOrder = restoreOrder; State = state; }
            public SessionContributorCapture Capture(ulong authoritativeRevision) { ulong captured = CapturedRevisionOverride ?? authoritativeRevision; return SessionContributorCapture.Success(new SessionSectionSnapshot(SectionId, SemanticType, SchemaVersion, captured, Encoding.UTF8.GetBytes(State ?? string.Empty))); }
            public SessionContributorResult Validate(SessionSectionSnapshot section) { ValidateCount++; if (FailValidation) return SessionContributorResult.Reject("fixture validation failure"); return SessionContributorResult.Success(); }
            public SessionContributorResult Restore(SessionSectionSnapshot section) { ApplyCount++; State = Encoding.UTF8.GetString(section.CopyPayload()); return SessionContributorResult.Success(); }
            public bool TrySetState(string state) { if (ImmutableWhenResolved && State != null && State.Contains("state=resolved:")) return false; State = state; return true; }
            public MutableContributor CloneFresh() => new MutableContributor(SectionId, SemanticType, RestoreOrder, string.Empty) { FailValidation = FailValidation, ImmutableWhenResolved = ImmutableWhenResolved };
        }

        private sealed class ContributorList : List<MutableContributor>
        {
            public MutableContributor Find(string id) { for (var i = 0; i < Count; i++) if (string.Equals(this[i].SectionId, id, StringComparison.Ordinal)) return this[i]; throw new InvalidOperationException("Missing fixture contributor " + id); }
        }

        private sealed class FreshGraphFactory : ISessionRestoreGraphFactory
        {
            private readonly IReadOnlyList<MutableContributor> _templates; private readonly string _failValidationSection; public int CreateCount { get; private set; } public FreshGraph LastGraph { get; private set; } public string NewTransportConnectionId { get; set; } = "connection:fresh";
            public FreshGraphFactory(IReadOnlyList<MutableContributor> templates, string failValidationSection = null) { _templates = templates; _failValidationSection = failValidationSection; }
            public bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error)
            {
                CreateCount++; var contributors = new ContributorList();
                for (var i = 0; i < _templates.Count; i++) { MutableContributor fresh = _templates[i].CloneFresh(); if (string.Equals(fresh.SectionId, _failValidationSection, StringComparison.Ordinal)) fresh.FailValidation = true; contributors.Add(fresh); }
                LastGraph = new FreshGraph(contributors, NewTransportConnectionId); graph = LastGraph; error = string.Empty; return true;
            }
        }

        private sealed class FreshGraph : ISessionRestoreGraph
        {
            public ContributorList MutableContributors { get; } public string TransportConnectionId { get; } public bool Completed { get; private set; } public bool Aborted { get; private set; } public IReadOnlyList<ISessionSnapshotContributor> Contributors => ToInterfaceList(MutableContributors);
            public FreshGraph(ContributorList contributors, string transportConnectionId) { MutableContributors = contributors; TransportConnectionId = transportConnectionId; }
            public MutableContributor Find(string id) => MutableContributors.Find(id); public void CompleteRestore() { Completed = true; } public void AbortRestore() { Aborted = true; }
        }
    }
}
