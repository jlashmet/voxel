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
                    $"seed {seed}: tower spacing changed during planning migration");
                Assert.AreEqual(castle.GateTowerHeight + 38, gatehouse.LeftTowerHeight);
                Assert.AreEqual(castle.GateTowerHeight + 12, gatehouse.RightTowerHeight);
                Assert.AreEqual(castle.WallHeight + 22, gatehouse.BlockHeight);
                Assert.AreEqual(CastleLayout.FrontGateHeight + 14, gatehouse.OpeningHeight);
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
            }
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
    }
}
