using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    /// <summary>WB039 proof that different house presets stay on one config/compiler path.</summary>
    public sealed class HouseVariantPresetTests
    {
        [Test]
        public void VariantPresetsExposeMateriallyDifferentHousePolicy()
        {
            HouseConfig cottage = HousePresets.CottageCompatibility(7, 11);
            HouseConfig townhouse = HouseVariantPresets.TallTownhouse(7, 11);
            HouseConfig farmhouse = HouseVariantPresets.WideFarmhouse(7, 11);

            Assert.AreEqual(64, cottage.Width);
            Assert.AreEqual(1, cottage.FloorCount);

            Assert.AreEqual(48, townhouse.Width);
            Assert.AreEqual(56, townhouse.Depth);
            Assert.AreEqual(2, townhouse.FloorCount);
            Assert.AreEqual(52, townhouse.Walls.Height);
            Assert.AreEqual(1, townhouse.RoofPitchRun);
            Assert.AreEqual(2, townhouse.RoofEaveOverhang);

            Assert.AreEqual(80, farmhouse.Width);
            Assert.AreEqual(64, farmhouse.Depth);
            Assert.AreEqual(1, farmhouse.FloorCount);
            Assert.AreEqual(30, farmhouse.Walls.Height);
            Assert.AreEqual(RoofAxis.X, farmhouse.RoofRidgeAxis);
            Assert.AreEqual(3, farmhouse.RoofPitchRun);
            Assert.AreEqual(3, farmhouse.RoofEaveOverhang);

            Assert.IsTrue(townhouse.Footprint.IsWellFormed);
            Assert.IsTrue(townhouse.Walls.IsWellFormed);
            Assert.IsTrue(townhouse.Floors.IsWellFormed);
            Assert.IsTrue(farmhouse.Footprint.IsWellFormed);
            Assert.IsTrue(farmhouse.Walls.IsWellFormed);
            Assert.IsTrue(farmhouse.Floors.IsWellFormed);
        }

        [Test]
        public void VariantPresetsCompileThroughSameBuilderToDistinctDeterministicPrograms()
        {
            HouseConfig cottage = HousePresets.CottageCompatibility(7, 11);
            HouseConfig townhouse = HouseVariantPresets.TallTownhouse(7, 11);
            HouseConfig farmhouse = HouseVariantPresets.WideFarmhouse(7, 11);

            int[] cottageProgram = HouseProgramCompiler.BuildCompatibilityProgram(in cottage, 0, 1);
            int[] townhouseProgram = HouseProgramCompiler.BuildCompatibilityProgram(in townhouse, 0, 1);
            int[] farmhouseProgram = HouseProgramCompiler.BuildCompatibilityProgram(in farmhouse, 0, 1);

            CollectionAssert.AreEqual(
                townhouseProgram,
                HouseProgramCompiler.BuildCompatibilityProgram(in townhouse, 0, 1));
            CollectionAssert.AreEqual(
                farmhouseProgram,
                HouseProgramCompiler.BuildCompatibilityProgram(in farmhouse, 0, 1));

            AssertProgramsDiffer(cottageProgram, townhouseProgram, "cottage vs townhouse");
            AssertProgramsDiffer(cottageProgram, farmhouseProgram, "cottage vs farmhouse");
            AssertProgramsDiffer(townhouseProgram, farmhouseProgram, "townhouse vs farmhouse");
        }

        private static void AssertProgramsDiffer(int[] left, int[] right, string description)
        {
            if (left.Length != right.Length)
                return;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return;
            }

            Assert.Fail($"Expected materially different compiled programs for {description}.");
        }
    }
}
