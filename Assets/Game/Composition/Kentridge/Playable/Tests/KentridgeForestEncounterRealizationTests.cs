using System;
using NUnit.Framework;
using Game.Characters.Api;
using Game.Composition.EncounterRealization;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeForestEncounterRealizationTests
    {
        [Test]
        public void ExactMacroLayoutPlacement_DrivesEncounterAnchorAndFormation()
        {
            TopDownWorldLayout layout = Layout("forest", new TopDownWorldGridPoint(2, -3));
            KentridgeForestEncounterRealization.RememberMacroLayout(
                layout,
                "forest",
                1000,
                -500,
                400);

            EncounterRealizationResult result = Compose("first");

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic);
            Assert.That(result.Realization.RealizationId, Is.EqualTo("macro-forest"));
            Assert.That(result.Realization.Anchor, Is.EqualTo(new CharacterVector3(180f, 0f, -170f)));
            Assert.That(result.Realization.Characters[0].Position, Is.EqualTo(new CharacterVector3(174.6f, 0f, -170.8f)));
            Assert.That(result.Realization.Characters[1].Position, Is.EqualTo(new CharacterVector3(180.8f, 0f, -168.8f)));
            Assert.That(result.Realization.Characters[2].Position, Is.EqualTo(new CharacterVector3(185.8f, 0f, -169.9f)));
        }

        [Test]
        public void LaterWorldBuilderLayout_ReplacesPriorPhysicalRealizationRatherThanUsingHardcodedCoordinates()
        {
            KentridgeForestEncounterRealization.RememberMacroLayout(
                Layout("forest", new TopDownWorldGridPoint(0, 1)),
                "forest",
                10,
                20,
                100);
            CharacterVector3 firstAnchor = Compose("before").Realization.Anchor;

            KentridgeForestEncounterRealization.RememberMacroLayout(
                Layout("forest", new TopDownWorldGridPoint(-4, 5)),
                "forest",
                -300,
                700,
                250);
            EncounterRealizationResult second = Compose("after");

            Assert.That(firstAnchor, Is.EqualTo(new CharacterVector3(1f, 0f, 12f)));
            Assert.That(second.IsSuccess, Is.True, second.Diagnostic);
            Assert.That(second.Realization.Anchor, Is.EqualTo(new CharacterVector3(-130f, 0f, 195f)));
            Assert.That(second.Realization.Anchor, Is.Not.EqualTo(firstAnchor));
        }

        [Test]
        public void MissingForestNode_FailsAtWorldRealizationHandoff()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                KentridgeForestEncounterRealization.RememberMacroLayout(
                    Layout("elsewhere", new TopDownWorldGridPoint(1, 1)),
                    "forest",
                    0,
                    0,
                    100));

            StringAssert.Contains("forest", exception.Message);
            StringAssert.Contains("WorldBuilder macro layout", exception.Message);
        }

        private static EncounterRealizationResult Compose(string suffix)
        {
            return KentridgeForestEncounterRealization.Compose(
                new EncounterDefinition(
                    new EncounterId("forest-test-" + suffix),
                    EncounterCombatPolicy.Required,
                    "forest-test"),
                CharacterId.FromStableKey("test", suffix + "-left"),
                CharacterId.FromStableKey("test", suffix + "-centre"),
                CharacterId.FromStableKey("test", suffix + "-right"));
        }

        private static TopDownWorldLayout Layout(string nodeId, TopDownWorldGridPoint position)
        {
            var node = new TopDownWorldNodeSpec(nodeId, nodeId, TopDownWorldNodeKind.Region);
            return new TopDownWorldLayout(
                nodeId,
                123u,
                new[] { new TopDownWorldNodePlacement(node, position) },
                Array.Empty<TopDownWorldRouteSpec>());
        }
    }
}
