using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Converts an authoritative surface-brick coordinate into a non-border representative
    /// coordinate inside the same solid-render chunk.
    ///
    /// Initial surface discovery establishes which chunk owns authoritative content; it is not
    /// halo invalidation. The solid cache's generic brick admission also understands border
    /// dependencies, so feeding a discovered brick that lies exactly on a chunk boundary can
    /// create halo-only neighbour chunks. Those neighbours may have no resident owned core even
    /// though their extraction halo touches resident Storage. Canonicalising only the discovery
    /// feed preserves the owning chunk while preventing that accidental neighbour admission.
    /// Mutation invalidation and water discovery continue to consume the original coordinates.
    /// </summary>
    internal static class SurfaceDiscoveryChunkOwner
    {
        public static int3 Canonicalize(int3 worldBrick, int bricksPerChunkAxis)
        {
            int edge = math.max(1, bricksPerChunkAxis);
            int interior = edge / 2;
            int3 chunk = OwningChunk(worldBrick, edge);
            return chunk * edge + interior;
        }

        public static int3 OwningChunk(int3 worldBrick, int bricksPerChunkAxis)
        {
            int edge = math.max(1, bricksPerChunkAxis);
            return new int3(
                FloorDiv(worldBrick.x, edge),
                FloorDiv(worldBrick.y, edge),
                FloorDiv(worldBrick.z, edge));
        }

        /// <summary>
        /// Canonicalizes one discovery publication batch and partitions it by the exact renderer
        /// shard that owns each resulting chunk. Surface discovery is chunk admission, so multiple
        /// surface bricks that resolve to the same chunk are emitted only once. The scheduler can
        /// then call each shard only with unique work it can admit instead of making every shard
        /// rescan the batch or making the owning shard repeat the same dictionary/hash work for
        /// every surface brick in that chunk. Returns the number of unique chunk admissions routed
        /// across all shard buckets.
        /// </summary>
        public static int PartitionByOwningShard(
            IReadOnlyList<int3> worldBricks,
            int bricksPerChunkAxis,
            int shardCount,
            List<int3>[] shardBricks)
        {
            int count = math.max(1, shardCount);
            if (shardBricks == null || shardBricks.Length < count)
                throw new ArgumentException("Discovery shard buckets must cover every shard.",
                                            nameof(shardBricks));

            for (int shard = 0; shard < count; shard++)
            {
                if (shardBricks[shard] == null)
                    throw new ArgumentException("Discovery shard buckets must be initialized.",
                                                nameof(shardBricks));
                shardBricks[shard].Clear();
            }

            if (worldBricks == null) return 0;

            int edge = math.max(1, bricksPerChunkAxis);
            int interior = edge / 2;
            int routed = 0;
            for (int i = 0; i < worldBricks.Count; i++)
            {
                // Compute ownership once. Canonicalize() already derives this chunk, so doing an
                // OwningChunk(canonical) pass afterwards repeated three floor divisions for every
                // discovery record on the player thread.
                int3 chunk = OwningChunk(worldBricks[i], edge);
                int shard = CpuTransvoxelChunkCache.ShardForChunk(chunk, count);
                int3 canonical = chunk * edge + interior;
                List<int3> bucket = shardBricks[shard];

                // A publication batch is bounded and terrain usually contributes many bricks per
                // chunk, so the already-reused bucket is also the cheapest allocation-free dedup
                // set. This removes the much more expensive repeated cache admission (chunk math,
                // shard hash, clipmap/slot lookup and managed HashSet probes) downstream. In the
                // sparse worst case the bucket scan is small because records are spread by shard.
                if (bucket.Contains(canonical)) continue;
                bucket.Add(canonical);
                routed++;
            }
            return routed;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
