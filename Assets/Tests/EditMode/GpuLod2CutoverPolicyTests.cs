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
        public void SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings()
        {
            Assert.False(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                "Production must not hard-disable the validated GPU extraction backend for exact rings.");
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(1),
                "The exact base ring must remain eligible for GPU extraction.");
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(2),
                "LOD2 is an exact ring and must remain eligible for GPU extraction.");
        }

        [Test]
        public void SceneIssue20260823014011920GpuCutoverTargetsOnlyNearExactRings()
        {
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(1),
                "The base exact ring should remain on the GPU path.");
            Assert.True(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(2),
                "LOD2 should join the GPU cutover after parity is restored.");
            Assert.False(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(4),
                "LOD4 still relies on the feature-preserving CPU fallback.");
            Assert.False(CpuTransvoxelChunkCache.SupportsGpuSurfaceStep(8),
                "Block HLOD remains a separate coarse representation.");
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
                "LOD2 GPU chunks adjacent to the inner ring must emit the matching transition face.");
        }
    }
}
