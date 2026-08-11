using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Structures;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// One-shot cleanup for the showcase's pre-semantic voxel trees.
    ///
    /// CastleBuilder still authors the old Tree/Pine volumes before the migration layer publishes
    /// procedural TreeInstances. Keeping those voxels as wood/grass/moss made them a second visible
    /// tree through whichever recovery mesher happened to own the chunk. Once the semantic snapshot
    /// exists, rewrite that old volume to unsupported moss. Occupancy remains solid, so the current
    /// DDA/explosion path still has a cheap destruction proxy, while both smooth extractors reject
    /// the unsupported volume as non-terrain and the existing legacy-hard mask suppresses any stale
    /// hard ownership on the old timber bricks.
    ///
    /// This class is intentionally temporary. The final tree system should collide against the
    /// semantic branch graph directly and CastleBuilder should stop authoring voxel trees entirely.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class LegacyTreeProxyCleanup : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;

        // The largest legacy crown is under 2 m radius and the tallest showcase tree is under
        // 8 m. A little padding catches the broadleaf scaffold limbs and overlapping crown lobes
        // without reaching the castle walls or the protected gate approach.
        private const int HorizontalRadiusVoxels = 30;
        private const int HeightVoxels = 100;

        private bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("Legacy Tree Proxy Cleanup")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.AddComponent<LegacyTreeProxyCleanup>();
        }

        private void Update()
        {
            if (_done) return;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView view)) return;

            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            if (instances == null || instances.Count == 0) return;

            var dirtyRegions = new HashSet<int3>();
            int changedVoxels = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                int3 root = (int3)math.round(instances[i].PositionMetres / VoxelSize);
                changedVoxels += RewriteProxy(ref view.Table, ref view.Pool, root, dirtyRegions);
            }

            if (dirtyRegions.Count > 0 && VoxelRenderBridge.RegionsNeedingUpload != null)
            {
                foreach (int3 region in dirtyRegions)
                {
                    VoxelRenderBridge.RegionsNeedingUpload.Add(region);
                    // Density/surface halos can sample across a region edge. Conservatively wake
                    // the four horizontal neighbours; non-resident entries are harmless.
                    VoxelRenderBridge.RegionsNeedingUpload.Add(region + new int3(1, 0, 0));
                    VoxelRenderBridge.RegionsNeedingUpload.Add(region + new int3(-1, 0, 0));
                    VoxelRenderBridge.RegionsNeedingUpload.Add(region + new int3(0, 0, 1));
                    VoxelRenderBridge.RegionsNeedingUpload.Add(region + new int3(0, 0, -1));
                }
            }

            // Direct explosion->branch damage is now authoritative for procedural presentation.
            // The old polling bridge expects a wood trunk and would interpret this hidden proxy
            // rewrite as an already-severed tree, so retire it once its placement work is done.
            LegacyShowcaseTreeMigration migration = FindObjectOfType<LegacyShowcaseTreeMigration>();
            if (migration != null) migration.enabled = false;

            _done = true;
            Debug.Log($"Procedural vegetation: hid legacy voxel tree proxies ({changedVoxels:N0} voxels across {dirtyRegions.Count} regions).");
        }

        private static int RewriteProxy(ref RegionTable table, ref BrickPool pool, int3 root,
                                        HashSet<int3> dirtyRegions)
        {
            int3 minVoxel = new(root.x - HorizontalRadiusVoxels,
                                root.y,
                                root.z - HorizontalRadiusVoxels);
            int3 maxVoxel = new(root.x + HorizontalRadiusVoxels,
                                root.y + HeightVoxels,
                                root.z + HorizontalRadiusVoxels);

            int3 minBrick = minVoxel >> VoxelDimensions.BrickEdgeLog2;
            int3 maxBrick = maxVoxel >> VoxelDimensions.BrickEdgeLog2;
            int changed = 0;

            for (int bz = minBrick.z; bz <= maxBrick.z; bz++)
            for (int by = minBrick.y; by <= maxBrick.y; by++)
            for (int bx = minBrick.x; bx <= maxBrick.x; bx++)
            {
                int3 worldBrick = new(bx, by, bz);
                int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                if (!table.TryGetRegion(regionCoord, out Region region)) continue;

                int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
                int brickIndex = Region.BrickIndex(localBrick.x, localBrick.y, localBrick.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsEmpty) continue;

                int3 brickOrigin = worldBrick * VoxelDimensions.BrickEdge;
                bool brickChanged = false;

                if (brick.IsUniform)
                {
                    byte material = brick.UniformMaterial;
                    if (!IsLegacyTreeMaterial(material)) continue;

                    // A uniform matching brick inside this tightly-scoped tree volume is old tree
                    // mass. Converting the whole reference avoids allocating a mixed brick merely
                    // to change one non-empty collision material into another.
                    region.BrickRefs[brickIndex] = BrickRef.Uniform(Mat.Moss);
                    brickChanged = true;
                    changed += VoxelDimensions.VoxelsPerBrick;
                }
                else
                {
                    int offset = pool.VoxelOffset(brick.PoolIndex);
                    for (int vz = 0; vz < VoxelDimensions.BrickEdge; vz++)
                    for (int vy = 0; vy < VoxelDimensions.BrickEdge; vy++)
                    for (int vx = 0; vx < VoxelDimensions.BrickEdge; vx++)
                    {
                        int3 worldVoxel = brickOrigin + new int3(vx, vy, vz);
                        if (worldVoxel.y < minVoxel.y || worldVoxel.y > maxVoxel.y
                            || worldVoxel.x < minVoxel.x || worldVoxel.x > maxVoxel.x
                            || worldVoxel.z < minVoxel.z || worldVoxel.z > maxVoxel.z)
                            continue;

                        int dx = worldVoxel.x - root.x;
                        int dz = worldVoxel.z - root.z;
                        if (dx * dx + dz * dz
                            > HorizontalRadiusVoxels * HorizontalRadiusVoxels)
                            continue;

                        int voxelIndex = vx | (vy << 3) | (vz << 6);
                        byte material = pool.Voxels[offset + voxelIndex];
                        if (!IsLegacyTreeMaterial(material)) continue;

                        pool.Voxels[offset + voxelIndex] = Mat.Moss;
                        changed++;
                        brickChanged = true;
                    }

                    if (brickChanged) pool.MarkDirty(brick.PoolIndex);
                }

                if (!brickChanged) continue;
                region.Dirty = true;
                table.CommitRegion(region);
                dirtyRegions.Add(regionCoord);
            }

            return changed;
        }

        private static bool IsLegacyTreeMaterial(byte material) =>
            material == Mat.Wood || material == Mat.Grass || material == Mat.Moss;
    }
}
