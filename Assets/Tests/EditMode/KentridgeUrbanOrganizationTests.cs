using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanOrganizationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanPlanPreservesAxialAscentWhileDensifyingUpperBands()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);

            Assert.AreEqual(6, plan.FrontageRuns.Count);
            Assert.AreEqual(1, plan.Thresholds.Count);

            int civicRuns = 0;
            int westCivicEnd = int.MinValue;
            int eastCivicStart = int.MaxValue;

            for (int i = 0; i < plan.FrontageRuns.Count; i++)
            {
                KentridgeFrontageRun run = plan.FrontageRuns[i];
                Assert.IsTrue(run.IsHorizontal,
                    "The first authored massing plan uses contour-following horizontal frontage.");
                Assert.Greater(run.LengthDm, 100);
                Assert.That(run.CoveragePercent, Is.InRange(60, 90));
                Assert.That(run.MinStoreys, Is.InRange(2, 3));
                Assert.That(run.MaxStoreys, Is.InRange(run.MinStoreys, 3));

                if (run.Band == KentridgeUrbanBand.CivicCrown)
                {
                    civicRuns++;
                    if (run.EndDm.X <= KentridgeTownPlanner.MainSpineXDm)
                        westCivicEnd = System.Math.Max(westCivicEnd, run.EndDm.X);
                    if (run.StartDm.X >= KentridgeTownPlanner.MainSpineXDm)
                        eastCivicStart = System.Math.Min(eastCivicStart, run.StartDm.X);
                }
            }

            Assert.AreEqual(2, civicRuns);
            Assert.LessOrEqual(westCivicEnd, 1110);
            Assert.GreaterOrEqual(eastCivicStart, 1240);
            Assert.Greater(eastCivicStart - westCivicEnd,
                KentridgeTownPlanner.MainRoadWidthDm,
                "Civic mass must frame rather than close the main uphill sight/circulation axis.");

            KentridgeUrbanThreshold threshold = plan.Thresholds[0];
            Assert.AreEqual(KentridgeTownPlanner.MainSpineXDm, threshold.CentreDm.X);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, threshold.LowerBand);
            Assert.AreEqual(KentridgeUrbanBand.CivicCrown, threshold.UpperBand);
        }

        [Test]
        public void CoarseMassingAdapterRealizesThePlanWithoutCreatingGameplayStructures()
        {
            FeatureCatalogue massing = KentridgeUrbanMassingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(2, massing.Definitions.Length,
                    "CI massing adapter only needs two silhouette heights.");
                Assert.AreEqual(17, massing.ExplicitPlacements.Length,
                    "Six frontage runs should currently resolve to seventeen anonymous massing sites.");

                for (int i = 0; i < massing.Definitions.Length; i++)
                {
                    Assert.AreEqual(FeatureKind.Infrastructure, massing.Definitions[i].Kind);
                    Assert.AreEqual(86, massing.Definitions[i].Precedence);
                }
            }
            finally
            {
                massing.Dispose();
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
    }
}
