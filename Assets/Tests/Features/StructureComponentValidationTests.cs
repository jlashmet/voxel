using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureComponentValidationTests
    {
        [Test]
        public void InvalidDimensionsAreRejected()
        {
            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Door,
                Width = 0,
                Height = 8,
                Spacing = 0,
            };

            Assert.AreEqual(
                StructureComponentValidationIssue.InvalidDimension,
                StructureComponentValidation.Opening(opening, wallRunLength: 24));
        }

        [Test]
        public void ImpossibleOpeningSpacingIsRejected()
        {
            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 6,
                Height = 8,
                Spacing = 5,
                StartMargin = 2,
                EndMargin = 2,
                WidthVariation = 1,
            };

            Assert.AreEqual(
                StructureComponentValidationIssue.ImpossibleOpeningSpacing,
                StructureComponentValidation.Opening(opening, wallRunLength: 32));
        }

        [Test]
        public void UnsupportedRoofCombinationIsRejected()
        {
            var flatWithPitch = new RoofConfig
            {
                Style = RoofStyle.Flat,
                PitchRise = 1,
                PitchRun = 2,
                Thickness = 2,
            };

            var gableWithoutPitch = new RoofConfig
            {
                Style = RoofStyle.Gable,
                PitchRise = 0,
                PitchRun = 0,
                Thickness = 2,
            };

            Assert.AreEqual(
                StructureComponentValidationIssue.UnsupportedRoofCombination,
                StructureComponentValidation.Roof(flatWithPitch));
            Assert.AreEqual(
                StructureComponentValidationIssue.UnsupportedRoofCombination,
                StructureComponentValidation.Roof(gableWithoutPitch));
        }

        [Test]
        public void BoundsOverflowIsRejected()
        {
            var bounds = new StructureGenerationBounds(
                new int3(-16, 0, -16),
                new int3(16, 32, 16));

            Assert.AreEqual(
                StructureComponentValidationIssue.None,
                StructureComponentValidation.VolumeWithinBounds(
                    bounds,
                    new int3(-8, 0, -8),
                    new int3(8, 16, 8)));

            Assert.AreEqual(
                StructureComponentValidationIssue.BoundsOverflow,
                StructureComponentValidation.VolumeWithinBounds(
                    bounds,
                    new int3(-8, 0, -8),
                    new int3(17, 16, 8)));
        }

        [Test]
        public void PrimitiveBudgetOverflowIsRejectedRatherThanTruncated()
        {
            Assert.AreEqual(
                StructureComponentValidationIssue.None,
                StructureComponentValidation.PrimitiveBudget(
                    emittedPrimitiveCount: 64,
                    declaredMaxPrimitives: 128));

            Assert.AreEqual(
                StructureComponentValidationIssue.PrimitiveBudgetOverflow,
                StructureComponentValidation.PrimitiveBudget(
                    emittedPrimitiveCount: 129,
                    declaredMaxPrimitives: 128));

            Assert.AreEqual(
                StructureComponentValidationIssue.PrimitiveBudgetOverflow,
                StructureComponentValidation.PrimitiveBudget(
                    emittedPrimitiveCount: FeatureBudget.MaxPrimitivesPerInstance + 1,
                    declaredMaxPrimitives: FeatureBudget.MaxPrimitivesPerInstance));
        }
    }
}
