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
        public void GpuCutoverTargetsNearExactRingsAndBlockHlod()
        {
            Assert.True(GpuSolidChunkCache.SupportsGpuSurfaceStep(1),
                "Full-resolution surface extraction must be GPU-capable.");
            Assert.True(GpuSolidChunkCache.SupportsGpuSurfaceStep(2),
                "LOD2 must remain GPU-capable, including its transition-face path.");
            Assert.True(GpuSolidChunkCache.SupportsGpuSurfaceStep(4),
                "Step-4 ordinary extraction and conditional feature preservation must stay on GPU.");
            Assert.True(GpuSolidChunkCache.SupportsGpuSurfaceStep(8),
                "Feature-preserving block HLOD now counts and writes in the GPU page arena.");
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
                "Unsupported GPU extraction must remain an explicit failure; "
              + "publishing it as empty would create a visible hole.");
        }

        [Test]
        public void SceneIssue20260823014011920GpuLod2CarriesInnerTransitionFaceMask()
        {
            using var cache = new GpuSolidChunkCache(sourceStep: 2)
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
