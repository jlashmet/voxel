using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Characters.Api;
using Game.Characters.Runtime;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Residency.Api;
using Game.Residency.Runtime;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using NUnit.Framework;

namespace Game.Residency.Tests
{
    public sealed class ResidencyPersistenceIntegrationTests
    {
        [Test]
        public void ResidencyCycleThenSessionRestorePreservesCharacterAndChangedWorldObjectSemanticState()
        {
            CharacterId characterId = CharacterId.FromStableKey("npc", "persistent-hero");
            var characters = new CharacterRegistry();
            var kinematics = new CharacterKinematicState(
                new CharacterVector3(1, 2, 3),
                new CharacterVector3(4, 0, 0),
                new CharacterVector3(0, 0, 1));
            Assert.AreEqual(CharacterRegistryFailure.None, characters.Create(
                new CharacterDefinition(characterId, CharacterTraits.ConversationCapable | CharacterTraits.Combatant),
                kinematics,
                out CharacterSnapshot created));

            WorldObjectId doorId = new WorldObjectId("door:persistent-gate");
            var objects = new WorldObjectRegistry();
            var door = new DoorToggleObject(doorId, new CharacterVector3(8, 0, 8));
            Assert.IsTrue(objects.TryRegister(door));
            Assert.IsTrue(door.Interact(new WorldInteractionContext(characterId)).Succeeded);
            Assert.IsTrue(door.IsOpen);

            using (var coordinator = new GameplayResidencyCoordinator(null))
            {
                IResidencyDemandLease characterDemand = coordinator.Acquire(new ResidencyDemandRequest(
                    new ResidencyTarget(ResidencyTargetKind.Character, characterId.Value),
                    ResidencyFidelity.Detailed, "persistence-fixture", "Persistence", "cycle before save"));
                IResidencyDemandLease objectDemand = coordinator.Acquire(new ResidencyDemandRequest(
                    new ResidencyTarget(ResidencyTargetKind.WorldObject, doorId.Value),
                    ResidencyFidelity.Detailed, "persistence-fixture", "Persistence", "cycle before save"));
                coordinator.Reconcile();
                characterDemand.Dispose();
                objectDemand.Dispose();
                coordinator.Reconcile();
                AssertCurrent(coordinator, new ResidencyTarget(ResidencyTargetKind.Character, characterId.Value), ResidencyFidelity.Dormant);
                AssertCurrent(coordinator, new ResidencyTarget(ResidencyTargetKind.WorldObject, doorId.Value), ResidencyFidelity.Dormant);
            }

            const ulong revision = 77UL;
            var store = new MemoryStore();
            var sourceContributors = new ISessionSnapshotContributor[]
            {
                new CharacterContributor(characters),
                new WorldObjectContributor(objects)
            };
            var restoreFactory = new RestoreGraphFactory(characterId, doorId);
            var persistence = new SessionPersistenceService(new FixedBarrier(revision), sourceContributors, store, restoreFactory);
            var contentId = new SessionContentId("residency:persistence-v1");
            var worldId = new SessionWorldId("residency:world");
            var saveId = new SessionSaveId("residency-cycle");

            SessionPersistenceResult save = persistence.CaptureAndSave(new SessionCaptureRequest(
                saveId, "residency-session", contentId, worldId, 638900000000000100L));
            Assert.IsTrue(save.Succeeded, save.Detail);
            SessionPersistenceResult restore = persistence.Restore(new SessionRestoreRequest(saveId, contentId, worldId));
            Assert.IsTrue(restore.Succeeded, restore.Detail);
            Assert.IsTrue(restoreFactory.LastGraph.Completed);
            Assert.IsFalse(restoreFactory.LastGraph.Aborted);

            Assert.IsTrue(restoreFactory.LastGraph.Characters.TryGet(characterId, out CharacterSnapshot restoredCharacter));
            Assert.AreEqual(created.Definition.Traits, restoredCharacter.Definition.Traits);
            Assert.AreEqual(created.Kinematics, restoredCharacter.Kinematics);
            Assert.AreEqual(created.Revision, restoredCharacter.Revision);

            Assert.IsTrue(restoreFactory.LastGraph.Objects.TryGet(doorId, out IWorldObjectBehavior restoredBehavior));
            var restoredDoor = (DoorToggleObject)restoredBehavior;
            Assert.IsTrue(restoredDoor.IsOpen);
            Assert.AreEqual(door.CaptureState().Revision, restoredDoor.CaptureState().Revision);
        }

