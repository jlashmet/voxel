using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Runtime-only transport for the checked-in mountain-dragon sparse bake. The compact payload
    /// expands directly to the same BakedVoxelStructure that the authoring-time canonical MVX codec
    /// produced; no source mesh or voxelization code executes at runtime.
    /// </summary>
    public static class MountainDragonBakedArtifact
    {
        public const string ResourcePath = "VoxelShowcase/MountainDragonBake/mountain-dragon.mdvp.gz.b64";
        public const int ExpectedCellCount = 98100;
        public const int ExpectedCanonicalByteCount = 1073295;
        public const string ExpectedCanonicalSha256 = "83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41";
        public const string ExpectedTransportSha256 = "758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961";
        private const string Magic = "MDVP1";
        private const int BoundaryEdges = 1418;
        private const int NonManifoldEdges = 3660;

        public static BakedVoxelStructure Load()
        {
            TextAsset payload = Resources.Load<TextAsset>(ResourcePath);
            if (payload == null)
                throw new InvalidOperationException($"Mountain-dragon baked payload was not found at Resources/{ResourcePath}.");
            return DecodeBase64(payload.text);
        }

        public static BakedVoxelStructure DecodeBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                throw new FormatException("Mountain-dragon baked payload is empty.");
            byte[] compressed;
            try { compressed = Convert.FromBase64String(base64.Trim()); }
            catch (FormatException exception)
            {
                throw new FormatException("Mountain-dragon baked payload is not valid Base64.", exception);
            }
            if (!string.Equals(Sha256Hex(compressed), ExpectedTransportSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Mountain-dragon baked transport SHA-256 changed.");

            byte[] packed = DecompressBounded(compressed, 512 * 1024);
            using var stream = new MemoryStream(packed, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
            string magic = Encoding.ASCII.GetString(reader.ReadBytes(5));
            if (magic != Magic) throw new FormatException("Mountain-dragon baked payload header is invalid.");
            float voxelSize = reader.ReadSingle();
            int3 gridOrigin = new int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            int3 size = new int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            int sourceTriangles = checked((int)reader.ReadUInt32());
            int cellCount = checked((int)reader.ReadUInt32());
            if (cellCount != ExpectedCellCount)
                throw new FormatException($"Mountain-dragon baked cell count changed: {cellCount}.");
            if (sourceTriangles != MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount)
                throw new FormatException($"Mountain-dragon baked source triangle count changed: {sourceTriangles}.");
            if (math.any(size <= 0) || math.any(size > MountainDragonVoxelBakePolicy.MaximumStructureSize))
                throw new FormatException($"Mountain-dragon baked size is invalid: {size}.");

            var cells = new BakedVoxelCell[cellCount];
            long flat = 0;
            long plane = checked((long)size.y * size.z);
            long capacity = checked((long)size.x * size.y * size.z);
            for (int i = 0; i < cells.Length; i++)
            {
                uint delta = ReadVarUInt(reader);
                flat = i == 0 ? delta : checked(flat + delta);
                byte material = reader.ReadByte();
                if (material == 0 || flat < 0 || flat >= capacity)
                    throw new FormatException($"Mountain-dragon baked cell {i} is invalid.");
                int x = checked((int)(flat / plane));
                long remainder = flat - (long)x * plane;
                int y = checked((int)(remainder / size.z));
                int z = checked((int)(remainder - (long)y * size.z));
                cells[i] = new BakedVoxelCell(new int3(x, y, z), material);
            }
            if (stream.Position != stream.Length)
                throw new FormatException("Mountain-dragon baked payload has trailing data.");

            var bake = new BakedVoxelStructure(
                voxelSize, gridOrigin, size, cells, sourceTriangles, 0d,
                BoundaryEdges, NonManifoldEdges, interiorFilled: true);
            string canonical = BakedVoxelStructureCodec.Encode(bake);
            int canonicalBytes = Encoding.UTF8.GetByteCount(canonical);
            if (canonicalBytes != ExpectedCanonicalByteCount
                || !string.Equals(Sha256Hex(Encoding.UTF8.GetBytes(canonical)), ExpectedCanonicalSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Mountain-dragon baked artifact no longer matches the canonical MVX identity.");
            return bake;
        }

        private static uint ReadVarUInt(BinaryReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift <= 28; shift += 7)
            {
                byte current = reader.ReadByte();
                value |= (uint)(current & 0x7f) << shift;
                if ((current & 0x80) == 0) return value;
            }
            throw new FormatException("Mountain-dragon baked payload contains an oversized varint.");
        }

        private static byte[] DecompressBounded(byte[] compressed, int maxOutputBytes)
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                int read = gzip.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                if (output.Length + read > maxOutputBytes)
                    throw new InvalidOperationException("Mountain-dragon baked payload exceeds its decompression bound.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
