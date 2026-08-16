using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSiteGeometryPlanningTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void SitePlannerFreezesHistoricalGeometryRecipe()
        {
            CastleSitePlan site = CastleSitePlanner.Create(123u);
            CastleSiteGeometryPlan geometry = site.Geometry;

            Assert.AreEqual(3.7f, geometry.EdgeFrequencyA);
            Assert.AreEqual(18f, geometry.EdgeAmplitudeA);
            Assert.AreEqual(8.3f, geometry.EdgeFrequencyB);
            Assert.AreEqual(9f, geometry.EdgeAmplitudeB);
            Assert.AreEqual(17.1f, geometry.EdgeFrequencyC);
            Assert.AreEqual(4f, geometry.EdgeAmplitudeC);
            Assert.AreEqual(1.7f, geometry.CliffFalloffExponent);
            Assert.AreEqual(11f, geometry.CliffNoiseAngularFrequency);
            Assert.AreEqual(6f, geometry.CliffNoiseProgressFrequency);
            Assert.AreEqual(0.10f, geometry.CliffNoiseAmplitude);
            Assert.AreEqual(14, geometry.CliffGroundInset);
            Assert.AreEqual(12, geometry.GrassEdgeInset);
            Assert.AreEqual(8, geometry.ApproachReachInset);
            Assert.AreEqual(92, geometry.RiverOffset);
            Assert.AreEqual(90, geometry.RiverHalfWidth);
            Assert.AreEqual(42, geometry.WaterHalfWidth);
            Assert.AreEqual(CastleLayout.LowerRiverDepth, geometry.RiverDepth);
            Assert.AreEqual(0.028f, geometry.MeanderFrequencyA);
            Assert.AreEqual(8f, geometry.MeanderAmplitudeA);
            Assert.AreEqual(0.071f, geometry.MeanderFrequencyB);
            Assert.AreEqual(3f, geometry.MeanderAmplitudeB);

            CastleRiverCrossSectionPlan crossSection = geometry.RiverCrossSection;
            Assert.AreEqual(0.18f, crossSection.BankBlendStart);
            Assert.AreEqual(1f, crossSection.BankBlendEnd);
            Assert.AreEqual(32, crossSection.OutsideTerraceDrop);
            Assert.AreEqual(1, crossSection.InsideTerraceDrop);
            Assert.AreEqual(0.38f, crossSection.LooseBankThreshold);
            Assert.AreEqual(0.46f, crossSection.DeepSoilThreshold);
            Assert.AreEqual(0.56f, crossSection.GrassThreshold);
            Assert.AreEqual(2, crossSection.ShallowSoilDepth);
            Assert.AreEqual(5, crossSection.DeepSoilDepth);
            Assert.AreEqual(10, crossSection.BedDepth);
            Assert.AreEqual(4, crossSection.BedRise);
            Assert.AreEqual(20, crossSection.ExistingSurfaceRejectDepth);
            Assert.AreEqual(8, crossSection.SurfaceClearance);
        }

        [Test]
        public void SpatialSiteRealizerConsumesFrozenGeometryPlan()
        {
            string site = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSiteRealizer.cs"));

            StringAssert.Contains("CastleSiteGeometryPlan geometry = sitePlan.Geometry", site);
            StringAssert.Contains("geometry.EdgeFrequencyA", site);
            StringAssert.Contains("geometry.CliffFalloffExponent", site);
            StringAssert.Contains("geometry.GrassEdgeInset", site);
            StringAssert.Contains("in CastleSiteGeometryPlan geometry", site);
            StringAssert.Contains("geometry.RiverHalfWidth", site);
            StringAssert.Contains("geometry.RiverDepth", site);
            StringAssert.Contains("geometry.MeanderFrequencyA", site);
            StringAssert.Contains("geometry.RiverCrossSection", site);
            StringAssert.Contains("crossSection.BankBlendStart", site);
            StringAssert.Contains("crossSection.OutsideTerraceDrop", site);
            StringAssert.Contains("crossSection.DeepSoilThreshold", site);
            StringAssert.Contains("crossSection.GrassThreshold", site);
            StringAssert.Contains("crossSection.BedDepth", site);
        }
    }
}
