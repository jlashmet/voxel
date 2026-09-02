using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanSkeletonTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanSkeletonDefinesPrimaryClimbAndMultipleDistrictLoops()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(13, plan.Nodes.Count);
            Assert.AreEqual(16, plan.Links.Count);

            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.X, market.CentreDm.X);
            Assert.AreEqual(4, market.Importance);

            KentridgeUrbanNode westMarketJunction = plan.Get(KentridgeUrbanNodeId.WestMarketJunction);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westMarketJunction.CentreDm.X);
            Assert.AreEqual(KentridgeTownPlanner.MarketStreetZDm, westMarketJunction.CentreDm.Y);

            KentridgeUrbanNode westUpper = plan.Get(KentridgeUrbanNodeId.WestUpperLanding);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westUpper.CentreDm.X);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, westUpper.CentreDm.Y);

            KentridgeUrbanNode eastResidential = plan.Get(KentridgeUrbanNodeId.EastResidentialJunction);
            Assert.AreEqual(KentridgeTownPlanner.EastLaneXDm, eastResidential.CentreDm.X);
            Assert.AreEqual(KentridgeTownPlanner.ResidentialStreetZDm, eastResidential.CentreDm.Y);
            Assert.AreEqual(KentridgeUrbanNodeKind.Junction, eastResidential.Kind);

            int secondaryContours = 0;
            int secondaryStairs = 0;
            bool upperToRidge = false;
            bool westUpperToCentralUpper = false;
            bool residentialToEast = false;
            bool eastToWorking = false;

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
                }
                else if (link.Kind == KentridgeUrbanLinkKind.PrimaryStreet)
                {
                    if (link.From == KentridgeUrbanNodeId.ResidentialJunction && link.To == KentridgeUrbanNodeId.EastResidentialJunction) residentialToEast = true;
                    if (link.From == KentridgeUrbanNodeId.EastResidentialJunction && link.To == KentridgeUrbanNodeId.WorkingYard) eastToWorking = true;
                }
            }

            Assert.AreEqual(2, secondaryContours);
            Assert.AreEqual(3, secondaryStairs);
            Assert.IsTrue(upperToRidge);
            Assert.IsTrue(westUpperToCentralUpper);
            Assert.IsTrue(residentialToEast,
                "The existing residential street must be represented all the way to the east service lane.");
            Assert.IsTrue(eastToWorking,
                "The working yard should participate in the lower-east loop rather than remain a semantic dead end.");
        }

        [Test]
        public void MajorNodesReservePublicSpaceInsteadOfTreatingTheTownAsContinuousBuildingFill()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);
            KentridgeUrbanNode market = plan.Get(KentridgeUrbanNodeId.MarketSquare);
            KentridgeUrbanNode upper = plan.Get(KentridgeUrbanNodeId.UpperLanding);
            KentridgeUrbanNode civic = plan.Get(KentridgeUrbanNodeId.CivicCrown);
            KentridgeUrbanNode westUpper = plan.Get(KentridgeUrbanNodeId.WestUpperLanding);
            KentridgeUrbanNode eastResidential = plan.Get(KentridgeUrbanNodeId.EastResidentialJunction);

            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.X, 100);
            Assert.GreaterOrEqual(market.OpenSpaceHalfExtentsDm.Y, 60);
            Assert.Greater(upper.OpenSpaceHalfExtentsDm.X, 0);
            Assert.Greater(civic.OpenSpaceHalfExtentsDm.X, upper.OpenSpaceHalfExtentsDm.X);
            Assert.Greater(westUpper.OpenSpaceHalfExtentsDm.X, 0);
            Assert.Greater(eastResidential.OpenSpaceHalfExtentsDm.X, 0);
        }
    }
}
