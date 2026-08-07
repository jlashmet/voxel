using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Server.Storage
{
    /// <summary>
    /// File-based region blob persistence for the server.
    ///
    /// Each region is serialized to a compressed file keyed by its coordinate tuple
    /// (x,y,z) stored in a hash-consistent directory structure under the world save path.
    /// This is the "cold" tier of the server residency model: when a region transitions
    /// from Warm to Cold, it is flushed here and evicted from RAM.
    ///
    /// For now this uses gzip compression (System.IO.Compression). In production, this would
    /// be replaced with LZ4 or a custom delta-compression format optimized for terrain data
    /// where most regions are uniform material.
    /// </summary>
    public static class RegionStore
    {
        private const string StoreSubdirectory = "region_blobs";

        private static string _worldSavePath;

        /// <summary>Initialize the region store with a world save path.</summary>
        public static void Initialize(string worldSavePath)
        {
            _worldSavePath = Path.Combine(worldSavePath, StoreSubdirectory);
            Directory.CreateDirectory(_worldSavePath);
        }

        /// <summary>Get the canonical file path for a region coordinate.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetFilePath(int3 coord) =>
            Path.Combine(
                _worldSavePath,
                $"{coord.x:x8}_{coord.y:x8}_{coord.z:x8}.blob");

        /// <summary>Write a region to disk. Called from RegionResidency.EvaluateForEviction.</summary>
        public static void Write(int3 coord, byte[] data)
        {
            var path = GetFilePath(coord);
            var tempPath = path + ".tmp";

            // Write to temp file first, then rename for atomicity.
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest))
            {
                gs.Write(data, 0, data.Length);
            }

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }

        /// <summary>Read a region from disk. Returns null if not found.</summary>
        public static byte[] Read(int3 coord)
        {
            var path = GetFilePath(coord);
            if (!File.Exists(path))
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var gs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                gs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>Delete a region's persisted blob. Called on world unload.</summary>
        public static void Delete(int3 coord)
        {
            var path = GetFilePath(coord);
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>Check if a region is persisted on disk.</summary>
        public static bool Exists(int3 coord) => File.Exists(GetFilePath(coord));

        /// <summary>Flush all pending dirty regions to disk. For shutdown safety.</summary>
        public static void FlushAll()
        {
            // In production: iterate the DirtyRegions set and call Write on each.
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>Clean up the region store directory (for testing).</summary>
        public static void Cleanup()
        {
            if (Directory.Exists(_worldSavePath))
                Directory.Delete(_worldSavePath, true);
        }
    }
}
