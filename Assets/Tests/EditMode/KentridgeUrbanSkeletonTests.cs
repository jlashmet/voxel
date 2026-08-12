using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanSkeletonTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanSkeletonDefinesPrimaryClimbEastLoopAndTwoWestAlternateAscentStages()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(12, plan.Nodes.Count);
            Assert.AreEqual(14, plan.Links.Count);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.X, market.CentreDm.X);
            Assert.AreEqual(4, market.Importance);

            KentridgeUrbanNode westMarketJunction = plan.Get(KentridgeUrbanNodeId.WestMarketJunction);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westMarketJunction.CentreDm.X);
            Assert.AreEqual(KentridgeTownPlanner.MarketStreetZDm, westMarketJunction.CentreDm.Y);
            Assert.AreEqual(KentridgeUrbanNodeKind.Junction, westMarketJunction.Kind);

            KentridgeUrbanNode westUpper = plan.Get(KentridgeUrbanNodeId.WestUpperLanding);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westUpper.CentreDm.X);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, westUpper.CentreDm.Y);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, westUpper.Band);

            int secondaryContours = 0;
            int secondaryStairs = 0;
            bool upperToRidge = false;
            bool marketToWestJunction = false;
            bool westJunctionToUpper = false;
            bool westUpperToCentralUpper = false;

            for (int i = 0; i < plan.Links.Count; i++)
            {
                KentridgeUrbanLink link = plan.Links[i];
                if (link.Kind == KentridgeUrbanLinkKind.SecondaryContour)
                {
                    secondaryContours++;
                    if (link.From == KentridgeUrbanNodeId.UpperLanding && link.To == KentridgeUrbanNodeId.EastRidgeLanding) upperToRidge = true;
                    if (link.From == KentridgeUrbanNodeId.WestUpperLanding && link.To == KentridgeUrbanNodeId.UpperLanding) westUpperToCentralUpper = true;
                }
                else if (link.Kind == KentridgeUrbanLinkKind.SecondaryStair)
                {
                    secondaryStairs++;
                    if (link.From == KentridgeUrbanNodeId.WestMarketJunction && link.To == KentridgeUrbanNodeId.WestUpperLanding) westJunctionToUpper = true;
                }
                else if (link.Kind == KentridgeUrbanLinkKind.PrimaryStreet && link.From == KentridgeUrbanNodeId.MarketSquare && link.To == KentridgeUrbanNodeId.WestMarketJunction)
                {
                    marketToWestJunction = true;
                }
            }

            Assert.AreEqual(2, secondaryContours);
            Assert.AreEqual(3, secondaryStairs);
            Assert.IsTrue(upperToRidge);
            Assert.IsTrue(marketToWestJunction);
            Assert.IsTrue(westJunctionToUpper);
            Assert.IsTrue(westUpperToCentralUpper);
        }

        [Test]
        public void MajorNodesReservePublicSpaceInsteadOfTreatingTheTownAsContinuousBuildingFill()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);
            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            KentridgeUrbanNode upper = plan.Get(KentridgeUrbanNodeId.UpperLanding);
            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);
            KentridgeUrbanNode westUpper = plan.Get(KentridgeUrbanNodeId.WestUpperLanding);

            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.X, 100);
            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.Y, 60);
            Assert.Greater(upper.OpenSpaceHalfExtentsDm.X, 0);
            Assert.Greater(civic.OpenSpaceHalfExtentsDm.X, upper.OpenSpaceHalfExtentsDm.X);
            Assert.Greater(westUpper.OpenSpaceHalfExtentsDm.X, 0);
        }
    }
}