        private static void AssertCurrent(IGameplayResidencyCoordinator coordinator, ResidencyTarget target, ResidencyFidelity expected)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot snapshot));
            Assert.AreEqual(expected, snapshot.Current);
        }

        private sealed class CharacterContributor : ISessionSnapshotContributor
        {
            private readonly ICharacterRegistryPersistence _registry;
            public CharacterContributor(ICharacterRegistryPersistence registry) { _registry = registry; }
            public string SectionId => "characters";
            public int SchemaVersion => 1;
            public int RestoreOrder => 10;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                CharacterRegistryState state = _registry.CaptureState();
                if (state.Characters.Count != 1) return SessionContributorCapture.Reject("Fixture expects one character.");
                CharacterRecord record = state.Characters[0];
                CharacterKinematicState k = record.Kinematics;
                string payload = string.Join("|", new[]
                {
                    record.Definition.Id.Value,
                    ((int)record.Definition.Traits).ToString(CultureInfo.InvariantCulture),
                    ((int)record.Lifecycle).ToString(CultureInfo.InvariantCulture),
                    F(k.Position.X), F(k.Position.Y), F(k.Position.Z),
                    F(k.Velocity.X), F(k.Velocity.Y), F(k.Velocity.Z),
                    F(k.Facing.X), F(k.Facing.Y), F(k.Facing.Z),
                    record.Revision.ToString(CultureInfo.InvariantCulture)
                });
                return SessionContributorCapture.Success(new SessionSectionSnapshot(
                    SectionId, typeof(CharacterRegistryState).FullName, SchemaVersion, authoritativeRevision, Encoding.UTF8.GetBytes(payload)));
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section) =>
                section.SectionId == SectionId && section.SchemaVersion == SchemaVersion
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject("Character section metadata mismatch.");

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                string[] fields = Encoding.UTF8.GetString(section.CopyPayload()).Split('|');
                if (fields.Length != 13) return SessionContributorResult.Reject("Character payload shape mismatch.");
                var id = new CharacterId(fields[0]);
                var definition = new CharacterDefinition(id, (CharacterTraits)I(fields[1]));
                var kinematics = new CharacterKinematicState(
                    new CharacterVector3(P(fields[3]), P(fields[4]), P(fields[5])),
                    new CharacterVector3(P(fields[6]), P(fields[7]), P(fields[8])),
                    new CharacterVector3(P(fields[9]), P(fields[10]), P(fields[11])));
                var record = new CharacterRecord(definition, (CharacterLifecycleState)I(fields[2]), kinematics, U(fields[12]), Array.Empty<CharacterBinding>());
                CharacterRegistryFailure failure = _registry.RestoreState(new CharacterRegistryState(new[] { record }, Array.Empty<CharacterId>()));
                return failure == CharacterRegistryFailure.None ? SessionContributorResult.Success() : SessionContributorResult.Reject(failure.ToString());
            }
        }

        private sealed class WorldObjectContributor : ISessionSnapshotContributor
        {
            private readonly IWorldObjectRegistry _registry;
            public WorldObjectContributor(IWorldObjectRegistry registry) { _registry = registry; }
            public string SectionId => "world-objects";
            public int SchemaVersion => 1;
            public int RestoreOrder => 20;
            public bool RequiredForRestore => true;

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                IReadOnlyList<WorldObjectStateSnapshot> state = _registry.CaptureState();
                if (state.Count != 1) return SessionContributorCapture.Reject("Fixture expects one world object.");
                WorldObjectStateSnapshot item = state[0];
                string payload = string.Join("|", new[]
                {
                    item.ObjectId.Value,
                    ((int)item.Kind).ToString(CultureInfo.InvariantCulture),
                    item.Enabled ? "1" : "0",
                    item.StateCode.ToString(CultureInfo.InvariantCulture),
                    item.Revision.ToString(CultureInfo.InvariantCulture)
                });
                return SessionContributorCapture.Success(new SessionSectionSnapshot(
                    SectionId, typeof(WorldObjectStateSnapshot).FullName, SchemaVersion, authoritativeRevision, Encoding.UTF8.GetBytes(payload)));
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section) =>
                section.SectionId == SectionId && section.SchemaVersion == SchemaVersion
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject("World object section metadata mismatch.");

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                string[] fields = Encoding.UTF8.GetString(section.CopyPayload()).Split('|');
                if (fields.Length != 5) return SessionContributorResult.Reject("World object payload shape mismatch.");
                var snapshot = new WorldObjectStateSnapshot(
                    new WorldObjectId(fields[0]), (WorldObjectKind)I(fields[1]), fields[2] == "1", I(fields[3]), U(fields[4]));
                WorldInteractionResult result = _registry.RestoreState(new[] { snapshot });
                return result.Succeeded ? SessionContributorResult.Success() : SessionContributorResult.Reject(result.Failure.ToString());
            }
        }

        private sealed class RestoreGraphFactory : ISessionRestoreGraphFactory
        {
            private readonly CharacterId _characterId;
            private readonly WorldObjectId _doorId;
            public RestoreGraphFactory(CharacterId characterId, WorldObjectId doorId) { _characterId = characterId; _doorId = doorId; }
            public RestoreGraph LastGraph { get; private set; }
            public bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error)
            {
                LastGraph = new RestoreGraph(_characterId, _doorId);
                graph = LastGraph;
                error = string.Empty;
                return true;
            }
        }

        private sealed class RestoreGraph : ISessionRestoreGraph
        {
            private readonly ISessionSnapshotContributor[] _contributors;
            public RestoreGraph(CharacterId characterId, WorldObjectId doorId)
            {
                Characters = new CharacterRegistry();
                Objects = new WorldObjectRegistry();
                Objects.TryRegister(new DoorToggleObject(doorId, new CharacterVector3(8, 0, 8)));
                _contributors = new ISessionSnapshotContributor[] { new CharacterContributor(Characters), new WorldObjectContributor(Objects) };
            }
            public CharacterRegistry Characters { get; }
            public WorldObjectRegistry Objects { get; }
            public bool Completed { get; private set; }
            public bool Aborted { get; private set; }
            public IReadOnlyList<ISessionSnapshotContributor> Contributors => _contributors;
            public void CompleteRestore() { Completed = true; }
            public void AbortRestore() { Aborted = true; }
        }

        private sealed class FixedBarrier : ISessionCaptureBarrier
        {
            private readonly ulong _revision;
            public FixedBarrier(ulong revision) { _revision = revision; }
            public bool TryEnter(out ISessionCaptureLease lease) { lease = new Lease(_revision); return true; }
            private sealed class Lease : ISessionCaptureLease
            {
                public Lease(ulong revision) { AuthoritativeRevision = revision; }
                public ulong AuthoritativeRevision { get; }
                public void Dispose() { }
            }
        }

        private sealed class MemoryStore : ISessionSaveStore
        {
            private byte[] _staged;
            private byte[] _published;
            private SessionSaveId _id;
            public bool TryStage(SessionSaveId saveId, byte[] payload, out string error) { _id = saveId; _staged = (byte[])payload.Clone(); error = string.Empty; return true; }
            public bool TryPublish(SessionSaveId saveId, out string error) { if (_staged == null || saveId != _id) { error = "missing stage"; return false; } _published = _staged; _staged = null; error = string.Empty; return true; }
            public bool TryReadPublished(SessionSaveId saveId, out byte[] payload, out string error) { if (_published == null || saveId != _id) { payload = null; error = "missing"; return false; } payload = (byte[])_published.Clone(); error = string.Empty; return true; }
            public IReadOnlyList<SessionSaveId> ListPublished() => _published == null ? Array.Empty<SessionSaveId>() : new[] { _id };
        }

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static float P(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        private static int I(string value) => int.Parse(value, CultureInfo.InvariantCulture);
        private static ulong U(string value) => ulong.Parse(value, CultureInfo.InvariantCulture);
    }
}
