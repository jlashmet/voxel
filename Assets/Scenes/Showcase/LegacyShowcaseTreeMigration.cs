using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Structures;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Temporary bridge from the showcase's old voxel Tree/Pine calls to semantic procedural
    /// vegetation. It reproduces those placements deterministically, publishes real TreeInstance
    /// records, and masks only the old foliage-crown bricks out of the smooth terrain field.
    ///
    /// Delete this class once world generation emits TreeInstance directly.
    /// </summary>
    public sealed class LegacyShowcaseTreeMigration : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private bool _published;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("Legacy Showcase Tree Migration")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.AddComponent<LegacyShowcaseTreeMigration>();
        }

        private void Update()
        {
            if (_published || !VoxelRenderBridge.TryGetWorld(out VoxelWorldView view)) return;

            uint worldSeed = VoxelRenderBridge.TerrainSeed;
            int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = TerrainSampler.HeightAt(cx, cz, worldSeed);
            CastlePlan plan = CastleBuilder.Plan(new int3(cx, ground, cz), worldSeed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;

            // Wait until CastleBuilder has actually populated the presentation trees. The render
            // bridge can become valid earlier in scene startup on some script execution orders.
            int probeX = plan.Centre.x - 178;
            int probeZ = gateZ - 92;
            if (FindWoodRoot(ref view.Table, in view.Pool, probeX, probeZ) < 0) return;

            var instances = new List<TreeInstance>(48);
            var excludedBricks = new HashSet<int3>();
            int ordinal = 0;

            AddTreeBelt(in plan, top, worldSeed, instances, excludedBricks, ref ordinal);
            AddApproachTrees(in plan, gateZ, ref view.Table, in view.Pool,
                             worldSeed, instances, excludedBricks, ref ordinal);
            AddForegroundCopse(in plan, gateZ, ref view.Table, in view.Pool,
                               worldSeed, instances, excludedBricks, ref ordinal);
            AddWaterfallTrees(in plan, ref view.Table, in view.Pool,
                              worldSeed, instances, excludedBricks, ref ordinal);

            ProceduralTreeRegistry.Replace(instances, excludedBricks);
            _published = true;
            Destroy(gameObject);
        }

        private static void AddTreeBelt(in CastlePlan plan, int top, uint worldSeed,
                                        List<TreeInstance> instances, HashSet<int3> excluded,
                                        ref int ordinal)
        {
            var rng = new Random(plan.Seed ^ 0x7EE5u);
            int built = 0;

            // Keep this RNG sequence identical to CastleBuilder.TreeBelt so the new semantic trees
            // sit exactly where the old voxel crowns were authored.
            for (int attempt = 0; attempt < 96 && built < 22; attempt++)
            {
                float angle = rng.NextFloat(0f, math.PI * 2f);
                float radius = rng.NextFloat(plan.PlateauRadius * 0.74f,
                                             plan.PlateauRadius - 26f);
                int ox = (int)math.round(math.cos(angle) * radius);
                int oz = (int)math.round(math.sin(angle) * radius);

                bool outsideWalls = math.abs(ox) > plan.BaileyHalfX + plan.TowerRadius + 16
                                 || math.abs(oz) > plan.BaileyHalfZ + plan.TowerRadius + 16;
                bool blocksGate = oz < -plan.BaileyHalfZ && math.abs(ox) < 105;
                int waterfallOffsetX = CastleBuilder.WaterfallStreamX(in plan) - plan.Centre.x;
                int waterfallOffsetZ = CastleBuilder.WaterfallLipZ(in plan) - plan.Centre.z;
                bool nearWaterfall = math.abs(ox - waterfallOffsetX) < 125
                                  && math.abs(oz - waterfallOffsetZ) < 165;
                if (!outsideWalls || blocksGate || nearWaterfall) continue;

                int height = rng.NextInt(34, 58);
                int canopyRadius = rng.NextInt(12, 19);
                TreeSpecies species = BroadleafSpecies(built);
                AddLegacyBroadleaf(plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                                   height, canopyRadius, species, worldSeed,
                                   instances, excluded, ref ordinal);
                built++;
            }
        }

        private static void AddApproachTrees(in CastlePlan plan, int gateZ,
                                             ref RegionTable table, in BrickPool pool,
                                             uint worldSeed, List<TreeInstance> instances,
                                             HashSet<int3> excluded, ref int ordinal)
        {
            int2[] offsets =
            {
                new(-178, -92), new(168, -78), new(-235, -105), new(235, -110),
                new(-154, 42), new(184, 62),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = plan.Centre.x + offsets[i].x;
                int z = gateZ + offsets[i].y;
                int y = FindWoodRoot(ref table, in pool, x, z);
                if (y < 0) continue;

                if ((i & 1) == 0)
                {
                    int height = 58 + (i % 3) * 8;
                    int radius = 18 + (i & 1) * 3;
                    AddLegacyPine(x, y, z, height, radius, worldSeed,
                                  instances, excluded, ref ordinal);
                }
                else
                {
                    int height = 44 + (i % 3) * 6;
                    int radius = 15 + (i % 2) * 3;
                    AddLegacyBroadleaf(x, y, z, height, radius, BroadleafSpecies(i + 3),
                                       worldSeed, instances, excluded, ref ordinal);
                }
            }
        }

        private static void AddForegroundCopse(in CastlePlan plan, int gateZ,
                                               ref RegionTable table, in BrickPool pool,
                                               uint worldSeed, List<TreeInstance> instances,
                                               HashSet<int3> excluded, ref int ordinal)
        {
            int2[] offsets =
            {
                new(-260, -82), new(-282, -48), new(266, -62), new(292, -30),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = plan.Centre.x + offsets[i].x;
                int z = gateZ + offsets[i].y;
                int y = FindWoodRoot(ref table, in pool, x, z);
                if (y < 0) continue;
                AddLegacyPine(x, y, z, 44 + i * 5, 13 + (i & 1) * 3, worldSeed,
                              instances, excluded, ref ordinal);
            }
        }

        private static void AddWaterfallTrees(in CastlePlan plan,
                                              ref RegionTable table, in BrickPool pool,
                                              uint worldSeed, List<TreeInstance> instances,
                                              HashSet<int3> excluded, ref int ordinal)
        {
            int streamX = CastleBuilder.WaterfallStreamX(in plan);
            int riverZ = CastleBuilder.LowerRiverZAt(in plan, streamX);
            int poolX = streamX;
            int poolZ = riverZ + 27;
            int2[] offsets = { new(-88, 58), new(92, 72), new(-105, -28), new(108, -18) };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = poolX + offsets[i].x;
                int z = poolZ + offsets[i].y;
                int y = FindWoodRoot(ref table, in pool, x, z);
                if (y < 0) continue;

                if ((i & 1) == 0)
                    AddLegacyBroadleaf(x, y, z, 40 + i * 3, 15,
                                       i == 0 ? TreeSpecies.Sakura : TreeSpecies.Willow,
                                       worldSeed, instances, excluded, ref ordinal);
                else
                    AddLegacyPine(x, y, z, 45 + i * 3, 14, worldSeed,
                                  instances, excluded, ref ordinal);
            }
        }

        private static void AddLegacyBroadleaf(int x, int y, int z, int height,
                                               int canopyRadius, TreeSpecies species,
                                               uint worldSeed, List<TreeInstance> instances,
                                               HashSet<int3> excluded, ref int ordinal)
        {
            AddInstance(x, y, z, height, species, worldSeed, instances, ref ordinal);

            int lobeRadius = math.max(7, canopyRadius * 3 / 4);
            int crownRadius = canopyRadius / 2 + lobeRadius + 4;
            int crownMinY = y + height - canopyRadius / 2 - lobeRadius - 3;
            int crownMaxY = y + height - canopyRadius / 2 + lobeRadius + 9;
            AddExcludedBrickBounds(excluded,
                new int3(x - crownRadius, crownMinY, z - crownRadius),
                new int3(x + crownRadius, crownMaxY, z + crownRadius));
        }

        private static void AddLegacyPine(int x, int y, int z, int height, int radius,
                                          uint worldSeed, List<TreeInstance> instances,
                                          HashSet<int3> excluded, ref int ordinal)
        {
            AddInstance(x, y, z, height, TreeSpecies.Pine, worldSeed, instances, ref ordinal);
            AddExcludedBrickBounds(excluded,
                new int3(x - radius - 2, y + height / 4, z - radius - 2),
                new int3(x + radius + 2, y + height + 2, z + radius + 2));
        }

        private static void AddInstance(int x, int y, int z, int legacyHeightVoxels,
                                        TreeSpecies species, uint worldSeed,
                                        List<TreeInstance> instances, ref int ordinal)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(species);
            float desiredHeight = math.max(2.5f, legacyHeightVoxels * VoxelSize);
            float scale = desiredHeight / math.max(0.1f, profile.MidHeight);
            uint seed = math.hash(new int4(x, y, z, ordinal + (int)species * 131)) ^ worldSeed;
            if (seed == 0) seed = 1u;

            instances.Add(new TreeInstance
            {
                PositionMetres = new float3(x, y, z) * VoxelSize,
                Species = species,
                Seed = seed,
                Scale = scale,
            });
            ordinal++;
        }

        private static TreeSpecies BroadleafSpecies(int index)
        {
            switch (index & 7)
            {
                case 0: return TreeSpecies.Oak;
                case 1: return TreeSpecies.Sakura;
                case 2: return TreeSpecies.Birch;
                case 3: return TreeSpecies.Maple;
                case 4: return TreeSpecies.Willow;
                case 5: return TreeSpecies.Oak;
                case 6: return TreeSpecies.Sakura;
                default: return TreeSpecies.Dead;
            }
        }

        private static int FindWoodRoot(ref RegionTable table, in BrickPool pool, int x, int z)
        {
            for (int y = 0; y < ShowcaseWorld.RegionVoxelEdge; y++)
            {
                if (VoxelAccess.GetVoxel(ref table, in pool, new int3(x, y, z)) == ShowcaseWorld.MatWood)
                    return y;
            }
            return -1;
        }

        private static void AddExcludedBrickBounds(HashSet<int3> excluded,
                                                   int3 minVoxel, int3 maxVoxel)
        {
            int edge = VoxelDimensions.BrickEdge;
            int3 minBrick = new(FloorDiv(minVoxel.x, edge), FloorDiv(minVoxel.y, edge),
                                FloorDiv(minVoxel.z, edge));
            int3 maxBrick = new(FloorDiv(maxVoxel.x, edge), FloorDiv(maxVoxel.y, edge),
                                FloorDiv(maxVoxel.z, edge));

            for (int z = minBrick.z; z <= maxBrick.z; z++)
            for (int y = minBrick.y; y <= maxBrick.y; y++)
            for (int x = minBrick.x; x <= maxBrick.x; x++)
                excluded.Add(new int3(x, y, z));
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
