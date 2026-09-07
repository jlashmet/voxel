using Game.Quests.Api;
using Game.WorldObjects.Api;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeWorldInteractionQuestObservationAdapterTests
    {
        [Test]
        public void SuccessfulWorldFactBecomesSemanticQuestInteractionWithoutQuestPolicy()
        {
            QuestObservation observed = default;
            bool called = false;
            var adapter = new KentridgeWorldInteractionQuestObservationAdapter(observation =>
            {
                observed = observation;
                called = true;
            });

            adapter.Publish(new WorldInteractionFact(
                7,
                new Game.Characters.Api.CharacterId("player"),
                new WorldObjectId("kentridge-well"),
                WorldObjectKind.DoorToggle,
                1,
                3));

            Assert.That(called, Is.True);
            Assert.That(observed.Kind, Is.EqualTo(QuestObservationKind.Interacted));
            Assert.That(observed.SubjectId, Is.EqualTo("kentridge-well"));
        }
    }
}
