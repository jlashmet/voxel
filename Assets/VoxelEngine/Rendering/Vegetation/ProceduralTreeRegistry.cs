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
        /// Updates presentation damage without regenerating the deterministic tree identity.
        /// Small foliage-health noise is ignored so polling the voxel proxy does not churn the
        /// renderer when a coarse sample count toggles by one point.
        /// </summary>
        public static void SetDamage(int index, float foliageHealth, bool severed)
        {
            if ((uint)index >= (uint)s_Damage.Count) return;

            foliageHealth = math.saturate(foliageHealth);
            TreeDamageState previous = s_Damage[index];
            if (previous.Severed == severed
                && math.abs(previous.FoliageHealth - foliageHealth) < 0.025f)
                return;

            s_Damage[index] = new TreeDamageState
            {
                FoliageHealth = foliageHealth,
                Severed = severed,
            };
            unchecked { s_DamageVersion++; }
        }
    }
}
