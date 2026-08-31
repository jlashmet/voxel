using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeFarVisibilityPlanningTests
    {
        private const uint Seed = 0x51A7u;

        [Test]
        public void CampaignPlan_ExposesAllSemanticBuildingsBeforeVoxelGeneration()
        {
            var game = Campaign.Create("kentridge-far-visibility");
            RegionRef region = game.World.RequireRegion("kentridge-region", _ => { });
            SettlementRef kentridge = game.World.RequireSettlement("kentridge", settlement => settlement
                .InRegion(region)
                .Archetype(SettlementArchetype.Town));
            game.World.RequireSite("starting-pub", kentridge, site => site
                .Archetype(SiteArchetype.Pub));

            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);

            KentridgeCampaignGenerationPlan generation = KentridgeCampaignWorldPlanner.Plan(
                game.Build(),
                town);

            int expectedBuildingCount = settlement.Plots.Count(plot =>
                plot.Archetype != StructureArchetype.Well);
            var records = generation.Visibility.Query(
                new WorldVisibilityBoundsDm(-100000, -100000, 100000, 100000));

            Assert.That(records.Count, Is.EqualTo(expectedBuildingCount),
                "Semantic far visibility must be available from campaign planning before any voxel catalogue or region generation is requested.");
            Assert.That(records.Select(value => value.StructureKey).Distinct().Count(),
                Is.EqualTo(records.Count),
                "Every planned building must retain one stable far-visibility identity.");
            Assert.That(records.Any(value =>
                value.VisibilityClass == StructureVisibilityClass.OrdinaryStructure), Is.True,
                "The campaign manifest must include ordinary settlement fabric, not only landmarks.");
            Assert.That(records.Any(value =>
                value.VisibilityClass == StructureVisibilityClass.Landmark
                || value.VisibilityClass == StructureVisibilityClass.SettlementAnchor
                || value.VisibilityClass == StructureVisibilityClass.HorizonLandmark), Is.True,
                "Semantically significant Kentridge structures must remain independently visible.");
        }
    }
}
