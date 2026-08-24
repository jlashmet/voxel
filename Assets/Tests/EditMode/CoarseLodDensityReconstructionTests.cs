using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CoarseLodDensityReconstructionTests
    {
        [Test]
        public void Step2LayeredTerrainPreservesOneVoxelSurfacePhase()
        {
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;

            // The same horizontal surface, translated upward by one authoritative voxel, should
            // translate the reconstructed crossing by one voxel as well. At SourceStep 2 both
            // surfaces occupy the same coarse edge, so this specifically catches a density field
            // that loses sub-step phase and turns gradual slopes into two-voxel contour terraces.
            float lower = ReconstructedCrossingY(
                sourceStep: 2, topSolidY: 0, surfaces, coatings, palette);
            float raised = ReconstructedCrossingY(
                sourceStep: 2, topSolidY: 1, surfaces, coatings, palette);

            float translated = raised - lower;
            Assert.That(translated, Is.EqualTo(1f).Within(0.25f),
                $"SourceStep 2 moved the reconstructed surface only {translated:F3} voxel when "
              + "the authoritative layered terrain moved by one voxel. That phase loss quantizes "
              + "a smooth heightfield into coarse contour rings/terraces.");
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        public void LayeredTerrainPreservesEverySubstepSurfacePhase(int sourceStep)
        {
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;

            for (int topSolidY = 0; topSolidY < sourceStep; topSolidY++)
            {
                float crossing = ReconstructedCrossingY(
                    sourceStep, topSolidY, surfaces, coatings, palette);
                float expected = topSolidY + 0.5f;
                Assert.That(crossing, Is.EqualTo(expected).Within(0.25f),
                    $"SourceStep {sourceStep} topSolidY={topSolidY} reconstructed at {crossing:F3} "
                  + $"instead of {expected:F3}. A coarse scalar field must retain the fine-voxel "
                  + "phase inside each coarse edge or a smooth heightfield becomes contour bands.");
            }
        }

        [Test]
        public void Step2LayeredSlopeUsesVisibleTopSurfaceMaterial()
        {
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;

            CpuDensitySample sample = CpuDensityOracle.SampleLayeredSlopeEdgeAtOrigin(
                sourceStep: 2, surfaceMaterial: 1, subsurfaceMaterial: 2,
                surfaces, coatings, palette);

            Assert.Greater(sample.Density, 0f,
                "The coarse sample must remain inside the terrain for this reproduction.");
            Assert.That(sample.Material, Is.EqualTo(1),
                "SceneIssue 014011: a lateral air crossing must not make a coarse terrain sample "
              + "render the buried subsurface material when the layered top-surface voxel is exposed "
              + "one voxel above it.");
        }

        private static float ReconstructedCrossingY(
            int sourceStep, int topSolidY,
            in SurfaceCatalogueView surfaces,
            in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            int lowerSampleY = FloorToStep(topSolidY, sourceStep);
            int upperSampleY = lowerSampleY + sourceStep;

            CpuDensitySample lower = CpuDensityOracle.SampleLayeredColumnAtOrigin(
                sourceStep, topSolidY - lowerSampleY,
                surfaceMaterial: 1, subsurfaceMaterial: 2,
                surfaces, coatings, palette);
            CpuDensitySample upper = CpuDensityOracle.SampleLayeredColumnAtOrigin(
                sourceStep, topSolidY - upperSampleY,
                surfaceMaterial: 1, subsurfaceMaterial: 2,
                surfaces, coatings, palette);

            Assert.Greater(lower.Density, 0f,
                "The lower endpoint of the test edge must be inside the terrain.");
            Assert.Less(upper.Density, 0f,
                "The upper endpoint of the test edge must be outside the terrain.");

            float t = lower.Density / (lower.Density - upper.Density);
            return lowerSampleY + t * sourceStep;
        }

        private static int FloorToStep(int value, int step)
        {
            int quotient = value / step;
            if (value < 0 && value % step != 0) quotient--;
            return quotient * step;
        }
    }
}
