using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VoxelEngine.MeshVoxelization.Editor;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Source-specific authoring pipeline for the pinned mountain-dragon OBJ. The source mesh is
    /// reconstructed and imported only in the Editor; the emitted sparse bake is the sole runtime
    /// gameplay artifact.
    /// </summary>
    public static class MountainDragonBakeGenerator
    {
        public const string DefaultArtifactPath = "Artifacts/SingleTest/mountain-dragon.mvx";
        public const string DefaultMetricsPath = "Artifacts/SingleTest/mountain-dragon-bake-metrics.txt";

        public readonly struct Result
        {
            public readonly BakedVoxelStructure Bake;
            public readonly BakedVoxelStructureStats Stats;
            public readonly string Encoded;
            public readonly int SerializedByteCount;

            public Result(BakedVoxelStructure bake, BakedVoxelStructureStats stats, string encoded)
            {
                Bake = bake ?? throw new ArgumentNullException(nameof(bake));
                Stats = stats;
                Encoded = encoded ?? throw new ArgumentNullException(nameof(encoded));
                SerializedByteCount = Encoding.UTF8.GetByteCount(encoded);
            }
        }

        public static Result GeneratePinnedBake()
        {
            MountainDragonSourceArchive.ReconstructImportedAsset();
            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                MountainDragonSourceArchive.GeneratedAssetPath);
            if (sourceRoot == null)
                throw new InvalidOperationException(
                    $"Unity did not import the reconstructed mountain-dragon OBJ at " +
                    $"{MountainDragonSourceArchive.GeneratedAssetPath}.");

            MeshVoxelizationSource source = UnityMeshVoxelizationAdapter.BuildSource(
                sourceRoot,
                MountainDragonPalettePolicy.DragonMaterial);
            if (source.Triangles.Length != MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount)
                throw new InvalidOperationException(
                    $"Mountain-dragon imported triangle count changed: expected " +
                    $"{MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount}, got {source.Triangles.Length}.");

            MeshVoxelizationSettings settings = MountainDragonAuthoringPolicy.CreateVoxelizationSettings();
            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);
            MountainDragonVoxelBakePolicy.ValidateBakeEnvelope(bake);
            BakedVoxelStructureStats stats = MeshVoxelizationMetrics.Analyze(bake);
            string encoded = BakedVoxelStructureCodec.Encode(bake);
            return new Result(bake, stats, encoded);
        }

        public static string FormatMetrics(in Result result)
        {
            BakedVoxelStructure bake = result.Bake;
            long denseCells = (long)bake.Size.x * bake.Size.y * bake.Size.z;
            var text = new StringBuilder(384);
            text.Append("sourceTriangles=").Append(bake.SourceTriangleCount).Append('\n');
            text.Append("voxelSize=").Append(bake.VoxelSize.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("gridOrigin=").Append(bake.GridOrigin.x).Append(',').Append(bake.GridOrigin.y).Append(',').Append(bake.GridOrigin.z).Append('\n');
            text.Append("size=").Append(bake.Size.x).Append(',').Append(bake.Size.y).Append(',').Append(bake.Size.z).Append('\n');
            text.Append("denseEnvelopeCells=").Append(denseCells).Append('\n');
            text.Append("authoredVoxelCount=").Append(result.Stats.CellCount).Append('\n');
            text.Append("surfaceVoxelCount=").Append(result.Stats.SurfaceCellCount).Append('\n');
            text.Append("connectedComponents=").Append(result.Stats.ConnectedComponentCount).Append('\n');
            text.Append("materialCount=").Append(result.Stats.MaterialCount).Append('\n');
            text.Append("sparseBrickCount=").Append(result.Stats.SparseBrickCount).Append('\n');
            text.Append("boundaryEdges=").Append(bake.BoundaryEdgeCount).Append('\n');
            text.Append("nonManifoldEdges=").Append(bake.NonManifoldEdgeCount).Append('\n');
            text.Append("interiorFilled=").Append(bake.InteriorFilled).Append('\n');
            text.Append("voxelizationMilliseconds=").Append(bake.VoxelizationMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("serializedBytes=").Append(result.SerializedByteCount).Append('\n');
            return text.ToString();
        }

        public static Result GeneratePinnedBakeAndWriteArtifact(string outputPath = null)
        {
            Result result = GeneratePinnedBake();
            string fullPath = ResolveOutputPath(
                string.IsNullOrWhiteSpace(outputPath) ? DefaultArtifactPath : outputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Mountain-dragon bake output directory could not be resolved.");

            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, result.Encoded, new UTF8Encoding(false));

            string metricsPath = string.IsNullOrWhiteSpace(outputPath)
                ? ResolveOutputPath(DefaultMetricsPath)
                : Path.ChangeExtension(fullPath, ".metrics.txt");
            string metricsDirectory = Path.GetDirectoryName(metricsPath);
            if (string.IsNullOrEmpty(metricsDirectory))
                throw new InvalidOperationException("Mountain-dragon metrics output directory could not be resolved.");
            Directory.CreateDirectory(metricsDirectory);
            File.WriteAllText(metricsPath, FormatMetrics(in result), new UTF8Encoding(false));
            return result;
        }

        private static string ResolveOutputPath(string relativeOrAbsolute) =>
            Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute);
    }
}
