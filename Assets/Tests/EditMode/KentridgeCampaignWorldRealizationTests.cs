using System.Linq;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCampaignWorldRealizationTests
    {
        private const uint Seed = 0x51A7u;

        [Test]
        public void OpeningCampaignPlansBeforeVoxelEmissionAndRealizesAfterPlacement()
        {
            var game = Campaign.Create("kentridge-opening-world");
            SiteRef startingPub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior)
                .RequireCapability(SiteCapability.PlayerSpawn(4))
                .RequireCapability(SiteCapability.PublicExit));

            NpcRef madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(startingPub));
            NpcRef steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(startingPub));
            NpcRef logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(startingPub));

            game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(startingPub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan)));

            LootTableRef cacheLoot = game.Loot.Table("pub-cache-loot", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            game.World.RequireSecret("pub-cache", secret => secret
                .Inside(startingPub)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(cacheLoot));

            CampaignBlueprint blueprint = game.Build();
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);

            KentridgeCampaignGenerationPlan generation = KentridgeCampaignWorldPlanner.Plan(
                blueprint,
                settlement,
                new RegionRef("kentridge-region"),
                new SettlementRef("kentridge"));

            Assert.That(generation.Sites.IsResolved, Is.True);
            Assert.That(generation.NpcAssignments.Count, Is.EqualTo(3));
            Assert.That(generation.HiddenSpaces.Count, Is.EqualTo(1),
                "Pre-voxel planning must expose the real hidden room geometry for catalogue emission.");
            Assert.That(generation.Secrets.Count, Is.EqualTo(1));
            Assert.That(generation.Secrets[0].RequiredSecret.Id, Is.EqualTo("pub-cache"));

            VoxelWorldGenSettings settings = Settings();
            var catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed,
                settings,
                generation.HiddenSpaces,
                Allocator.Temp);
            try
            {
                bool foundHiddenStructure = false;
                for (var i = 0; i < catalogue.Definitions.Length; i++)
                {
                    if (catalogue.Definitions[i].Name.ToString().StartsWith("kentridge-hidden-"))
                    {
                        foundHiddenStructure = true;
                        break;
                    }
                }
                Assert.That(foundHiddenStructure, Is.True,
                    "The exact hidden-space geometry selected during campaign planning must be emitted into the combined voxel catalogue.");
            }
            finally
            {
                catalogue.Dispose();
            }

            var siteFacts = new KentridgeVoxelSiteRealizationFacts(settlement, 1);
            var hiddenFacts = new KentridgeHiddenSpaceVoxelRealizationFacts(
                settlement,
                1,
                generation.HiddenSpaces);
            KentridgeCampaignWorldRealization realized = KentridgeCampaignWorldRealizer.Realize(
                generation,
                siteFacts,
                hiddenFacts);

            Assert.That(realized.Npcs.Count, Is.EqualTo(3));
            CollectionAssert.AreEquivalent(
                new[] { "madeline", "steven", "logan" },
                realized.Npcs.Select(value => value.Npc.Id).ToArray());
            Assert.That(realized.Npcs.Select(value =>
                    value.Position.Position.X + ":" +
                    value.Position.Position.Y + ":" +
                    value.Position.Position.Z)
                .Distinct()
                .Count(), Is.EqualTo(3));

            Assert.That(realized.CutsceneStages.Count, Is.EqualTo(1));
            Assert.That(realized.CutsceneStages[0].Site, Is.EqualTo(startingPub));
            Assert.DoesNotThrow(() =>
                realized.CutsceneStages[0].Binding.Resolve(KentridgeOpeningCutscene.LoganStop));

            Assert.That(realized.Secrets.Count, Is.EqualTo(1));
            Assert.That(realized.Secrets[0].Secret.RequiredSecret.Id, Is.EqualTo("pub-cache"));
            Assert.That(realized.Secrets[0].EntranceBounds.UnitsPerDecimetre, Is.EqualTo(1));
            Assert.That(realized.Secrets[0].ContainerFloorPoint.Position.Y,
                Is.EqualTo(realized.Secrets[0].HiddenSpaceBounds.MinInclusive.Y));
        }

        private static VoxelWorldGenSettings Settings() =>
            new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: 1,
                    masonry: 2,
                    darkMasonry: 3,
                    timber: 4,
                    glass: 5,
                    warmWindow: 6,
                    roofTile: 7,
                    slate: 8,
                    cloth: 9,
                    moss: 10,
                    water: 11,
                    roadSurface: 12));
    }
}
