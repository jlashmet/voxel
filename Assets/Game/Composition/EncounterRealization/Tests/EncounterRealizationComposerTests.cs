using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.EncounterRealization.Tests
{
    public sealed class EncounterRealizationComposerTests
    {
        [Test]
        public void TwoIndependentAuthoredFixtures_ReuseExactWorldBuilderPlacements()
        {
            var first = BuildFixture("alpha", 12f, 3f, -4f);
            var second = BuildFixture("beta", -8f, 1.5f, 21f);

            Assert.That(first.Realization.Anchor, Is.EqualTo(new CharacterVector3(12f, 3f, -4f)));
            Assert.That(first.Realization.Characters[0].Position, Is.EqualTo(new CharacterVector3(13f, 3f, -2f)));
            Assert.That(second.Realization.Anchor, Is.EqualTo(new CharacterVector3(-8f, 1.5f, 21f)));
            Assert.That(second.Realization.Characters[0].Position, Is.EqualTo(new CharacterVector3(-7f, 1.5f, 23f)));
            Assert.That(first.Realization.RealizationId, Is.EqualTo("generated-alpha"));
            Assert.That(second.Realization.RealizationId, Is.EqualTo("generated-beta"));
        }

        [Test]
        public void MissingRequiredWorldBuilderFact_FailsDeterministically()
        {
            Authoring authoring = Author("missing");
            var facts = new FixtureFacts();
            var result = EncounterRealizationComposer.Compose(
                Spec(authoring, "missing"),
                facts);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.EqualTo(EncounterRealizationFailure.MissingSiteRealization));
            StringAssert.Contains("generated-missing", result.Diagnostic);
            StringAssert.Contains("encounter-missing", result.Diagnostic);
        }

        [Test]
        public void BridgeAssembly_DependsOnApisOnly()
        {
            string[] references = typeof(EncounterRealizationComposer).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(references, "Game.WorldBuilder.Runtime");
            CollectionAssert.DoesNotContain(references, "Game.Encounters.Runtime");
            CollectionAssert.DoesNotContain(references, "Game.Characters.Runtime");
        }

        private static EncounterRealizationResult BuildFixture(string suffix, float x, float y, float z)
        {
            Authoring authoring = Author(suffix);
            var site = new ResolvedSiteId("generated-" + suffix);
            var sitePosition = new CharacterVector3(x, y, z);
            var npcPosition = new CharacterVector3(x + 1f, y, z + 2f);
            var facts = new FixtureFacts();
            facts.AddSite(site, sitePosition);
            facts.AddNpc(authoring.Npc, site, npcPosition);

            EncounterRealizationResult result = EncounterRealizationComposer.Compose(
                Spec(authoring, suffix),
                facts);

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic);
            return result;
        }

        private static EncounterRealizationSpec Spec(Authoring authoring, string suffix)
        {
            return new EncounterRealizationSpec(
                new EncounterDefinition(
                    new EncounterId("encounter-" + suffix),
                    EncounterCombatPolicy.Required,
                    "authored-fixture"),
                authoring.Site,
                new ResolvedSiteId("generated-" + suffix),
                new[]
                {
                    new EncounterCharacterIntent(
                        CharacterId.FromStableKey("fixture", "npc-" + suffix),
                        EncounterParticipantOwnership.EncounterOwned,
                        "enemy",
                        authoring.Npc)
                });
        }

        private static Authoring Author(string suffix)
        {
            var campaign = Campaign.Create("fixture-" + suffix);
            RegionHandle region = campaign.World.Region("region-" + suffix);
            SiteHandle site = region.Site("site-" + suffix);
            NpcHandle npc = site.Npc("npc-" + suffix);
            return new Authoring(site.Ref, npc.Ref);
        }

        private readonly struct Authoring
        {
            public SiteRef Site { get; }
            public NpcRef Npc { get; }
            public Authoring(SiteRef site, NpcRef npc) { Site = site; Npc = npc; }
        }

        private sealed class FixtureFacts : IEncounterRealizationFacts
        {
            private readonly Dictionary<ResolvedSiteId, CharacterVector3> _sites =
                new Dictionary<ResolvedSiteId, CharacterVector3>();
            private readonly Dictionary<string, CharacterVector3> _npcs =
                new Dictionary<string, CharacterVector3>();

            public void AddSite(ResolvedSiteId site, CharacterVector3 position) => _sites.Add(site, position);

            public void AddNpc(NpcRef npc, ResolvedSiteId site, CharacterVector3 position) =>
                _npcs.Add(Key(npc, site), position);

            public bool TryGetSiteAnchor(ResolvedSiteId site, out CharacterVector3 position) =>
                _sites.TryGetValue(site, out position);

            public bool TryGetNpcAnchor(NpcRef npc, ResolvedSiteId site, out CharacterVector3 position) =>
                _npcs.TryGetValue(Key(npc, site), out position);

            private static string Key(NpcRef npc, ResolvedSiteId site) => npc.Id + "@" + site.Value;
        }
    }
}
