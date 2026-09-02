using System;
using System.Collections.Generic;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCampaignSecretBootstrapTests
    {
        private const uint Seed = 0x51A7u;

        private sealed class Actor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public Actor(CutsceneInt3 position) => Position = position;
            public void PlaceAt(CutsceneStagePoint destination) => Position = destination.Position;
            public ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }
            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class ActorHost : IKentridgeCampaignActorHost
        {
            private readonly Dictionary<NpcRef, ICutsceneActorRuntime> _npcs =
                new Dictionary<NpcRef, ICutsceneActorRuntime>();
            public int PreparedCount { get; private set; }

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                for (var i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    PreparedCount++;
                    Int3 point = placement.Position.Position;
                    _npcs[placement.Npc] = new Actor(new CutsceneInt3(point.X, point.Y, point.Z));
                }
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor) =>
                _npcs.TryGetValue(npc, out actor);

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = null;
                return false;
            }
        }

        private sealed class SecretHost : IKentridgeCampaignSecretHost
        {
            public IReadOnlyList<ResolvedSecretWorldGeometry> Prepared { get; private set; }

            public void PrepareSecrets(IReadOnlyList<ResolvedSecretWorldGeometry> secrets) =>
                Prepared = secrets;
        }

        private sealed class Presentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }

        [Test]
        public void SecretCampaignRequiresGameplayHostAndPassesExactFalseWallBounds()
        {
            var game = Campaign.Create("secret-session");
            RegionRef region = game.World.RequireRegion("kentridge-region", _ => { });
            SettlementRef kentridge = game.World.RequireSettlement("kentridge", settlement => settlement
                .InRegion(region)
                .Archetype(SettlementArchetype.Town));
            SiteRef pub = game.World.RequireSite("starting-pub", kentridge, site => site
                .Archetype(SiteArchetype.Pub));
            game.World.RequireNpc("keeper", npc => npc.PlaceAt(pub));
            LootTableRef loot = game.Loot.Table("cache-loot", table => table
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            game.World.RequireSecret("pub-cache", secret => secret
                .Inside(pub)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(loot));

            CampaignBlueprint blueprint = game.Build();
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                blueprint,
                town);
            var siteFacts = new KentridgeVoxelSiteRealizationFacts(settlement, 1);
            var hiddenFacts = new KentridgeHiddenSpaceVoxelRealizationFacts(
                settlement,
                1,
                generation.HiddenSpaces);
            var realizationFacts = new KentridgeCampaignRealizationFacts(siteFacts, hiddenFacts);

            var missingHostActors = new ActorHost();
            ArgumentNullException missingHost = Assert.Throws<ArgumentNullException>(() =>
                KentridgeCampaignSessionBootstrap.CreateSession(
                    blueprint,
                    generation,
                    realizationFacts,
                    missingHostActors,
                    new Presentation()));
            Assert.That(missingHost.ParamName, Is.EqualTo("secretHost"));
            Assert.That(missingHostActors.PreparedCount, Is.EqualTo(0),
                "Missing secret runtime wiring must fail before the authoritative NPC batch is mutated.");

            var actors = new ActorHost();
            var secrets = new SecretHost();
            KentridgeCampaignSession session = KentridgeCampaignSessionBootstrap.CreateSession(
                blueprint,
                generation,
                realizationFacts,
                actors,
                new Presentation(),
                secrets);

            Assert.That(session.World.Secrets.Count, Is.EqualTo(1));
            Assert.That(secrets.Prepared, Is.SameAs(session.World.Secrets));
            Assert.That(actors.PreparedCount, Is.EqualTo(1));

            RealizedWorldBounds wall = secrets.Prepared[0].EntranceBounds;
            Assert.That(wall.UnitsPerDecimetre, Is.EqualTo(1));
            int sizeX = wall.MaxInclusive.X - wall.MinInclusive.X + 1;
            int sizeY = wall.MaxInclusive.Y - wall.MinInclusive.Y + 1;
            int sizeZ = wall.MaxInclusive.Z - wall.MinInclusive.Z + 1;
            Assert.That(sizeY, Is.EqualTo(24));
            CollectionAssert.AreEquivalent(new[] { 4, 8 }, new[] { sizeX, sizeZ },
                "The gameplay secret host must receive the exact 4 dm wall thickness and 8 dm opening width; a whole-block edit would be too coarse.");
        }
    }
}
