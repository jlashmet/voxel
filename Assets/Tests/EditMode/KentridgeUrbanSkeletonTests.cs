using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanSkeletonTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanSkeletonDefinesPrimaryClimbEastLoopAndWestAlternateAscent()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);

            Assert.AreEqual(10, plan.Nodes.Count);
            Assert.AreEqual(11, plan.Links.Count);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.X, market.CentreDm.X);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.Y, market.CentreDm.Y);
            Assert.AreEqual(4, market.Importance);

            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);
            Assert.AreEqual(KentridgeUrbanBand.CivicCrown, civic.Band);
            Assert.AreEqual(4, civic.Importance);
            Assert.Less(civic.CentreDm.Y, market.CentreDm.Y,
                "The civic crown should sit north/uphill of the market in Kentridge's primary climb.");

            KentridgeUrbanNode westLanding = plan.Get(KentridgeUrbanNodeId.WestMarketLanding);
            Assert.AreEqual(KentridgeUrbanCirculation.LowerWestStairXDm, westLanding.CentreDm.X);
            Assert.AreEqual(KentridgeUrbanCirculation.LowerWestStairNorthZDm, westLanding.CentreDm.Y);
            Assert.AreEqual(KentridgeUrbanBand.MarketBelt, westLanding.Band);

            int secondaryContours = 0;
            int secondaryStairs = 0;
            bool upperToRidge = false;
            bool residentialToWest = false;
            bool westToMarket = false;

            for (int i = 0; i < plan.Links.Count; i++)
            {
                KentridgeUrbanLink link = plan.Links[i];
                if (link.Kind == KentridgeUrbanLinkKind.SecondaryContour)
                {
                    secondaryContours++;
                    if (link.From == KentridgeUrbanNodeId.UpperLanding
                        && link.To == KentridgeUrbanNodeId.EastRidgeLanding)
                        upperToRidge = true;
                }
                else if (link.Kind == KentridgeUrbanLinkKind.SecondaryStair)
                {
                    secondaryStairs++;
                    if (link.From == KentridgeUrbanNodeId.ResidentialJunction
                        && link.To == KentridgeUrbanNodeId.WestMarketLanding)
                        residentialToWest = true;
                    if (link.From == KentridgeUrbanNodeId.WestMarketLanding
                        && link.To == KentridgeUrbanNodeId.MarketSquare)
                        westToMarket = true;
                }
            }

            Assert.AreEqual(1, secondaryContours);
            Assert.AreEqual(2, secondaryStairs,
                "The west alternate ascent should be represented as a two-link pedestrian route through its landing.");
            Assert.IsTrue(upperToRidge,
                "The east ridge must reconnect to the central upper town instead of remaining an island.");
            Assert.IsTrue(residentialToWest);
            Assert.IsTrue(westToMarket);
        }

        [Test]
        public void MajorNodesReservePublicSpaceInsteadOfTreatingTheTownAsContinuousBuildingFill()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            KentridgeUrbanNode upper = plan.Get(KentridgeUrbanNodeId.UpperLanding);
            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);
            KentridgeUrbanNode westLanding = plan.Get(KentridgeUrbanNodeId.WestMarketLanding);

            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.X, 100);
            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.Y, 60);
            Assert.Greater(upper.OpenSpaceHalfExtentsDm.X, 0);
            Assert.Greater(civic.OpenSpaceHalfExtentsDm.X, upper.OpenSpaceHalfExtentsDm.X,
                "The civic crown should have a stronger public-space reservation than the upper landing.");
            Assert.Greater(westLanding.OpenSpaceHalfExtentsDm.X, 0,
                "The alternate ascent should terminate in a real landing rather than directly into a building edge.");
        }
    }
}
