using System.Collections.Generic;
using Game.Characters.Api;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using NUnit.Framework;

namespace Game.WorldObjects.Tests
{
    public sealed class WorldObjectBoundaryTests
    {
        [Test]
        public void UnsupportedCapabilityRejectsWithoutPublishingFact()
        {
            var actorId = new CharacterId("fixture:actor");
            var position = new CharacterVector3(4f, 5f, 6f);
            var registry = new WorldObjectRegistry();
            registry.TryRegister(new UnsupportedBehavior(new WorldObjectId("fixture:unsupported"), position));
            var facts = new RecordingFacts();
            var processor = new InteractionClickedProcessor(new SingleCharacterQuery(actorId, position, 77UL), registry, facts);

            var result = processor.Process(77UL);

            Assert.That(result.Failure, Is.EqualTo(WorldInteractionFailure.UnsupportedCapability));
            Assert.That(facts.Count, Is.EqualTo(0));
        }

        [Test]
        public void RestoreConsumedPickupDoesNotReplayTransfer()
        {
            var actorId = new CharacterId("fixture:actor");
            var position = new CharacterVector3(4f, 5f, 6f);
            var firstTransfer = new CountingTransfer();
            var source = new ItemPickupObject(
                new WorldObjectId("fixture:pickup"),
                position,
                new WorldItemPayload("fixture-item", 1),
                firstTransfer);
            Assert.That(source.Interact(new WorldInteractionContext(actorId)).Succeeded, Is.True);
            Assert.That(firstTransfer.Calls, Is.EqualTo(1));
            var consumed = source.CaptureState();

            var restoredTransfer = new CountingTransfer();
            var restored = new ItemPickupObject(
                new WorldObjectId("fixture:pickup"),
                position,
                new WorldItemPayload("fixture-item", 1),
                restoredTransfer);
            Assert.That(restored.RestoreState(consumed).Succeeded, Is.True);
            Assert.That(restored.Enabled, Is.False);
            Assert.That(restoredTransfer.Calls, Is.EqualTo(0));
        }

        private sealed class CountingTransfer : IWorldItemPickupTransfer
        {
            public int Calls { get; private set; }
            public WorldInteractionResult TryTransfer(CharacterId actorId, WorldObjectId objectId, WorldItemPayload payload)
            {
                Calls++;
                return WorldInteractionResult.Success();
            }
        }

        private sealed class UnsupportedBehavior : IWorldObjectBehavior
        {
            public WorldObjectId Id { get; }
            public WorldObjectKind Kind => WorldObjectKind.DoorToggle;
            public CharacterVector3 Position { get; }
            public UnsupportedBehavior(WorldObjectId id, CharacterVector3 position) { Id = id; Position = position; }
            public WorldInteractionResult Interact(WorldInteractionContext context) =>
                WorldInteractionResult.Reject(WorldInteractionFailure.UnsupportedCapability);
            public WorldObjectStateSnapshot CaptureState() => new WorldObjectStateSnapshot(Id, Kind, true, 0, 0);
            public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot) => WorldInteractionResult.Success();
        }

        private sealed class RecordingFacts : IWorldInteractionFactSink
        {
            public int Count { get; private set; }
            public void Publish(WorldInteractionFact fact) { Count++; }
        }

        private sealed class SingleCharacterQuery : ICharacterQuery
        {
            private readonly CharacterId _id;
            private readonly CharacterBinding _binding;
            private readonly CharacterSnapshot _snapshot;

            public SingleCharacterQuery(CharacterId id, CharacterVector3 position, ulong steamId)
            {
                _id = id;
                _binding = new CharacterBinding("steam", steamId.ToString());
                _snapshot = new CharacterSnapshot(
                    new CharacterDefinition(id, CharacterTraits.PlayerControlled),
                    CharacterLifecycleState.Active,
                    new CharacterKinematicState(position, default, default),
                    1);
            }

            public IReadOnlyList<CharacterSnapshot> GetAll() => new[] { _snapshot };
            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                snapshot = _snapshot;
                return id == _id;
            }
            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = _id;
                return binding.Equals(_binding);
            }
        }
    }
}
