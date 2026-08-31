using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Source-specific authoring adapter for the checked-in mountain-dragon transfer archive.
    /// The archive is transport-only: ordinary runtime never reads these files or source triangles.
    /// </summary>
    public static class MountainDragonSourceArchive
    {
        public const string ExpectedObjSha256 =
            "f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1";
        public const string ExpectedGzipSha256 =
            "fd2f8253fcf5bc32b275640448511f59d20dcc7d01c307f99124b224431892d4";
        public const int ExpectedObjByteCount = 860349;
        public const string GeneratedAssetPath =
            "Assets/Generated/MeshVoxelization/mountain_dragon_clean.obj";

        private const string IssueSourceDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/source";
        private const string PartPattern = "mountain_dragon_clean.obj.gz.b64.part*";
        private const int MaxCompressedBytes = 2 * 1024 * 1024;
        private const int MaxObjBytes = 2 * 1024 * 1024;

        public static byte[] ReconstructObjBytes(string repositoryRoot = null)
        {
            string root = string.IsNullOrWhiteSpace(repositoryRoot)
                ? Directory.GetCurrentDirectory()
                : repositoryRoot;
            string sourceDirectory = Path.Combine(root, IssueSourceDirectory);
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException(
                    $"Mountain-dragon source directory was not found: {sourceDirectory}");

            string[] parts = Directory.GetFiles(sourceDirectory, PartPattern, SearchOption.TopDirectoryOnly);
            if (parts.Length == 0)
                throw new InvalidOperationException("Mountain-dragon source archive contains no transfer parts.");

            Array.Sort(parts, StringComparer.Ordinal);
            var base64 = new StringBuilder(parts.Length * 20_000);
            for (int i = 0; i < parts.Length; i++)
            {
                string expectedSuffix = $"part{i:00}";
                if (!parts[i].EndsWith(expectedSuffix, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Mountain-dragon source archive is not contiguous; expected {expectedSuffix} at index {i}.");

                string part = File.ReadAllText(parts[i], Encoding.ASCII).Trim();
                if (part.Length == 0)
                    throw new InvalidOperationException($"Mountain-dragon source archive part is empty: {parts[i]}");
                base64.Append(part);
            }

            byte[] compressed;
            try
            {
                compressed = Convert.FromBase64String(base64.ToString());
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "Mountain-dragon source archive base64 is malformed or incomplete.", exception);
            }

            if (compressed.Length > MaxCompressedBytes)
                throw new InvalidOperationException(
                    $"Mountain-dragon source archive exceeds the {MaxCompressedBytes}-byte compressed safety bound.");

            string compressedHash = Sha256Hex(compressed);
            if (!string.Equals(compressedHash, ExpectedGzipSha256, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Mountain-dragon source archive is incomplete or changed. " +
                    $"Expected gzip SHA-256 {ExpectedGzipSha256}, got {compressedHash} from {parts.Length} parts.");

            byte[] obj = DecompressBounded(compressed, MaxObjBytes);
            if (obj.Length != ExpectedObjByteCount)
                throw new InvalidOperationException(
                    $"Mountain-dragon OBJ byte count changed. Expected {ExpectedObjByteCount}, got {obj.Length}.");

            string objHash = Sha256Hex(obj);
            if (!string.Equals(objHash, ExpectedObjSha256, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Mountain-dragon OBJ SHA-256 changed. Expected {ExpectedObjSha256}, got {objHash}.");

            return obj;
        }

        [MenuItem("Tools/Voxel/Mesh Voxelization/Reconstruct Mountain Dragon Source")]
        public static void ReconstructImportedAsset()
        {
            byte[] obj = ReconstructObjBytes();
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), GeneratedAssetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Generated mountain-dragon asset directory could not be resolved.");

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(fullPath, obj);
            AssetDatabase.ImportAsset(
                GeneratedAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log(
                $"Reconstructed mountain-dragon OBJ at {GeneratedAssetPath} " +
                $"({ExpectedObjByteCount} bytes, SHA-256 {ExpectedObjSha256}).");
        }

        private static byte[] DecompressBounded(byte[] compressed, int maxOutputBytes)
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
            using var output = new MemoryStream(Math.Min(ExpectedObjByteCount, maxOutputBytes));
            var buffer = new byte[16 * 1024];
            int total = 0;
            while (true)
            {
                int read = gzip.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                total = checked(total + read);
                if (total > maxOutputBytes)
                    throw new InvalidOperationException(
                        $"Mountain-dragon OBJ exceeds the {maxOutputBytes}-byte decompression safety bound.");
                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
