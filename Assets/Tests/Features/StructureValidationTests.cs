using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureValidationTests
    {
        [Test]
        public void InvalidDimensionsAreRejectedByDefault()
        {
            StructureValidationResult result = StructureConfigValidation.Dimension(
                value: 0,
                minimum: 1,
                maximum: 64,
                StructureValidationPolicy.Reject,
                out int resolved);

            Assert.AreEqual(StructureValidationResult.Rejected, result);
            Assert.AreEqual(0, resolved);
        }

        [Test]
        public void OpeningSpacingThatCanOverlapDeterministicVariationIsRejected()
        {
            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 6,
                Height = 8,
                Spacing = 6,
                StartMargin = 2,
                EndMargin = 2,
                FrameThickness = 1,
                LintelThickness = 1,
                WidthVariation = 2,
                HeightVariation = 1,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Glass,
            };

            Assert.IsFalse(opening.IsWellFormed,
                "spacing must fit the widest deterministic opening variant");
        }

        [Test]
        public void UnsupportedRoofCombinationsAreRejected()
        {
            var flatWithPitch = new RoofConfig
            {
                Style = RoofStyle.Flat,
                PitchRise = 1,
                PitchRun = 2,
                Thickness = 1,
                ParapetHeight = 0,
            };
            Assert.IsFalse(flatWithPitch.IsWellFormed);

            var pitchedWithParapet = new RoofConfig
            {
                Style = RoofStyle.Gable,
                PitchRise = 1,
                PitchRun = 2,
                Thickness = 1,
                ParapetHeight = 2,
            };
            Assert.IsFalse(pitchedWithParapet.IsWellFormed);

            var validGable = pitchedWithParapet;
            validGable.ParapetHeight = 0;
            Assert.IsTrue(validGable.IsWellFormed);
        }

        [Test]
        public void GenerationBoundsRejectIntegerOverflow()
        {
            Assert.IsFalse(StructureGenerationBounds.TryCreate(
                new int3(int.MaxValue - 4, 0, 0),
                new int3(8, 16, 16),
                out _));

            Assert.IsTrue(StructureGenerationBounds.TryCreate(
                new int3(int.MaxValue - 8, -4, -4),
                new int3(8, 8, 8),
                out StructureGenerationBounds valid));
            Assert.AreEqual(int.MaxValue, valid.MaxExclusive.x);
        }

        [Test]
        public void CatalogueRejectsPerInstancePrimitiveBudgetOverflow()
        {
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 0,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: 0,
                materials: 0,
                explicitPlacements: 0,
                overrides: 0,
                allocator: Allocator.Temp);

            try
            {
                catalogue.Version = FeatureCatalogueBuilder.SupportedVersion;
                catalogue.Definitions[0] = new FeatureDefinition
                {
                    Kind = FeatureKind.Structure,
                    Footprint = new int3(32, 32, 32),
                    MaxPrimitives = FeatureBudget.MaxPrimitivesPerInstance + 1,
                };

                Assert.AreEqual(
                    CatalogueLoadResult.PrimitiveBudgetExceeded,
                    FeatureCatalogueBuilder.Finalise(ref catalogue));
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
