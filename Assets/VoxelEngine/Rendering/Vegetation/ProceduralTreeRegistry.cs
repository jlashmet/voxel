using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation registry for semantic tree instances. World generation publishes an immutable
    /// deterministic identity snapshot; render geometry and LODs are derived from these identities.
    ///
    /// The legacy showcase additionally publishes the brick coordinates occupied by its old timber
    /// and foliage proxies. Those voxels remain authoritative for collision/destruction during
    /// migration, but must never be emitted underneath the procedural tree by either hard or smooth
    /// mesh derivation.
    /// </summary>
    public static class ProceduralTreeRegistry
    {
        public struct TreeDamageState
        {
            public float FoliageHealth;
            public bool Severed;
        }

        // The recovery Surface Nets cache and both CPU mesh caches use 16 bricks = 12.8 m per
        // render chunk. Keep the legacy proxy ownership in that same coordinate space so the
        // renderer can make the coarse->Transvoxel handoff exclusive on a chunk-by-chunk basis.
        private const int LegacyRenderChunkBrickShift = 4;

        private static readonly List<TreeInstance> s_Instances = new();
        private static readonly List<TreeDamageState> s_Damage = new();
        private static readonly List<HashSet<int>> s_RemovedBranches = new();
        private static readonly HashSet<int3> s_LegacyHiddenHardBricks = new();
        private static readonly HashSet<int3> s_LegacyHiddenSmoothBricks = new();
        private static readonly HashSet<int3> s_LegacyProxyRenderChunks = new();
        private static readonly HashSet<int3> s_CoarseLegacyProxyRenderChunks = new();
        private static int s_Version;
        private static int s_DamageVersion;

        public static IReadOnlyList<TreeInstance> Instances => s_Instances;
        public static IReadOnlyList<TreeDamageState> Damage => s_Damage;
        public static int Version => s_Version;
        public static int DamageVersion => s_DamageVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Instances.Clear();
            s_Damage.Clear();
            s_RemovedBranches.Clear();
            s_LegacyHiddenHardBricks.Clear();
            s_LegacyHiddenSmoothBricks.Clear();
            s_LegacyProxyRenderChunks.Clear();
            s_CoarseLegacyProxyRenderChunks.Clear();
            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
        }

        /// <summary>
        /// Atomically replaces the vegetation snapshot. The legacy brick sets are migration-only
        /// metadata: gameplay can still hit those old voxels, but they are presentation-invisible.
        /// Timber/scaffold bricks are suppressed from the hard mesher; foliage crown bricks are
        /// suppressed from the smooth field.
        /// </summary>
        public static void Replace(IReadOnlyList<TreeInstance> instances,
                                   IEnumerable<int3> legacyHiddenHardBricks = null,
                                   IEnumerable<int3> legacyHiddenSmoothBricks = null)
        {
            s_Instances.Clear();
            s_Damage.Clear();
            s_RemovedBranches.Clear();
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    s_Instances.Add(instances[i]);
                    s_Damage.Add(new TreeDamageState
                    {
                        FoliageHealth = 1f,
                        Severed = false,
                    });
                    s_RemovedBranches.Add(new HashSet<int>());
                }
            }

            s_LegacyProxyRenderChunks.Clear();
            s_CoarseLegacyProxyRenderChunks.Clear();

            s_LegacyHiddenHardBricks.Clear();
            if (legacyHiddenHardBricks != null)
            {
                foreach (int3 brick in legacyHiddenHardBricks)
                {
                    s_LegacyHiddenHardBricks.Add(brick);
                    s_LegacyProxyRenderChunks.Add(RenderChunkForBrick(brick));
                }
            }

            s_LegacyHiddenSmoothBricks.Clear();
            if (legacyHiddenSmoothBricks != null)
            {
                foreach (int3 brick in legacyHiddenSmoothBricks)
                {
                    s_LegacyHiddenSmoothBricks.Add(brick);
                    s_LegacyProxyRenderChunks.Add(RenderChunkForBrick(brick));
                }
            }

            // Until the render pass has observed the new registry snapshot, assume every proxy
            // chunk is still owned by coarse Surface Nets. This prevents the procedural renderer
            // from flashing a duplicate tree for one frame during startup/publication. The next
            // render-graph record replaces this conservative set with the chunks actually drawn.
            foreach (int3 chunk in s_LegacyProxyRenderChunks)
                s_CoarseLegacyProxyRenderChunks.Add(chunk);

            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
        }

        public static bool IsLegacyHiddenHardBrick(int3 worldBrick) =>
            s_LegacyHiddenHardBricks.Contains(worldBrick);

        public static bool IsLegacyHiddenSmoothBrick(int3 worldBrick) =>
            s_LegacyHiddenSmoothBricks.Contains(worldBrick);

        /// <summary>
        /// True when a 12.8 m recovery render chunk contains any part of the old voxel tree proxy.
        /// This is used only during migration so Surface Nets and the procedural tree never own the
        /// same visible tree at the same time.
        /// </summary>
        public static bool IsLegacyProxyRenderChunk(int3 renderChunk) =>
            s_LegacyProxyRenderChunks.Contains(renderChunk);

        /// <summary>
        /// The render pass publishes exactly which legacy-proxy chunks are still being shown by
        /// coarse Surface Nets this frame. Procedural tree roots overlapping those chunks stay
        /// hidden until the equivalent Transvoxel chunk is ready, preventing a double tree while
        /// preserving the warmup terrain fallback.
        /// </summary>
        public static bool IsCoarseLegacyProxyRenderChunk(int3 renderChunk) =>
            s_CoarseLegacyProxyRenderChunks.Contains(renderChunk);

        public static void ClearCoarseLegacyProxyRenderChunks() =>
            s_CoarseLegacyProxyRenderChunks.Clear();

        public static void MarkCoarseLegacyProxyRenderChunk(int3 renderChunk)
        {
            if (s_LegacyProxyRenderChunks.Contains(renderChunk))
                s_CoarseLegacyProxyRenderChunks.Add(renderChunk);
        }

        private static int3 RenderChunkForBrick(int3 worldBrick) =>
            new(worldBrick.x >> LegacyRenderChunkBrickShift,
                worldBrick.y >> LegacyRenderChunkBrickShift,
                worldBrick.z >> LegacyRenderChunkBrickShift);

        /// <summary>
        /// Returns the directly cut branches for one tree. Descendant removal is derived from the
        /// deterministic skeleton topology by the renderer, so the registry only has to replicate
        /// the small set of actual cut points rather than a render-specific expanded mask.
        /// </summary>
        public static IReadOnlyCollection<int> RemovedBranches(int treeIndex)
        {
            if ((uint)treeIndex >= (uint)s_RemovedBranches.Count)
                return System.Array.Empty<int>();
            return s_RemovedBranches[treeIndex];
        }

        /// <summary>
        /// Records an authoritative branch cut. Calls are idempotent because one blast can overlap
        /// several samples from the same connected limb.
        /// </summary>
        public static bool RemoveBranch(int treeIndex, int branchIndex)
        {
            if ((uint)treeIndex >= (uint)s_RemovedBranches.Count || branchIndex < 0)
                return false;
            if (!s_RemovedBranches[treeIndex].Add(branchIndex)) return false;
            unchecked { s_DamageVersion++; }
            return true;
        }

        /// <summary>
        /// Damage is monotonic inside one generated tree snapshot. The legacy voxel proxy and the
        /// semantic destruction event are both temporary migration inputs; whichever observes the
        /// stronger damage wins, and later polling can never heal leaves or reconnect a trunk.
        /// </summary>
        public static void SetDamage(int index, float foliageHealth, bool severed)
        {
            if ((uint)index >= (uint)s_Damage.Count) return;

            TreeDamageState previous = s_Damage[index];
            float nextFoliageHealth = math.min(previous.FoliageHealth, math.saturate(foliageHealth));
            bool nextSevered = previous.Severed || severed;
            if (previous.Severed == nextSevered
                && math.abs(previous.FoliageHealth - nextFoliageHealth) < 0.025f)
                return;

            s_Damage[index] = new TreeDamageState
            {
                FoliageHealth = nextFoliageHealth,
                Severed = nextSevered,
            };
            unchecked { s_DamageVersion++; }
        }
    }
}
