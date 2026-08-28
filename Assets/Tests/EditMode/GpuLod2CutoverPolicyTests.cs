using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuLod2CutoverPolicyTests
    {
        [Test]
        public void ProductionSurfaceExtractionUsesGpuMesherForSupportedNearRings()
        {
            Assert.False(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Production must enable the GPU surface backend for supported near rings. "
              + "VOXEL_DISABLE_GPU_CUTOVER=1 remains an explicit diagnostic fallback only.");
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
            StringAssert.Contains("SampleField(p, _SourceStep, material, surface, boundary)", mesher);
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
