using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehouseCompletionTests
    {
        [Test]
        public void RuntimeReadyCastleCarriesFrozenGatehouseRecipe()
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleSpatialPlan raw = StructuresComposition.PlanCastleSpatial(in dimensions);
                CastleSpatialPlan completed = CastleTerrainPlanning.Resolve(
                    in dimensions, raw, seed ^ 0x71A5u);

                Assert.IsTrue(completed.Topology.HasGatehousePlan,
                    $"seed {seed}: runtime-ready topology lost its gatehouse recipe");
                CastleGatehousePlan gatehouse = completed.Topology.Gatehouse;
                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidate(
                        in gatehouse, out CastleGatehousePlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleGatehousePlan expected = CastleGatehousePlanner.Create(in dimensions);
                Assert.AreEqual(expected.TowerSpacing, gatehouse.TowerSpacing);
                Assert.AreEqual(expected.LeftTowerHeight, gatehouse.LeftTowerHeight);
                Assert.AreEqual(expected.RightTowerHeight, gatehouse.RightTowerHeight);
                Assert.AreEqual(expected.BlockHeight, gatehouse.BlockHeight);
                Assert.AreEqual(expected.OpeningHeight, gatehouse.OpeningHeight);
                Assert.AreEqual(expected.BridgeNearDistance, gatehouse.BridgeNearDistance);
                Assert.AreEqual(expected.BridgeLength, gatehouse.BridgeLength);
                Assert.AreEqual(expected.BridgeWidth, gatehouse.BridgeWidth);
            }
        }

        [Test]
        public void CompletionPreservesCallerSuppliedValidGatehouseRecipe()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 71u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(71u);
            CastleGatehousePlan custom = CastleGatehousePlanner.Create(in dimensions);
            custom.BridgeLength += 24;
            custom.BridgeWidth += 8;
            topology.HasGatehousePlan = true;
            topology.Gatehouse = custom;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan attached = CastleGatehousePlanCompletion.Attach(
                in dimensions, spatial);

            Assert.IsTrue(attached.Topology.HasGatehousePlan);
            Assert.AreEqual(custom.BridgeLength, attached.Topology.Gatehouse.BridgeLength);
            Assert.AreEqual(custom.BridgeWidth, attached.Topology.Gatehouse.BridgeWidth);
        }
    }
}
