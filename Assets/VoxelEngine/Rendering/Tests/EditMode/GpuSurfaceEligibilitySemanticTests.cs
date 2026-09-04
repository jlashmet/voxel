using System;
using System.IO;
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

        [Test]
        public void UnsupportedDecorationIsRejectedBeforeExtraction()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unavailable.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");
            using var extractor = new GpuSurfaceExtractor(shader, cellsPerAxis: 8, padding: 2);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            var source = new UnsupportedCoatingCatalogue();
            CoatingCatalogueView coatings = CoatingCatalogueView.Capture(in source);

            NotSupportedException error = Assert.Throws<NotSupportedException>(
                () => extractor.SetCatalogues(in surfaces, in coatings, null));
            StringAssert.Contains("does not implement decoration", error.Message);
        }

        [Test]
        public void ProductionAdmissionBackpressureCannotReclassifyEligibleWorkAsCpu()
        {
            string root = RepoRoot();
            string context = File.ReadAllText(Path.Combine(root, "Assets", "VoxelEngine",
                "Rendering", "Runtime", "GpuVoxel", "GpuSurfaceExtractionContext.cs"));
            string cache = File.ReadAllText(Path.Combine(root, "Assets", "VoxelEngine",
                "Rendering", "Runtime", "SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string policy = File.ReadAllText(Path.Combine(root, "Assets", "VoxelEngine",
                "Rendering", "Runtime", "SurfaceExtraction", "GpuSurfaceProductionPolicy.cs"));

            string productionStage = MethodBody(context,
                "internal GpuStageOutcome TryBeginStage(NativeArray<TransvoxelDensityBrick>",
                "private bool TryCaptureStorageGeneration");
            StringAssert.Contains("_stageAdmissionPending = true", productionStage,
                "Eligible production work must remain in the GPU admission state machine.");
            StringAssert.Contains("return GpuStageOutcome.Staged", productionStage);
            StringAssert.DoesNotContain("return GpuStageOutcome.NoSlot", productionStage,
                "Temporary world/mirror/handle pressure must not route eligible work to CPU.");

            StringAssert.Contains("SupportsGpuSurfaceStep(SourceStep)", cache,
                "GPU cutover must be expressed in renderer capabilities, not named scenes.");
            StringAssert.DoesNotContain("VoxelShowcase", policy);
            StringAssert.DoesNotContain("Kentridge", policy);
            StringAssert.DoesNotContain("materialId ==", policy,
                "Production cutover policy must not special-case material identities.");
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                dir = dir.Parent;
            Assert.NotNull(dir, "Could not locate project root containing Assets/.");
            return dir.FullName;
        }

        private static string MethodBody(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing source marker: {startMarker}");
            Assert.Greater(end, start, $"Missing source marker after {startMarker}: {endMarker}");
            return source.Substring(start, end - start);
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

        private struct UnsupportedCoatingCatalogue : ICoatingPresentationCatalogue
        {
            public uint Version => 1;
            public ulong CatalogueHash => 0xDEC0A7EUL;
            public ulong ComputeHash() => CatalogueHash;

            public CoatingReadDefinition GetPresentation(byte coatingId) => new()
            {
                StableId = coatingId,
                AllowedMaterialMask = uint.MaxValue,
                DecorationShape = coatingId == 9
                    ? (SurfaceDecorationShape)99
                    : SurfaceDecorationShape.None,
            };
        }
    }
}
