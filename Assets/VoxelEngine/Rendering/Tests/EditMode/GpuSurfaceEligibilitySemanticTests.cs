using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSurfaceEligibilitySemanticTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";

        [Test]
        public void UnsupportedReconstructionIsRejectedBeforeExtraction()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unavailable.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");
            using var extractor = new GpuSurfaceExtractor(shader, cellsPerAxis: 8, padding: 2);

            var source = new UnsupportedSurfaceCatalogue();
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.Capture(in source);
            CoatingCatalogueView coatings = default;

            NotSupportedException error = Assert.Throws<NotSupportedException>(
                () => extractor.SetCatalogues(in surfaces, in coatings, null));
            StringAssert.Contains("does not implement reconstruction", error.Message);
        }

        private struct UnsupportedSurfaceCatalogue : ISurfacePresentationCatalogue
        {
            public uint Version => 1;
            public ulong CatalogueHash => 0xBAD5EEDUL;
            public ulong ComputeHash() => CatalogueHash;

            public SurfaceStyleReadDefinition GetPresentation(ushort styleId) => new()
            {
                StableId = styleId,
                Reconstruction = styleId == 9
                    ? (SurfaceReconstruction)99
                    : SurfaceReconstruction.Smooth,
                Curvature = 255,
                JoinGroup = 1,
            };

            public SurfaceJoinReadRule GetPresentationJoin(byte groupA, byte groupB) =>
                SurfaceJoinReadRule.SharpSeam;
        }
    }
}
