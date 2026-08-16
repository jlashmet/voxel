using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRiverCrossSectionPlanTests
    {
        [Test]
        public void HistoricalCrossSectionFreezesLegacyGorgeProfile()
        {
            CastleRiverCrossSectionPlan profile = CastleSitePlanner.Create(211u).Geometry.RiverCrossSection;

            Assert.AreEqual(0.18f, profile.BankBlendStart);
            Assert.AreEqual(1f, profile.BankBlendEnd);
            Assert.AreEqual(32, profile.OutsideTerraceDrop);
            Assert.AreEqual(1, profile.InsideTerraceDrop);
            Assert.AreEqual(0.38f, profile.LooseBankThreshold);
            Assert.AreEqual(0.46f, profile.DeepSoilThreshold);
            Assert.AreEqual(0.56f, profile.GrassThreshold);
            Assert.AreEqual(2, profile.ShallowSoilDepth);
            Assert.AreEqual(5, profile.DeepSoilDepth);
            Assert.AreEqual(10, profile.BedDepth);
            Assert.AreEqual(4, profile.BedRise);
            Assert.AreEqual(20, profile.ExistingSurfaceRejectDepth);
            Assert.AreEqual(8, profile.SurfaceClearance);
        }

        [Test]
        public void ValidatorRejectsInvertedBankBlend()
        {
            CastleSitePlan generated = CastleSitePlanner.Create(223u);
            CastleSiteGeometryPlan source = generated.Geometry;
            CastleRiverCrossSectionPlan historical = source.RiverCrossSection;
            var invalidProfile = new CastleRiverCrossSectionPlan(
                bankBlendStart: 0.9f,
                bankBlendEnd: 0.2f,
                outsideTerraceDrop: historical.OutsideTerraceDrop,
                insideTerraceDrop: historical.InsideTerraceDrop,
                looseBankThreshold: historical.LooseBankThreshold,
                deepSoilThreshold: historical.DeepSoilThreshold,
                grassThreshold: historical.GrassThreshold,
                shallowSoilDepth: historical.ShallowSoilDepth,
                deepSoilDepth: historical.DeepSoilDepth,
                bedDepth: historical.BedDepth,
                bedRise: historical.BedRise,
                existingSurfaceRejectDepth: historical.ExistingSurfaceRejectDepth,
                surfaceClearance: historical.SurfaceClearance);
            CastleSiteGeometryPlan geometry = WithCrossSection(in source, in invalidProfile);
            var invalid = new CastleSitePlan(
                generated.GrassPatternSeed,
                generated.GrassCoveragePercent,
                generated.CourtyardPatternSeed,
                generated.CourtyardStonePercent,
                in geometry);

            Assert.IsFalse(CastleSitePlanValidator.TryValidate(
                in invalid, out CastleSitePlanIssue issue));
            Assert.AreEqual(CastleSitePlanIssue.InvalidRiverCrossSection, issue);
        }

        [Test]
        public void SpatialSiteRealizerConsumesCrossSectionInsteadOfOwningProfile()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Structures", "Runtime", "CastleSiteRealizer.cs"));

            StringAssert.Contains("geometry.RiverCrossSection", source);
            StringAssert.Contains("crossSection.BankBlendStart", source);
            StringAssert.Contains("crossSection.OutsideTerraceDrop", source);
            StringAssert.Contains("crossSection.DeepSoilThreshold", source);
            StringAssert.Contains("crossSection.GrassThreshold", source);
            StringAssert.Contains("crossSection.BedDepth", source);
            StringAssert.Contains("hasPlannedCrossSection", source);
        }

        private static CastleSiteGeometryPlan WithCrossSection(
            in CastleSiteGeometryPlan source,
            in CastleRiverCrossSectionPlan crossSection) =>
            new CastleSiteGeometryPlan(
                source.EdgeFrequencyA,
                source.EdgeAmplitudeA,
                source.EdgeFrequencyB,
                source.EdgeAmplitudeB,
                source.EdgeFrequencyC,
                source.EdgeAmplitudeC,
                source.CliffFalloffExponent,
                source.CliffNoiseAngularFrequency,
                source.CliffNoiseProgressFrequency,
                source.CliffNoiseAmplitude,
                source.CliffGroundInset,
                source.GrassEdgeInset,
                source.ApproachReachInset,
                source.RiverOffset,
                source.RiverHalfWidth,
                source.WaterHalfWidth,
                source.RiverDepth,
                source.MeanderFrequencyA,
                source.MeanderAmplitudeA,
                source.MeanderFrequencyB,
                source.MeanderAmplitudeB,
                in crossSection);

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
    }
}
