using System;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
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
            RegionRef region = game.World.RequireRegion("kentridge-region", _ => { });
            SettlementRef kentridge = game.World.RequireSettlement("kentridge", settlement => settlement
                .InRegion(region)
                .Archetype(SettlementArchetype.Town));
            SiteRef startingPub = game.World.RequireSite("starting-pub", kentridge, site => site
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
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);

            KentridgeCampaignGenerationPlan generation = KentridgeCampaignWorldPlanner.Plan(
                blueprint,
                town);

            Assert.That(generation.Sites.IsResolved, Is.True);
            Assert.That(generation.NpcAssignments.Count, Is.EqualTo(3));
            Assert.That(generation.HiddenSpaces.Count, Is.EqualTo(1),
                "Pre-voxel planning must expose the real hidden room geometry for catalogue emission.");
            Assert.That(generation.Secrets.Count, Is.EqualTo(1));
            Assert.That(generation.Secrets[0].RequiredSecret.Id, Is.EqualTo("pub-cache"));

            VoxelWorldGenSettings settings = Settings();
            var catalogue = KentridgeCombinedVoxelCatalogue.Build(
                settlement,
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

        [Test]
        public void KentridgePlannerRejectsPopulationRequirementItCannotProve()
        {
            var game = Campaign.Create("unsupported-kentridge-population");
            RegionRef region = game.World.RequireRegion("kentridge-region", _ => { });
            SettlementRef kentridge = game.World.RequireSettlement("kentridge", settlement => settlement
                .InRegion(region)
                .Archetype(SettlementArchetype.Town)
                .Population(100, 200));
            game.World.RequireSite("starting-pub", kentridge, site => site
                .Archetype(SiteArchetype.Pub));
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                KentridgeCampaignWorldPlanner.Plan(
                    game.Build(),
                    town));

            Assert.That(error.Message, Does.Contain("population requirement"));
            Assert.That(error.Message, Does.Contain("100..200"));
        }

        [Test]
        public void KnownOpeningDestinationResolvesToDifferentReachableKentridgeSiteWithPhysicalNpc()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));
            CampaignBlueprint blueprint = content.Blueprint;
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignWorldPlanner.Plan(
                blueprint,
                town);

            ResolvedSiteId pubSite = generation.Sites.Bindings
                .Single(value => value.Role.Equals(content.StartingPub)).Site;
            ResolvedSiteId destinationSite = generation.Sites.Bindings
                .Single(value => value.Role.Equals(content.FirstDestination)).Site;
            Assert.That(destinationSite, Is.Not.EqualTo(pubSite));

            var projections = new KentridgeArchitectureSiteProjectionProvider(settlement);
            var traversal = new SettlementStreetTraversalFacts(settlement, projections);
            var facts = new SettlementPlanSiteCandidateFacts(
                settlement,
                new RegionRef("kentridge-region"),
                new SettlementRef("kentridge"),
                projections,
                traversal);
            Assert.That(
                facts.IsReachable(destinationSite, pubSite, TraversalProfile.NormalParty),
                Is.True,
                "The constraint-matched destination must be reachable over Kentridge's authored street graph.");

            NpcSiteAssignment destinationAssignment = generation.NpcAssignments
                .Single(value => value.Npc.Equals(content.DestinationNpc));
            Assert.That(destinationAssignment.Site, Is.EqualTo(destinationSite));
            Assert.That(destinationAssignment.RequiresConversation, Is.True);

            KentridgeCampaignWorldRealization realized = KentridgeCampaignWorldRealizer.Realize(
                generation,
                new KentridgeVoxelSiteRealizationFacts(settlement, 1));
            ResolvedNpcWorldPlacement physicalNpc = realized.Npcs
                .Single(value => value.Npc.Equals(content.DestinationNpc));

            Assert.That(physicalNpc.Site, Is.EqualTo(destinationSite));
            Assert.That(physicalNpc.RequiresConversation, Is.True);
            Assert.That(realized.CutsceneStages.Count, Is.EqualTo(1),
                "Only the recovered opening cutscene has authored stage points; the injected destination cutscene needs no fabricated stage unless its real content asks for one.");
            Assert.That(realized.CutsceneStages[0].Site, Is.EqualTo(content.StartingPub));
            Assert.That(generation.HiddenSpaces.Count, Is.EqualTo(0));
            Assert.That(generation.Secrets.Count, Is.EqualTo(0));
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });

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
