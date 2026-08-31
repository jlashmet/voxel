using System;
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

        public static Result GeneratePinnedBakeAndWriteArtifact(string outputPath = null)
        {
            Result result = GeneratePinnedBake();
            string relativeOrAbsolute = string.IsNullOrWhiteSpace(outputPath)
                ? DefaultArtifactPath
                : outputPath;
            string fullPath = Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Mountain-dragon bake output directory could not be resolved.");

            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, result.Encoded, new UTF8Encoding(false));
            return result;
        }
    }
}
