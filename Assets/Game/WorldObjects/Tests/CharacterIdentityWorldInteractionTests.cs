using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using NUnit.Framework;

namespace Game.WorldObjects.Tests
{
    public sealed class CharacterIdentityWorldInteractionTests
    {
        [Test]
        public void DurableCharacterEntryUsesSameInteractionPathWithoutPlatformResolution()
        {
            var actorId = new CharacterId("party-member-character");
            var position = new CharacterVector3(3f, 4f, 5f);
            var characters = new CharacterOnlyQuery(actorId, position);
            var objects = new WorldObjectRegistry();
            var facts = new RecordingFacts();
            var later = new DoorToggleObject(new WorldObjectId("door-b"), position);
            var first = new DoorToggleObject(new WorldObjectId("door-a"), position);
            Assert.That(objects.TryRegister(later), Is.True);
            Assert.That(objects.TryRegister(first), Is.True);

            WorldInteractionResult result = new InteractionClickedProcessor(characters, objects, facts).Process(actorId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(characters.ResolveCalls, Is.Zero,
                "An already-authenticated durable character must not be converted back into platform identity.");
            Assert.That(first.IsOpen, Is.True);
            Assert.That(later.IsOpen, Is.False);
            Assert.That(facts.Facts.Count, Is.EqualTo(1));
            Assert.That(facts.Facts[0].ActorId, Is.EqualTo(actorId));
            Assert.That(facts.Facts[0].ObjectId, Is.EqualTo(first.Id));
        }

        [Test]
        public void DurableCharacterEntryFailsClosedForUnknownCharacter()
        {
            var characters = new CharacterOnlyQuery(
                new CharacterId("known"), new CharacterVector3(1f, 0f, 1f));
            var objects = new WorldObjectRegistry();

            WorldInteractionResult result = new InteractionClickedProcessor(characters, objects)
                .Process(new CharacterId("unknown"));

            Assert.That(result.Failure, Is.EqualTo(WorldInteractionFailure.UnknownActor));
            Assert.That(characters.ResolveCalls, Is.Zero);
        }

        private sealed class CharacterOnlyQuery : ICharacterQuery
        {
            private readonly CharacterId _id;
            private readonly CharacterSnapshot _snapshot;

            public CharacterOnlyQuery(CharacterId id, CharacterVector3 position)
            {
                _id = id;
                _snapshot = new CharacterSnapshot(
                    new CharacterDefinition(id, CharacterTraits.PlayerControlled),
                    CharacterLifecycleState.Active,
                    new CharacterKinematicState(position, default, default),
                    1);
            }

            public int ResolveCalls { get; private set; }
            public IReadOnlyList<CharacterSnapshot> GetAll() => new[] { _snapshot };

            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                snapshot = _snapshot;
                return id == _id;
            }

            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                ResolveCalls++;
                id = default;
                throw new InvalidOperationException("Durable character interaction must not resolve platform identity.");
            }
        }

        private sealed class RecordingFacts : IWorldInteractionFactSink
        {
            public readonly List<WorldInteractionFact> Facts = new List<WorldInteractionFact>();
            public void Publish(WorldInteractionFact fact) => Facts.Add(fact);
        }
    }
}
