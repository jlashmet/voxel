using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehousePlanTests
    {
        [Test]
        public void PlannerFreezesHistoricalGatehouseRecipe()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan castle = CastlePlanner.Create(int3.zero, seed);
                CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(in castle);

                Assert.AreEqual(
                    math.max(54, castle.GateTowerRadius + CastleLayout.FrontGateWidth / 2 + 8),
                    gatehouse.TowerSpacing,
                    $"seed {seed}: tower spacing changed during compatibility planning");
                Assert.AreEqual(castle.GateTowerHeight + 38, gatehouse.LeftTowerHeight);
                Assert.AreEqual(castle.GateTowerHeight + 12, gatehouse.RightTowerHeight);
                Assert.AreEqual(castle.WallHeight + 22, gatehouse.BlockHeight);
                Assert.AreEqual(CastleLayout.FrontGateHeight + 14, gatehouse.OpeningHeight);
                Assert.AreEqual(10, gatehouse.GateLeafStrapFirstY);
                Assert.AreEqual(13, gatehouse.GateLeafStrapSpacing);
                Assert.AreEqual(3, gatehouse.GateLeafStrapThickness);
                Assert.AreEqual(castle.WallThickness + 4, gatehouse.BridgeNearDistance);
                Assert.AreEqual(150, gatehouse.BridgeLength);
                Assert.AreEqual(68, gatehouse.BridgeWidth);
                Assert.AreEqual(-2, gatehouse.BridgeDeckYOffset);
                Assert.AreEqual(2, gatehouse.BridgeDeckHeight);
                Assert.AreEqual(32, gatehouse.BridgeSupportOffset);
                Assert.AreEqual(-7, gatehouse.BridgeSupportYOffset);
                Assert.AreEqual(5, gatehouse.BridgeSupportHeight);
                Assert.AreEqual(8, gatehouse.BridgeSupportThickness);
                Assert.AreEqual(8, gatehouse.BridgeRailYOffset);
                Assert.AreEqual(4, gatehouse.BridgeRailHeight);
                Assert.AreEqual(4, gatehouse.BridgeRailThickness);
                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidate(
                        in gatehouse, out CastleGatehousePlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidateTowerDetails(
                        in gatehouse, castle.FloorHeight, out issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void SeededPlannerIsDeterministicValidAndVaried()
        {
            CastleGatehousePlan firstStyle = default;
            bool hasFirstStyle = false;
            bool sawTowerVariation = false;
            bool sawMasonryVariation = false;
            bool sawBridgeVariation = false;

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan castle = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in castle, in topology);
                CastleGatePlacementSpec gate = spatial.PrimaryGate;

                CastleGatehousePlan first = CastleGatehousePlanner.Create(
                    in castle, in gate, seed);
                CastleGatehousePlan second = CastleGatehousePlanner.Create(
                    in castle, in gate, seed);
                AssertGatehouseEquals(
                    in first, in second, $"seed {seed}: nondeterministic gatehouse");

                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidate(
                        in first, out CastleGatehousePlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidateTowerDetails(
                        in first, castle.FloorHeight, out issue),
                    $"seed {seed}: {issue}");

                if (!hasFirstStyle)
                {
                    firstStyle = first;
                    hasFirstStyle = true;
                    continue;
                }

                sawTowerVariation |= first.TowerSpacing != firstStyle.TowerSpacing
                                  || first.LeftTowerHeight != firstStyle.LeftTowerHeight
                                  || first.RightTowerHeight != firstStyle.RightTowerHeight;
                sawMasonryVariation |= first.BlockHeight != firstStyle.BlockHeight
                                    || first.OpeningHeight != firstStyle.OpeningHeight;
                sawBridgeVariation |= first.BridgeLength != firstStyle.BridgeLength
                                   || first.BridgeWidth != firstStyle.BridgeWidth
                                   || first.BridgeDeckHeight != firstStyle.BridgeDeckHeight
                                   || first.BridgeSupportOffset != firstStyle.BridgeSupportOffset;
            }

            Assert.IsTrue(sawTowerVariation, "Seeded gatehouses never varied their tower profile.");
            Assert.IsTrue(sawMasonryVariation, "Seeded gatehouses never varied their masonry cap.");
            Assert.IsTrue(sawBridgeVariation, "Seeded gatehouses never varied their bridge profile.");
        }

        [Test]
        public void SpatialCompletionFreezesSeededGatehousePlan()
        {
            const uint seed = 77u;
            CastlePlan castle = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in castle, in topology);
            CastleGatePlacementSpec gate = spatial.PrimaryGate;

            CastleSpatialPlan completed = CastleGatehousePlanCompletion.Attach(in castle, spatial);
            CastleGatehousePlan expected = CastleGatehousePlanner.Create(
                in castle, in gate, seed);
            CastleGatehousePlan actual = completed.Topology.Gatehouse;

            Assert.IsTrue(completed.Topology.HasGatehousePlan);
            AssertGatehouseEquals(in expected, in actual, "completion did not freeze seeded gatehouse");
        }

        [Test]
        public void ValidatorRejectsSupportOutsideBridgeDeck()
        {
            CastlePlan castle = CastlePlanner.Create(int3.zero, 41u);
            CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(in castle);
            gatehouse.BridgeSupportOffset = gatehouse.BridgeWidth;

            Assert.IsFalse(
                CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue));
            Assert.AreEqual(CastleGatehousePlanIssue.InvalidBridgeSupports, issue);
        }

        [Test]
        public void ValidatorRejectsMasonryThatCannotSpanOpening()
        {
            CastlePlan castle = CastlePlanner.Create(int3.zero, 43u);
            CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(in castle);
            gatehouse.BlockHeight = gatehouse.OpeningHeight;

            Assert.IsFalse(
                CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue));
            Assert.AreEqual(CastleGatehousePlanIssue.InvalidMasonry, issue);
        }

        [Test]
        public void ValidatorRejectsInvalidGateLeafStrapPattern()
        {
            CastlePlan castle = CastlePlanner.Create(int3.zero, 47u);
            CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(in castle);
            gatehouse.GateLeafStrapSpacing = 0;

            Assert.IsFalse(
                CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue));
            Assert.AreEqual(CastleGatehousePlanIssue.InvalidGateLeaf, issue);
        }

        private static void AssertGatehouseEquals(
            in CastleGatehousePlan expected,
            in CastleGatehousePlan actual,
            string message)
        {
            Assert.AreEqual(expected.TowerSpacing, actual.TowerSpacing, message);
            Assert.AreEqual(expected.LeftTowerHeight, actual.LeftTowerHeight, message);
            Assert.AreEqual(expected.RightTowerHeight, actual.RightTowerHeight, message);
            Assert.AreEqual(expected.BlockHeight, actual.BlockHeight, message);
            Assert.AreEqual(expected.OpeningHeight, actual.OpeningHeight, message);
            Assert.AreEqual(expected.GateLeafStrapFirstY, actual.GateLeafStrapFirstY, message);
            Assert.AreEqual(expected.GateLeafStrapSpacing, actual.GateLeafStrapSpacing, message);
            Assert.AreEqual(expected.GateLeafStrapThickness, actual.GateLeafStrapThickness, message);
            Assert.AreEqual(expected.BridgeNearDistance, actual.BridgeNearDistance, message);
            Assert.AreEqual(expected.BridgeLength, actual.BridgeLength, message);
            Assert.AreEqual(expected.BridgeWidth, actual.BridgeWidth, message);
            Assert.AreEqual(expected.BridgeDeckYOffset, actual.BridgeDeckYOffset, message);
            Assert.AreEqual(expected.BridgeDeckHeight, actual.BridgeDeckHeight, message);
            Assert.AreEqual(expected.BridgeSupportOffset, actual.BridgeSupportOffset, message);
            Assert.AreEqual(expected.BridgeSupportYOffset, actual.BridgeSupportYOffset, message);
            Assert.AreEqual(expected.BridgeSupportHeight, actual.BridgeSupportHeight, message);
            Assert.AreEqual(expected.BridgeSupportThickness, actual.BridgeSupportThickness, message);
            Assert.AreEqual(expected.BridgeRailYOffset, actual.BridgeRailYOffset, message);
            Assert.AreEqual(expected.BridgeRailHeight, actual.BridgeRailHeight, message);
            Assert.AreEqual(expected.BridgeRailThickness, actual.BridgeRailThickness, message);

            AssertSlitsEqual(expected.LeftTowerSlits, actual.LeftTowerSlits, message);
            AssertSlitsEqual(expected.RightTowerSlits, actual.RightTowerSlits, message);
        }

        private static void AssertSlitsEqual(
            CastleTowerSlitPlan expected,
            CastleTowerSlitPlan actual,
            string message)
        {
            Assert.NotNull(expected, message);
            Assert.NotNull(actual, message);
            Assert.AreEqual(expected.FloorCount, actual.FloorCount, message);
            for (int floor = 0; floor < expected.FloorCount; floor++)
                Assert.AreEqual(expected.PhaseRadiansAt(floor), actual.PhaseRadiansAt(floor), message);
        }
    }
}
