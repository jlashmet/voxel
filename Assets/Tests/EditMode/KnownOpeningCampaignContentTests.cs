using System;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KnownOpeningCampaignContentTests
    {
        [Test]
        public void ActorfulDestinationCutsceneRequiresAnExplicitCampaignBinding()
        {
            CutsceneActorId speaker = new CutsceneActorId("destination-speaker");
            CutsceneDefinition definition = ActorfulDestination(speaker);

            Assert.Throws<ArgumentException>(() =>
                KnownOpeningCampaignContent.Build(definition));
        }

        [Test]
        public void DestinationCutsceneCanBindItsActorIdToTheKnownDestinationNpcRole()
        {
            CutsceneActorId speaker = new CutsceneActorId("destination-speaker");
            CutsceneDefinition definition = ActorfulDestination(speaker);

            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                definition,
                (scene, roles) => scene.Bind(
                    speaker,
                    CutsceneActorTarget.Npc(roles.DestinationNpc)));

            CutsceneSpec destination = content.Blueprint.Cutscenes
                .Single(value => value.Ref.Equals(content.DestinationCutscene));
            Assert.That(destination.Site, Is.EqualTo(content.FirstDestination));
            Assert.That(destination.ActorBindings.Count, Is.EqualTo(1));
            Assert.That(destination.ActorBindings[0].Actor, Is.EqualTo(speaker));
            Assert.That(destination.ActorBindings[0].Target.Kind, Is.EqualTo(CutsceneActorTargetKind.Npc));
            Assert.That(destination.ActorBindings[0].Target.Npc, Is.EqualTo(content.DestinationNpc));
        }

        private static CutsceneDefinition ActorfulDestination(CutsceneActorId speaker) =>
            new CutsceneDefinition(
                "destination-conversation",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Dialogue(
                        speaker,
                        new CutsceneCueId("destination-conversation.dialogue"))
                });
    }
}
