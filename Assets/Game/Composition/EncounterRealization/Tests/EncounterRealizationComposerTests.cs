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
        public void AuthoredNpcPlacement_UsesExactRealizedNpcAnchor()
        {
            Authoring authoring = Author("npc");
            var site = new ResolvedSiteId("generated-npc");
            var facts = new FixtureFacts();
            facts.AddSite(site, new CharacterVector3(2f, 0f, 3f));
            facts.AddNpc(authoring.Npc, site, new CharacterVector3(9f, 4f, 8f));
            var spec = new EncounterRealizationSpec(
                Definition("npc"),
                authoring.Site,
                site,
                new[]
                {
                    new EncounterCharacterIntent(
                        CharacterId.FromStableKey("fixture", "npc-authored"),
                        EncounterParticipantOwnership.Persistent,
                        "witness",
                        authoring.Npc)
                });

            EncounterRealizationResult result = EncounterRealizationComposer.Compose(spec, facts);

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic);
            Assert.That(result.Realization.Characters[0].Position, Is.EqualTo(new CharacterVector3(9f, 4f, 8f)));
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
        public void MissingRequiredSpawnFact_FailsDeterministically()
        {
            Authoring authoring = Author("spawn-missing");
            var site = new ResolvedSiteId("generated-spawn-missing");
            var facts = new FixtureFacts();
            facts.AddSite(site, new CharacterVector3(1f, 2f, 3f));

            EncounterRealizationResult result = EncounterRealizationComposer.Compose(
                Spec(authoring, "spawn-missing"),
                facts);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.EqualTo(EncounterRealizationFailure.MissingSpawnRealization));
            StringAssert.Contains("spawn-spawn-missing", result.Diagnostic);
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
            var spawnPosition = new CharacterVector3(x + 1f, y, z + 2f);
            var facts = new FixtureFacts();
            facts.AddSite(site, sitePosition);
            facts.AddSpawn(new EncounterSpawnPointRef("spawn-" + suffix), site, spawnPosition);

            EncounterRealizationResult result = EncounterRealizationComposer.Compose(
                Spec(authoring, suffix),
                facts);

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic);
            return result;
        }

        private static EncounterRealizationSpec Spec(Authoring authoring, string suffix)
        {
            return new EncounterRealizationSpec(
                Definition(suffix),
                authoring.Site,
                new ResolvedSiteId("generated-" + suffix),
                new[]
                {
                    new EncounterCharacterIntent(
                        CharacterId.FromStableKey("fixture", "spawned-" + suffix),
                        EncounterParticipantOwnership.EncounterOwned,
                        "enemy",
                        new EncounterSpawnPointRef("spawn-" + suffix))
                });
        }

        private static EncounterDefinition Definition(string suffix) =>
            new EncounterDefinition(
                new EncounterId("encounter-" + suffix),
                EncounterCombatPolicy.Required,
                "authored-fixture");

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
            private readonly Dictionary<string, CharacterVector3> _spawns =
                new Dictionary<string, CharacterVector3>();

            public void AddSite(ResolvedSiteId site, CharacterVector3 position) => _sites.Add(site, position);

            public void AddNpc(NpcRef npc, ResolvedSiteId site, CharacterVector3 position) =>
                _npcs.Add(NpcKey(npc, site), position);

            public void AddSpawn(EncounterSpawnPointRef spawn, ResolvedSiteId site, CharacterVector3 position) =>
                _spawns.Add(SpawnKey(spawn, site), position);

            public bool TryGetSiteAnchor(ResolvedSiteId site, out CharacterVector3 position) =>
                _sites.TryGetValue(site, out position);

            public bool TryGetNpcAnchor(NpcRef npc, ResolvedSiteId site, out CharacterVector3 position) =>
                _npcs.TryGetValue(NpcKey(npc, site), out position);

            public bool TryGetSpawnAnchor(EncounterSpawnPointRef spawn, ResolvedSiteId site, out CharacterVector3 position) =>
                _spawns.TryGetValue(SpawnKey(spawn, site), out position);

            private static string NpcKey(NpcRef npc, ResolvedSiteId site) => npc.Id + "@" + site.Value;
            private static string SpawnKey(EncounterSpawnPointRef spawn, ResolvedSiteId site) => spawn.Value + "@" + site.Value;
        }
    }
}
