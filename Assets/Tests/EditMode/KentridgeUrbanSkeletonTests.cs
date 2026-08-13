using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanSkeletonTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanSkeletonDefinesAConnectedPrimaryClimbAndEastRidgeLoop()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);

            Assert.AreEqual(9, plan.Nodes.Count);
            Assert.AreEqual(9, plan.Links.Count);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.X, market.CentreDm.X);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.Y, market.CentreDm.Y);
            Assert.AreEqual(4, market.Importance);

            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);
            Assert.AreEqual(KentridgeUrbanBand.CivicCrown, civic.Band);
            Assert.AreEqual(4, civic.Importance);
            Assert.Less(civic.CentreDm.Y, market.CentreDm.Y,
                "The civic crown should sit north/uphill of the market in Kentridge's primary climb.");

            int secondaryContours = 0;
            bool upperToRidge = false;
            for (int i = 0; i < plan.Links.Count; i++)
            {
                KentridgeUrbanLink link = plan.Links[i];
                if (link.Kind != KentridgeUrbanLinkKind.SecondaryContour) continue;
                secondaryContours++;
                if (link.From == KentridgeUrbanNodeId.UpperLanding
                    && link.To == KentridgeUrbanNodeId.EastRidgeLanding)
                    upperToRidge = true;
            }

            Assert.AreEqual(1, secondaryContours,
                "The first secondary network should remain legible: one upper contour link.");
            Assert.IsTrue(upperToRidge,
                "The east ridge must reconnect to the central upper town instead of remaining an island.");
        }

        [Test]
        public void MajorNodesReservePublicSpaceInsteadOfTreatingTheTownAsContinuousBuildingFill()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            KentridgeUrbanNode upper = plan.Get(KentridgeUrbanNodeId.UpperLanding);
            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);

            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.X, 100);
            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.Y, 60);
            Assert.Greater(upper.OpenSpaceHalfExtentsDm.X, 0);
            Assert.Greater(civic.OpenSpaceHalfExtentsDm.X, upper.OpenSpaceHalfExtentsDm.X,
                "The civic crown should have a stronger public-space reservation than the upper landing.");
        }
    }
}
