using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTowerVariationPlanningTests
    {
        [Test]
        public void CompletionPreservesHistoricalOuterTowerVariationStream()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                var topology = new CastleTopologyPlan
                {
                    Perimeter = CastlePerimeterKind.Rectangular,
                    KeepPlacement = CastleKeepPlacement.Central,
                    Wards = CastleWardPattern.SingleWard,
                    DesiredTowerCount = 6,
                    HasPosternGate = false,
                };

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in dimensions, spatial);

                Assert.AreEqual(spatial.Towers.Length, completed.Towers.Length);
                for (int i = 0; i < completed.Towers.Length; i++)
                {
                    CastleTowerPlacementSpec tower = completed.Towers[i];
                    uint variationSeed = CastleSeedPartition.Derive(
                        seed,
                        CastleSeedDomain.Walls,
                        (uint)(0x2000 + tower.Id));
                    int expectedHeightVariation = 8 + (int)(variationSeed % 51u);
                    bool expectedRoof = tower.Role == CastleTowerPlacementRole.Corner
                                     && ((variationSeed >> 8) & 1u) != 0u;

                    Assert.AreEqual(expectedHeightVariation, tower.HeightVariation,
                        $"seed {seed}, tower {tower.Id}: height variation drifted");
                    Assert.AreEqual(expectedRoof, tower.HasRoof,
                        $"seed {seed}, tower {tower.Id}: roof variation drifted");
                }
            }
        }

        [Test]
        public void CompletionPreservesHistoricalInnerTowerRoofStream()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                var topology = new CastleTopologyPlan
                {
                    Perimeter = CastlePerimeterKind.Rectangular,
                    KeepPlacement = CastleKeepPlacement.Central,
                    Wards = CastleWardPattern.InnerAndOuterWards,
                    DesiredTowerCount = 4,
                    HasPosternGate = false,
                };

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in dimensions, spatial);

                Assert.AreEqual(spatial.InnerWardVertices.Length, completed.InnerTowers.Length);
                for (int i = 0; i < completed.InnerTowers.Length; i++)
                {
                    CastleTowerPlacementSpec tower = completed.InnerTowers[i];
                    uint variationSeed = CastleSeedPartition.Derive(
                        seed,
                        CastleSeedDomain.Walls,
                        (uint)(0x2A00 + tower.Id));

                    Assert.AreEqual(0, tower.HeightVariation,
                        $"seed {seed}, inner tower {tower.Id}: unexpected height variation");
                    Assert.AreEqual((variationSeed & 1u) != 0u, tower.HasRoof,
                        $"seed {seed}, inner tower {tower.Id}: roof variation drifted");
                }
            }
        }
    }
}
