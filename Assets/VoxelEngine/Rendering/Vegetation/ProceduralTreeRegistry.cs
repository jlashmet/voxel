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
    /// proxy. Those voxels remain authoritative for collision/destruction during migration, but
    /// must never be emitted by the hard-surface renderer underneath the procedural tree.
    /// </summary>
    public static class ProceduralTreeRegistry
    {
        public struct TreeDamageState
        {
            public float FoliageHealth;
            public bool Severed;
        }

        private static readonly List<TreeInstance> s_Instances = new();
        private static readonly List<TreeDamageState> s_Damage = new();
        private static readonly List<HashSet<int>> s_RemovedBranches = new();
        private static readonly HashSet<int3> s_LegacyHiddenHardBricks = new();
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
            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
        }

        /// <summary>
        /// Atomically replaces the vegetation snapshot. <paramref name="legacyHiddenHardBricks"/>
        /// is migration-only metadata: gameplay can still hit those old timber voxels, but the
        /// exact castle mesher must not draw them as architecture.
        /// </summary>
        public static void Replace(IReadOnlyList<TreeInstance> instances,
                                   IEnumerable<int3> legacyHiddenHardBricks = null)
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

            s_LegacyHiddenHardBricks.Clear();
            if (legacyHiddenHardBricks != null)
            {
                foreach (int3 brick in legacyHiddenHardBricks)
                    s_LegacyHiddenHardBricks.Add(brick);
            }

            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
        }

        public static bool IsLegacyHiddenHardBrick(int3 worldBrick) =>
            s_LegacyHiddenHardBricks.Contains(worldBrick);

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
