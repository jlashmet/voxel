using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseRoofConfigTests
    {
        [Test]
        public void CottagePresetExposesSharedRoofControlsWithoutDormers()
        {
            HouseConfig house = HousePresets.CottageCompatibility(stoneMaterial: 1, woodMaterial: 2);

            Assert.AreEqual(RoofStyle.Gable, house.Roof.Style);
            Assert.AreEqual(RoofAxis.Z, house.Roof.RidgeAxis);
            Assert.AreEqual(1, house.Roof.PitchRise);
            Assert.AreEqual(2, house.Roof.PitchRun);
            Assert.AreEqual(0, house.Roof.EaveOverhang);
            Assert.AreEqual(StructureMaterialRole.Roof, house.Roof.MaterialRole);
            Assert.IsFalse(house.Dormers.Enabled);
            Assert.IsTrue(house.Dormers.IsWellFormed);
        }

        [Test]
        public void DormerHookAcceptsBoundedNonFlatRoofDetails()
        {
            var dormers = new HouseDormerConfig
            {
                Count = 2,
                Facade = HouseRoofFacade.Front,
                Width = 8,
                Height = 7,
                Depth = 6,
                Spacing = 10,
                EdgeMargin = 6,
                Style = RoofStyle.Gable,
                RoofMaterialRole = StructureMaterialRole.Roof,
                WallMaterialRole = StructureMaterialRole.SecondaryWall,
            };

            Assert.IsTrue(dormers.Enabled);
            Assert.IsTrue(dormers.IsWellFormed);

            dormers.Style = RoofStyle.Flat;
            Assert.IsFalse(dormers.IsWellFormed,
                "the dormer extension should use a supported pitched detail instead of silently inventing flat dormer geometry");
        }

        [Test]
        public void DormerHookRejectsInvalidDimensions()
        {
            var dormers = new HouseDormerConfig
            {
                Count = 1,
                Facade = HouseRoofFacade.Rear,
                Width = 0,
                Height = 8,
                Depth = 6,
                Style = RoofStyle.Gable,
            };

            Assert.IsFalse(dormers.IsWellFormed);
        }
    }
}
