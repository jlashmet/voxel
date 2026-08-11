using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Migration-only destruction bridge between the showcase's old voxel trees and the semantic
    /// procedural tree skeleton.
    ///
    /// The old voxels remain authoritative collision/destruction targets for now. At publication
    /// time each visible procedural branch is bound to a few nearby legacy tree voxels. When a
    /// blast removes most of those supports, the semantic branch is cut; the renderer then removes
    /// that branch, all connected descendants, and their leaves from every LOD mesh.
    ///
    /// This is deliberately branch-local rather than another whole-tree health percentage. Delete
    /// it once tree generation writes a native semantic destruction graph alongside TreeInstance.
    /// </summary>
    public sealed class ProceduralTreeDamageBridge : MonoBehaviour
    {
        private sealed class BranchBinding
        {
            public int TreeIndex;
            public int BranchIndex;
            public int3 Support0;
            public int3 Support1;
            public int3 Support2;
            public byte SupportCount;
            public bool Cut;
        }

        private const float VoxelSize = 0.1f;
        private const double PollSeconds = 0.10;
        private const int SearchRadiusVoxels = 4;

        private readonly List<BranchBinding> _bindings = new(2048);
        private int _seenRegistryVersion = int.MinValue;
        private int _nextTreeToBind;
        private double _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("Procedural Tree Damage Bridge")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.AddComponent<ProceduralTreeDamageBridge>();
        }

        private void Update()
        {
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView view)) return;

            int version = ProceduralTreeRegistry.Version;
            if (_seenRegistryVersion != version)
            {
                _seenRegistryVersion = version;
                _bindings.Clear();
                _nextTreeToBind = 0;
                _nextPoll = Time.realtimeSinceStartupAsDouble + PollSeconds;
            }

            // Build one tree's bindings per frame. The nearest-proxy search touches sparse storage,
            // so spreading bootstrap work prevents semantic vegetation from reintroducing a large
            // render-thread hitch just to establish destruction compatibility.
            if (_nextTreeToBind < ProceduralTreeRegistry.Instances.Count)
            {
                BuildTreeBindings(_nextTreeToBind, ref view.Table, in view.Pool);
                _nextTreeToBind++;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextPoll) return;
            _nextPoll = now + PollSeconds;
            PollBindings(ref view.Table, in view.Pool);
        }

        private void BuildTreeBindings(int treeIndex, ref RegionTable table, in BrickPool pool)
        {
            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            if ((uint)treeIndex >= (uint)instances.Count) return;

            TreeInstance instance = instances[treeIndex];
            ProceduralTreeMeshBuilder.TreeSkeleton skeleton =
                ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);

            for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
            {
                ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[branchIndex];

                // Lower trunk connectivity already drives the whole-tree fall state through the
                // legacy migration. Bind side branches and upper-trunk pieces here; cutting a low
                // trunk tube out of the mesh would make the crown vanish before the fall starts.
                float midpointY = (branch.Start.y + branch.End.y) * 0.5f;
                if (branch.Level == 0 && midpointY < skeleton.Height * 0.48f) continue;

                var binding = new BranchBinding
                {
                    TreeIndex = treeIndex,
                    BranchIndex = branchIndex,
                };

                AddSupport(ref binding, instance.PositionMetres,
                           math.lerp(branch.Start, branch.End, 0.30f), ref table, in pool);
                AddSupport(ref binding, instance.PositionMetres,
                           math.lerp(branch.Start, branch.End, 0.62f), ref table, in pool);
                AddSupport(ref binding, instance.PositionMetres,
                           math.lerp(branch.Start, branch.End, 0.90f), ref table, in pool);

                if (binding.SupportCount > 0) _bindings.Add(binding);
            }
        }

        private static void AddSupport(ref BranchBinding binding, float3 rootMetres,
                                       float3 localMetres, ref RegionTable table,
                                       in BrickPool pool)
        {
            int3 centre = (int3)math.round((rootMetres + localMetres) / VoxelSize);
            if (!TryFindNearestProxy(ref table, in pool, centre, out int3 support)) return;

            if (binding.SupportCount > 0 && support.Equals(binding.Support0)) return;
            if (binding.SupportCount > 1 && support.Equals(binding.Support1)) return;

            switch (binding.SupportCount)
            {
                case 0: binding.Support0 = support; break;
                case 1: binding.Support1 = support; break;
                case 2: binding.Support2 = support; break;
                default: return;
            }
            binding.SupportCount++;
        }

        private static bool TryFindNearestProxy(ref RegionTable table, in BrickPool pool,
                                                int3 centre, out int3 result)
        {
            for (int radius = 0; radius <= SearchRadiusVoxels; radius++)
            {
                for (int z = -radius; z <= radius; z++)
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    // Visit only the newly-added cube shell. This bounds a four-voxel search at
                    // 9^3 reads instead of repeating every inner cube for every radius.
                    if (radius > 0 && math.max(math.abs(x), math.max(math.abs(y), math.abs(z))) != radius)
                        continue;

                    int3 p = centre + new int3(x, y, z);
                    byte material = VoxelAccess.GetVoxel(ref table, in pool, p);
                    if (!IsLegacyTreeMaterial(material)) continue;
                    result = p;
                    return true;
                }
            }

            result = default;
            return false;
        }

        private void PollBindings(ref RegionTable table, in BrickPool pool)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                BranchBinding binding = _bindings[i];
                if (binding.Cut || binding.SupportCount == 0) continue;

                int remaining = 0;
                if (binding.SupportCount > 0 && ProxyStillPresent(ref table, in pool, binding.Support0))
                    remaining++;
                if (binding.SupportCount > 1 && ProxyStillPresent(ref table, in pool, binding.Support1))
                    remaining++;
                if (binding.SupportCount > 2 && ProxyStillPresent(ref table, in pool, binding.Support2))
                    remaining++;

                // A single sample can disappear on the ragged edge of an explosion. Require the
                // majority of this branch's supports to be gone before changing semantic topology.
                int removed = binding.SupportCount - remaining;
                int required = binding.SupportCount / 2 + 1;
                if (removed < required) continue;

                binding.Cut = ProceduralTreeRegistry.RemoveBranch(
                    binding.TreeIndex, binding.BranchIndex);
            }
        }

        private static bool ProxyStillPresent(ref RegionTable table, in BrickPool pool, int3 p)
        {
            return IsLegacyTreeMaterial(VoxelAccess.GetVoxel(ref table, in pool, p));
        }

        private static bool IsLegacyTreeMaterial(byte material)
        {
            return material == ShowcaseWorld.MatWood
                || material == Mat.Grass
                || material == Mat.Moss;
        }
    }
}
