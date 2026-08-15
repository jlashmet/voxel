using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

using VoxelEngine.Collision.Api;

namespace VoxelEngine.Collision.Runtime
{
    /// <summary>Result of an authoritative voxel raycast.</summary>
    public struct HitInfo
    {
        /// <summary>Logical read-block coordinate where the ray first encountered solid content.</summary>
        public int3 Position;

        /// <summary>Face normal at the entry point into the hit block.</summary>
        public float3 Normal;

        /// <summary>True when a solid block was found. No Storage allocator/pool identity is exposed.</summary>
        public bool IsHit;

        /// <summary>Distance from ray origin to the hit block, in the query's world units.</summary>
        public float Distance { get; set; }
    }

    /// <summary>
    /// Authoritative raycast over the shared deterministic DDA traversal. Storage lookup occurs
    /// only when traversal enters a new resident region; block checks inside that region are
    /// direct reads from the borrowed native <see cref="RegionReadView"/>.
    /// </summary>
    public static class VoxelRaycast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Raycast(
            IRegionReadSource source,
            float3 origin,
            float3 direction,
            out HitInfo hit)
        {
            hit = default;

            if (math.lengthsq(direction) < 1e-6f)
                return false;

            int3 startBlock = new int3(
                (int)math.floor(origin.x),
                (int)math.floor(origin.y),
                (int)math.floor(origin.z));

            float3 normalisedDir = math.normalize(direction);
            const float maxDistance = 10000f;

            int3 endBlock = new int3(
                (int)math.round(origin.x + normalisedDir.x * maxDistance),
                (int)math.round(origin.y + normalisedDir.y * maxDistance),
                (int)math.round(origin.z + normalisedDir.z * maxDistance));

            var cursor = DdaTraversal.Cursor.Between(startBlock, endBlock);
            RegionReadView region = default;

            while (cursor.MoveNext())
            {
                int3 current = cursor.Current;
                if (!region.IsCreated || !region.ContainsWorldBlock(current))
                {
                    if (!source.TryAcquireRegionContainingBlock(current, out region))
                    {
                        region = default;
                        continue;
                    }
                }

                if (!region.TryGetWorldBlock(current, out VoxelReadBlock block))
                    continue;

                bool solid = block.Kind == VoxelReadBlockKind.Uniform
                    ? block.UniformMaterial != VoxelGrid.MaterialEmpty
                    : block.Kind == VoxelReadBlockKind.Mixed
                      && region.IsWorldBlockOccupied(current);
                if (!solid) continue;

                hit.Position = current;
                hit.Normal = new float3(
                    cursor.EntryNormal.x, cursor.EntryNormal.y, cursor.EntryNormal.z);
                hit.IsHit = true;
                hit.Distance = math.length((float3)(current - startBlock));
                return true;
            }

            return false;
        }
    }
}
