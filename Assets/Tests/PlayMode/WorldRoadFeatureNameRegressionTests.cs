using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WorldRoadFeatureNameRegressionTests
    {
        private const uint Seed = 0x524F4144u;

        [Test]
        public void LongMacroRouteIdLowersToFixedStringFeatureNameWithoutTruncation()
        {
            var profile = new WorldRoadProfile(
                "macro-road-name-regression",
                "road-surface",
                carriagewayWidthDm: 36,
                transitionWidthDm: 18,
                maximumGradePermille: 160,
                maximumCutFillDm: 36,
                edgeVariationDm: 4,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 820,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                "macro:overworld-moordell->overworld-to-rossdam",
                "overworld-moordell",
                "overworld-to-rossdam",
                Seed,
                profile,
                "full-player FixedString64Bytes regression",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(200, 0),
                });
            ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                intent,
                new FlatRoadTerrain(),
                sampleSpacingDm: 40,
                searchMarginCells: 0);
            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, resolved.Status, resolved.FailureReason);

            var network = new WorldRoadNetwork(new[]
            {
                new WorldRoadNetworkRoute(
                    resolved,
                    WorldRoadSemanticClass.Vehicle,
                    shoulderWidthDm: 6,
                    clearanceWidthDm: 12),
            });
            FeatureCatalogue catalogue = WorldRoadNetworkVoxelCatalogue.Build(
                network,
                BuildSettings(),
                Allocator.Temp);

            try
            {
                Assert.Greater(catalogue.Definitions.Length, 0);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FixedString64Bytes name = catalogue.Definitions[i].Name;
                    Assert.LessOrEqual(name.Length, FixedString64Bytes.UTF8MaxLengthInBytes);
                    StringAssert.StartsWith("world-road-", name.ToString());
                    StringAssert.Contains("-s0p", name.ToString(),
                        "Truncation must preserve the segment/piece suffix used to distinguish bounded corridor pieces.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private sealed class FlatRoadTerrain : IWorldRoadTerrain
        {
            public int HeightAtDm(int xdm, int zdm) => 0;
            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;
        }
    }
}
