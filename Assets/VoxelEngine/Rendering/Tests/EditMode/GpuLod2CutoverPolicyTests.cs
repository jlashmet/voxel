using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuLod2CutoverPolicyTests
    {
        [Test]
        public void ProductionGpuCutoverDefaultsOnWithExplicitDisableFallback()
        {
            Assert.False(GpuSurfaceProductionPolicy.ShouldDisableLegacyGpuCutover(null, null),
                "Supported near-ring GPU extraction must be enabled in production by default.");
            Assert.True(GpuSurfaceProductionPolicy.ShouldDisableLegacyGpuCutover("1", null),
                "VOXEL_DISABLE_GPU_CUTOVER=1 must retain an emergency/A-B CPU fallback.");
            Assert.False(GpuSurfaceProductionPolicy.ShouldDisableLegacyGpuCutover(null, "1"),
                "The retired experimental opt-in must not be required for production GPU cutover.");
        }

        [Test]
        public void SceneIssue20260823014011920GpuCutoverTargetsOnlyNearExactRings()
        {
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(1),
                "Full-resolution surface extraction must be GPU-capable.");
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(2),
                "LOD2 must remain GPU-capable, including its transition-face path.");
            Assert.False(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(4),
                "The step-4 feature-preserving exact/fallback ring stays on CPU until GPU parity exists.");
            Assert.False(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(8),
                "Block HLOD remains the step-8 backend.");
        }

        [Test]
        public void SceneIssue20260823014011920GpuLod2PortsCoarseExposedMaterialCorrection()
        {
            string densityShader = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Resources/VoxelBrickDensity.hlsl");
            string mesher = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute");

            StringAssert.Contains("if (sourceStep > 1)", densityShader);
            StringAssert.Contains("if (centreSolid)", densityShader);
            StringAssert.Contains("PreferNearestCrossingSurfaceMaterial(", densityShader);
            StringAssert.Contains("for (int distance = 1; distance < sourceStep; distance++)", densityShader);
            StringAssert.Contains("p + direction * sourceStep", densityShader);
            StringAssert.Contains("DecodeSurfaceStorage", densityShader,
                "GPU density sampling must decode Storage's packed ushort surface semantics.");
            StringAssert.Contains("SampleField(p, _SourceStep, material, surface, boundary)", mesher);
        }

        [Test]
        public void UnsupportedGpuClassificationCannotBeMistakenForAnEmptyChunk()
        {
            var counts = new GpuExtractionCounts(0, 0, unsupported: true);

            Assert.True(counts.Unsupported);
            Assert.False(counts.IsEmpty,
                "An unsupported decorated/faceted chunk must take the CPU fidelity path; "
              + "publishing it as empty would create a visible hole.");
        }

        [Test]
        public void SceneIssue20260823014011920GpuLod2CarriesInnerTransitionFaceMask()
        {
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 2)
            {
                MinViewDistanceMetres = 130f
            };

            int mask = cache.BuildGpuTransitionFaceMask(
                new int3(1, 0, 0), voxelSize: 1f, cameraPosition: Vector3.zero);

            Assert.AreEqual(1 << 0, mask,
                "At the positive-X inner edge only the -X neighbour belongs to the finer ring; "
              + "GPU LOD2 must request that transition face with the same bit ordering as Transvoxel.");
        }
    }
}
