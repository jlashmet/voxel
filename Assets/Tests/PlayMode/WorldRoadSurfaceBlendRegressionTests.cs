using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WorldRoadSurfaceBlendRegressionTests
    {
        [Test]
        public void FractionalCorridorCoverageRoundTripsAsContinuousMaterialBlendWithoutStrideGrowth()
        {
            var corridor = new Primitive
            {
                Shape = PrimitiveShape.TerrainCorridor,
                Mode = PrimitiveMode.TerrainCorridor,
                Material = 13,
                A = new int3(0, 0, 0),
                B = new int3(100, 0, 0),
                InnerRadius = 10,
                Radius = 20,
                C = new int3(12, 4, 24),
                D = new int3(0, 0x524F4144, 1),
            };

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in corridor, 50, 15, out TerrainCorridorSample shoulder));
            Assert.That(shoulder.Coverage31, Is.GreaterThan(0).And.LessThan(31),
                "The production corridor sample must exercise a fractional shoulder rather than a core/outside endpoint.");

            var localSurface = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
                Flags = VoxelSurfaceFlags.PreserveFeature,
            };
            VoxelSurfaceSemantics authored = VoxelSurfaceSemantics.MaterialBlend(
                localSurface.ReconstructionStyleId,
                corridor.Material,
                shoulder.Coverage31,
                localSurface.Flags);
            VoxelSurfaceSemantics restored = VoxelSurfaceSemantics.FromStorage(authored.PackedStorage);

            Assert.IsTrue(restored.IsMaterialBlend);
            Assert.AreEqual(SurfaceStyles.Smooth, restored.ReconstructionStyleId);
            Assert.AreEqual(corridor.Material, restored.SecondaryMaterialId);
            Assert.AreEqual(shoulder.Coverage31, restored.BlendCoverage31,
                "Fractional road influence must survive persisted surface packing instead of collapsing to a binary material choice.");
            Assert.AreEqual(localSurface.Flags, restored.Flags);
            Assert.AreEqual(32, SmoothSurfaceVertex.Stride,
                "Continuous material presentation must reuse the existing vertex payload.");
            Assert.AreEqual(SmoothSurfaceVertex.Stride, Marshal.SizeOf<SmoothSurfaceVertex>());
        }

        [Test]
        public void BlendSecondaryMaterialCannotDisplaceDensityWhileOrdinaryCoatingStillDoes()
        {
            CoatingCatalogueView coatings = CoatingCatalogueView.CreateBuiltIns();
            var job = new TransvoxelDensityJob { Coatings = coatings };
            MethodInfo displacement = typeof(TransvoxelDensityJob).GetMethod(
                "CoatingDisplacement",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(displacement, "Regression must exercise the production CPU density coating path.");

            var ordinarySnow = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
                CoatingId = Coatings.Snow,
            };
            VoxelSurfaceSemantics blendUsingSnowByte = VoxelSurfaceSemantics.MaterialBlend(
                SurfaceStyles.Smooth,
                Coatings.Snow,
                coverage31: 17);

            float ordinaryDisplacement = (float)displacement.Invoke(job,
                new object[] { ordinarySnow.Packed });
            float blendDisplacement = (float)displacement.Invoke(job,
                new object[] { blendUsingSnowByte.Packed });

            Assert.Greater(ordinaryDisplacement, 0f,
                "Ordinary Snow coating must retain its pre-existing density displacement semantics.");
            Assert.AreEqual(0f, blendDisplacement,
                "The same packed nibble is a secondary material in blend mode and must be geometry-neutral.");

            SurfaceStyleReadDefinition reconstructed = SurfaceCatalogueView.CreateBuiltIns().Get(
                blendUsingSnowByte.StyleId);
            Assert.AreEqual(SurfaceReconstruction.Smooth, reconstructed.Reconstruction,
                "The material-blend marker must not select a different reconstruction style.");
        }
    }
}
