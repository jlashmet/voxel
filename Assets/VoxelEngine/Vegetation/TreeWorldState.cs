using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Core.Vegetation
{
    public readonly struct TreeBranchCutEvent
    {
        public readonly int TreeIndex;
        public readonly int BranchIndex;
        public readonly float3 HitPointMetres;
        public readonly float3 Impulse;

        public TreeBranchCutEvent(int treeIndex, int branchIndex,
                                  float3 hitPointMetres, float3 impulse)
        {
            TreeIndex = treeIndex;
            BranchIndex = branchIndex;
            HitPointMetres = hitPointMetres;
            Impulse = impulse;
        }
    }

    public readonly struct TreeDamageChangedEvent
    {
        public readonly int TreeIndex;
        public readonly float FoliageHealth;
        public readonly bool Severed;

        public TreeDamageChangedEvent(int treeIndex, float foliageHealth, bool severed)
        {
            TreeIndex = treeIndex;
            FoliageHealth = foliageHealth;
            Severed = severed;
        }
    }

    public readonly struct TreeSeveredEvent
    {
        public readonly int TreeIndex;
        public readonly int BreakBranchIndex;
        public readonly float3 HitPointMetres;
        public readonly float3 Impulse;

        public TreeSeveredEvent(int treeIndex, int breakBranchIndex,
                                float3 hitPointMetres, float3 impulse)
        {
            TreeIndex = treeIndex;
            BreakBranchIndex = breakBranchIndex;
            HitPointMetres = hitPointMetres;
            Impulse = impulse;
        }
    }

    /// <summary>
    /// Authoritative semantic state for procedural vegetation. World generation publishes immutable
    /// tree identities here; gameplay mutates branch/damage state here. Rendering is a subscriber,
    /// never an owner of gameplay state.
    /// </summary>
    public static class TreeWorldState
    {
        public struct TreeDamageState
        {
            public float FoliageHealth;
            public bool Severed;
        }

        private const float RootQuantizationMetres = 0.1f;

        private static readonly List<TreeInstance> s_Instances = new();
        private static readonly List<TreeDamageState> s_Damage = new();
        private static readonly List<HashSet<int>> s_RemovedBranches = new();
        private static int s_Version;
        private static int s_DamageVersion;

        public static IReadOnlyList<TreeInstance> Instances => s_Instances;
        public static IReadOnlyList<TreeDamageState> Damage => s_Damage;
        public static int Version => s_Version;
        public static int DamageVersion => s_DamageVersion;

        public static event Action SnapshotChanged;
        public static event Action<TreeBranchCutEvent> BranchCut;
        public static event Action<TreeDamageChangedEvent> DamageChanged;
        public static event Action<TreeSeveredEvent> TreeSevered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Instances.Clear();
            s_Damage.Clear();
            s_RemovedBranches.Clear();
            SnapshotChanged = null;
            BranchCut = null;
            DamageChanged = null;
            TreeSevered = null;
            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
        }

        public static void Replace(IReadOnlyList<TreeInstance> instances)
        {
            s_Instances.Clear();
            s_Damage.Clear();
            s_RemovedBranches.Clear();

            int duplicateRoots = 0;
            var authoredRoots = new HashSet<int3>();
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    TreeInstance instance = instances[i];
                    int3 root = (int3)math.round(instance.PositionMetres / RootQuantizationMetres);
                    if (!authoredRoots.Add(root))
                    {
                        duplicateRoots++;
                        continue;
                    }

                    s_Instances.Add(instance);
                    s_Damage.Add(new TreeDamageState
                    {
                        FoliageHealth = 1f,
                        Severed = false,
                    });
                    s_RemovedBranches.Add(new HashSet<int>());
                }
            }

            if (duplicateRoots > 0)
                Debug.LogWarning($"Procedural vegetation: discarded {duplicateRoots} duplicate tree roots before publication.");

            unchecked
            {
                s_Version++;
                s_DamageVersion++;
            }
            SnapshotChanged?.Invoke();
        }

        public static IReadOnlyCollection<int> RemovedBranches(int treeIndex)
        {
            if ((uint)treeIndex >= (uint)s_RemovedBranches.Count)
                return Array.Empty<int>();
            return s_RemovedBranches[treeIndex];
        }

        public static bool RemoveBranch(int treeIndex, int branchIndex,
                                        float3 hitPointMetres = default,
                                        float3 impulse = default)
        {
            if ((uint)treeIndex >= (uint)s_RemovedBranches.Count || branchIndex < 0)
                return false;
            if (!s_RemovedBranches[treeIndex].Add(branchIndex)) return false;

            unchecked { s_DamageVersion++; }
            BranchCut?.Invoke(new TreeBranchCutEvent(treeIndex, branchIndex,
                                                     hitPointMetres, impulse));
            return true;
        }

        public static void SetDamage(int index, float foliageHealth, bool severed,
                                     float3 hitPointMetres = default,
                                     float3 impulse = default,
                                     int breakBranchIndex = -1)
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

            DamageChanged?.Invoke(new TreeDamageChangedEvent(index,
                                                              nextFoliageHealth,
                                                              nextSevered));
            if (!previous.Severed && nextSevered)
                TreeSevered?.Invoke(new TreeSeveredEvent(
                    index, breakBranchIndex, hitPointMetres, impulse));
        }
    }
}
