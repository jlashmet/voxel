using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    /// <summary>WB034 coverage for facade-specific house doors and entry treatment.</summary>
    public sealed class HouseDoorConfigTests
    {
        [Test]
        public void CottagePresetKeepsOneCenteredFrontDoorOnly()
        {
            HouseConfig house = HousePresets.CottageCompatibility(stoneMaterial: 7, woodMaterial: 11);

            Assert.AreEqual(HouseFacade.Front, house.FrontDoors.Facade);
            Assert.AreEqual(HouseFacadePlacementMode.Centered, house.FrontDoors.Placement);
            Assert.AreEqual(1, house.FrontDoors.Count);
            Assert.AreEqual(StructureOpeningKind.Door, house.FrontDoors.Opening.Kind);
            Assert.AreEqual(12, house.FrontDoors.Opening.Width);
            Assert.AreEqual(20, house.FrontDoors.Opening.Height);
            Assert.IsTrue(house.FrontDoors.IsWellFormed);

            Assert.AreEqual(0, house.RearDoors.Count);
            Assert.AreEqual(0, house.LeftDoors.Count);
            Assert.AreEqual(0, house.RightDoors.Count);
        }

        [Test]
        public void FacadeDoorLayoutExposesPlacementFramesAndPorchSteps()
        {
            HouseConfig house = HousePresets.CottageCompatibility(stoneMaterial: 3, woodMaterial: 5);
            HouseDoorLayoutConfig rear = house.RearDoors;
            rear.Placement = HouseFacadePlacementMode.ExplicitOffsets;
            rear.Count = 2;
            rear.ExplicitOffsets.Add(12);
            rear.ExplicitOffsets.Add(40);
            rear.Opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Door,
                Width = 10,
                Height = 18,
                BottomOffset = 0,
                Spacing = 0,
                StartMargin = 0,
                EndMargin = 0,
                FrameThickness = 2,
                LintelThickness = 1,
                WidthVariation = 0,
                HeightVariation = 0,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Opening,
            };
            rear.EntryTreatment = new HouseEntryTreatmentConfig
            {
                PorchWidth = 24,
                PorchDepth = 8,
                PorchHeight = 2,
                StepCount = 2,
                StepDepth = 2,
                StepHeight = 1,
                PorchMaterialRole = StructureMaterialRole.Floor,
                StepMaterialRole = StructureMaterialRole.Foundation,
            };
            house.RearDoors = rear;

            Assert.IsTrue(house.RearDoors.IsWellFormed);
            Assert.AreEqual(HouseFacade.Rear, house.RearDoors.Facade);
            Assert.AreEqual(2, house.RearDoors.Count);
            Assert.AreEqual(HouseFacadePlacementMode.ExplicitOffsets, house.RearDoors.Placement);
            Assert.AreEqual(12, house.RearDoors.ExplicitOffsets[0]);
            Assert.AreEqual(40, house.RearDoors.ExplicitOffsets[1]);
            Assert.AreEqual(10, house.RearDoors.Opening.Width);
            Assert.AreEqual(18, house.RearDoors.Opening.Height);
            Assert.AreEqual(2, house.RearDoors.Opening.FrameThickness);
            Assert.AreEqual(1, house.RearDoors.Opening.LintelThickness);
            Assert.IsTrue(house.RearDoors.EntryTreatment.HasPorch);
            Assert.IsTrue(house.RearDoors.EntryTreatment.HasSteps);
        }

        [Test]
        public void EntryTreatmentRejectsIncompletePorchOrStepDimensions()
        {
            var treatment = new HouseEntryTreatmentConfig
            {
                PorchWidth = 16,
                PorchDepth = 0,
                PorchHeight = 2,
            };
            Assert.IsFalse(treatment.IsWellFormed);

            treatment = new HouseEntryTreatmentConfig
            {
                StepCount = 2,
                StepDepth = 0,
                StepHeight = 1,
            };
            Assert.IsFalse(treatment.IsWellFormed);
        }
    }
}
