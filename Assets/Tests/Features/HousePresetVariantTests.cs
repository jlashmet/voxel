using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class HousePresetVariantTests
    {
        [Test]
        public void FarmhouseAndTownhouseUseSameHouseConfigButDifferMaterially()
        {
            HouseConfig cottage = HousePresets.CottageCompatibility(11, 22);
            HouseConfig farmhouse = HousePresetVariants.Farmhouse(11, 22);
            HouseConfig townhouse = HousePresetVariants.Townhouse(11, 22);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(64, cottage.Width);
                Assert.AreEqual(64, cottage.Depth);
                Assert.AreEqual(1, cottage.FloorCount);

                Assert.AreEqual(80, farmhouse.Width);
                Assert.AreEqual(56, farmhouse.Depth);
                Assert.AreEqual(2, farmhouse.FloorCount);
                Assert.AreEqual(RoofStyle.Gable, farmhouse.Roof.Style);
                Assert.AreEqual(RoofAxis.X, farmhouse.Roof.RidgeAxis);
                Assert.AreEqual(4, farmhouse.FrontWindows.Count);
                Assert.IsTrue(farmhouse.Chimney.Enabled);
                Assert.AreEqual(1, farmhouse.ExteriorFeatures.Length);

                Assert.AreEqual(40, townhouse.Width);
                Assert.AreEqual(64, townhouse.Depth);
                Assert.AreEqual(3, townhouse.FloorCount);
                Assert.AreEqual(3, townhouse.WallThickness);
                Assert.AreEqual(RoofStyle.Hip, townhouse.Roof.Style);
                Assert.AreEqual(3, townhouse.FrontWindows.Count);
                Assert.AreEqual(1, townhouse.RearDoors.Count);

                Assert.AreNotEqual(cottage.Width, farmhouse.Width);
                Assert.AreNotEqual(farmhouse.Width, townhouse.Width);
                Assert.AreNotEqual(farmhouse.FloorCount, townhouse.FloorCount);
                Assert.AreNotEqual(farmhouse.Roof.Style, townhouse.Roof.Style);
            });
        }

        [Test]
        public void VariedPresetDetailConfigsRemainWellFormed()
        {
            HouseConfig farmhouse = HousePresetVariants.Farmhouse(7, 9);
            HouseConfig townhouse = HousePresetVariants.Townhouse(7, 9);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(farmhouse.Footprint.IsWellFormed);
                Assert.IsTrue(farmhouse.Walls.IsWellFormed);
                Assert.IsTrue(farmhouse.Floors.IsWellFormed);
                Assert.IsTrue(farmhouse.FrontDoors.IsWellFormed);
                Assert.IsTrue(farmhouse.FrontWindows.IsWellFormed);
                Assert.IsTrue(farmhouse.RearWindows.IsWellFormed);
                Assert.IsTrue(farmhouse.LeftWindows.IsWellFormed);
                Assert.IsTrue(farmhouse.RightWindows.IsWellFormed);
                Assert.IsTrue(farmhouse.Roof.IsWellFormed);
                Assert.IsTrue(farmhouse.Chimney.IsWellFormed);
                Assert.IsTrue(farmhouse.ExteriorFeatures[0].IsWellFormed);

                Assert.IsTrue(townhouse.Footprint.IsWellFormed);
                Assert.IsTrue(townhouse.Walls.IsWellFormed);
                Assert.IsTrue(townhouse.Floors.IsWellFormed);
                Assert.IsTrue(townhouse.FrontDoors.IsWellFormed);
                Assert.IsTrue(townhouse.RearDoors.IsWellFormed);
                Assert.IsTrue(townhouse.FrontWindows.IsWellFormed);
                Assert.IsTrue(townhouse.RearWindows.IsWellFormed);
                Assert.IsTrue(townhouse.Roof.IsWellFormed);
            });
        }

        [Test]
        public void DetailedFarmhouseProgramIsDeterministic()
        {
            HouseConfig farmhouse = HousePresetVariants.Farmhouse(7, 9);

            int[] first = HouseProgramCompiler.BuildProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);
            int[] second = HouseProgramCompiler.BuildProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);

            CollectionAssert.AreEqual(first, second,
                "same house config must compile to the exact same shape program");
        }

        [Test]
        public void DetailedFarmhouseFoundationMatchesConfiguredFootprint()
        {
            HouseConfig farmhouse = HousePresetVariants.Farmhouse(7, 9);
            int[] program = HouseProgramCompiler.BuildProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);

            Assert.Multiple(() =>
            {
                Assert.AreEqual((int)ShapeOp.EmitBox, program[0]);
                Assert.AreEqual(0, program[2], "foundation X origin must be definition-local");
                Assert.AreEqual(0, program[3], "foundation Y origin must begin at the base plane");
                Assert.AreEqual(0, program[4], "foundation Z origin must be definition-local");
                Assert.AreEqual(farmhouse.Width, program[5]);
                Assert.AreEqual(farmhouse.FoundationDepth, program[6]);
                Assert.AreEqual(farmhouse.Depth, program[7]);
            });
        }

        [Test]
        public void HouseCompilerRejectsFootprintWithoutNavigableInterior()
        {
            HouseConfig invalid = HousePresets.CottageCompatibility(7, 9);
            invalid.Footprint.Primary = new Unity.Mathematics.int2(0, 0) == default
                ? new StructureFootprintRect(
                    new Unity.Mathematics.int2(0, 0),
                    new Unity.Mathematics.int2(8, 8))
                : invalid.Footprint.Primary;
            invalid.Walls.Thickness = 4;

            Assert.Throws<System.ArgumentException>(() =>
                HouseProgramCompiler.BuildProgram(
                    in invalid,
                    mainDoorAnchorIndex: 0,
                    hearthAnchorIndex: 1));
        }
    }
}
