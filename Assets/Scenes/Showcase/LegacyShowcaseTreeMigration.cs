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
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Temporary bridge from the showcase's old voxel Tree/Pine calls to semantic procedural
    /// vegetation. It reproduces those placements deterministically and publishes real
    /// TreeInstance records.
    ///
    /// The legacy timber/foliage voxels remain the gameplay collision/destruction proxy for this
    /// migration milestone. Their timber bricks are explicitly hidden from the hard renderer and
    /// the procedural tree polls a sparse subset of those voxels for foliage loss/trunk severing.
    /// Delete this class once world generation emits semantic trees and their destruction graph
    /// directly.
    /// </summary>
    public sealed class LegacyShowcaseTreeMigration : MonoBehaviour
    {
        private sealed class LegacyTreeProxy
        {
            public int3 Root;
            public int HeightVoxels;
            public int RadiusVoxels;
            public bool Pine;
            public readonly List<int3> FoliageProbes = new(128);
        }

        private const float VoxelSize = 0.1f;
        private const double DamagePollSeconds = 0.125;
        private const int MaxFoliageProbesPerTree = 160;

        private readonly List<LegacyTreeProxy> _proxies = new();
        private bool _published;
        private double _nextDamagePoll;

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
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView view)) return;

            if (!_published)
            {
                TryPublish(ref view);
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextDamagePoll) return;
            _nextDamagePoll = now + DamagePollSeconds;
            PollDamage(ref view);
        }

        private void TryPublish(ref VoxelWorldView view)
        {
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
            var hiddenHardBricks = new HashSet<int3>();
            _proxies.Clear();
            int ordinal = 0;

            AddTreeBelt(in plan, top, worldSeed, instances, hiddenHardBricks, ref ordinal);
            AddApproachTrees(in plan, gateZ, ref view.Table, in view.Pool,
                             worldSeed, instances, hiddenHardBricks, ref ordinal);
            AddForegroundCopse(in plan, gateZ, ref view.Table, in view.Pool,
                               worldSeed, instances, hiddenHardBricks, ref ordinal);
            AddWaterfallTrees(in plan, ref view.Table, in view.Pool,
                              worldSeed, instances, hiddenHardBricks, ref ordinal);

            CaptureFoliageProbes(ref view.Table, in view.Pool);
            ProceduralTreeRegistry.Replace(instances, hiddenHardBricks);
            _published = true;
            _nextDamagePoll = Time.realtimeSinceStartupAsDouble + DamagePollSeconds;
        }

        private void AddTreeBelt(in CastlePlan plan, int top, uint worldSeed,
                                 List<TreeInstance> instances, HashSet<int3> hiddenHardBricks,
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
                AddInstance(plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                            height, canopyRadius, false, species, worldSeed,
                            instances, hiddenHardBricks, ref ordinal);
                built++;
            }
        }

        private void AddApproachTrees(in CastlePlan plan, int gateZ,
                                      ref RegionTable table, in BrickPool pool,
                                      uint worldSeed, List<TreeInstance> instances,
                                      HashSet<int3> hiddenHardBricks, ref int ordinal)
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
                    AddInstance(x, y, z, height, 18, true, TreeSpecies.Pine, worldSeed,
                                instances, hiddenHardBricks, ref ordinal);
                }
                else
                {
                    int height = 44 + (i % 3) * 6;
                    int radius = 15 + (i % 2) * 3;
                    AddInstance(x, y, z, height, radius, false, BroadleafSpecies(i + 3),
                                worldSeed, instances, hiddenHardBricks, ref ordinal);
                }
            }
        }

        private void AddForegroundCopse(in CastlePlan plan, int gateZ,
                                        ref RegionTable table, in BrickPool pool,
                                        uint worldSeed, List<TreeInstance> instances,
                                        HashSet<int3> hiddenHardBricks, ref int ordinal)
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
                int radius = 13 + (i & 1) * 3;
                AddInstance(x, y, z, 44 + i * 5, radius, true, TreeSpecies.Pine, worldSeed,
                            instances, hiddenHardBricks, ref ordinal);
            }
        }

        private void AddWaterfallTrees(in CastlePlan plan,
                                       ref RegionTable table, in BrickPool pool,
                                       uint worldSeed, List<TreeInstance> instances,
                                       HashSet<int3> hiddenHardBricks, ref int ordinal)
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
                    AddInstance(x, y, z, 40 + i * 3, 15, false,
                                i == 0 ? TreeSpecies.Sakura : TreeSpecies.Willow,
                                worldSeed, instances, hiddenHardBricks, ref ordinal);
                else
                    AddInstance(x, y, z, 45 + i * 3, 14, true, TreeSpecies.Pine,
                                worldSeed, instances, hiddenHardBricks, ref ordinal);
            }
        }

        private void AddInstance(int x, int y, int z, int legacyHeightVoxels,
                                 int legacyRadiusVoxels, bool pine,
                                 TreeSpecies species, uint worldSeed,
                                 List<TreeInstance> instances, HashSet<int3> hiddenHardBricks,
                                 ref int ordinal)
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

            var proxy = new LegacyTreeProxy
            {
                Root = new int3(x, y, z),
                HeightVoxels = legacyHeightVoxels,
                RadiusVoxels = legacyRadiusVoxels,
                Pine = pine,
            };
            _proxies.Add(proxy);
            AddHiddenHardBricks(proxy, hiddenHardBricks);
            ordinal++;
        }

        private static void AddHiddenHardBricks(LegacyTreeProxy proxy, HashSet<int3> hidden)
        {
            int trunkRadius = math.max(3, proxy.RadiusVoxels / 5) + 2;
            AddBrickBox(proxy.Root + new int3(-trunkRadius, 0, -trunkRadius),
                        proxy.Root + new int3(trunkRadius, proxy.HeightVoxels, trunkRadius), hidden);

            if (proxy.Pine) return;

            // The old broadleaf proxy authored two horizontal scaffold limbs at roughly 2/3
            // height. Hide those timber bricks too if the castle migration classifier happened
            // to tag them as architectural wood.
            int branchY = proxy.Root.y + proxy.HeightVoxels * 2 / 3;
            int branchLength = math.max(8, proxy.RadiusVoxels - 3);
            AddBrickBox(new int3(proxy.Root.x - branchLength, branchY - 2, proxy.Root.z - 4),
                        new int3(proxy.Root.x + branchLength, branchY + 10, proxy.Root.z + 4), hidden);
            AddBrickBox(new int3(proxy.Root.x - 4, branchY - 2, proxy.Root.z - branchLength),
                        new int3(proxy.Root.x + 4, branchY + 15, proxy.Root.z + branchLength), hidden);
        }

        private static void AddBrickBox(int3 minVoxel, int3 maxVoxel, HashSet<int3> hidden)
        {
            int3 minBrick = minVoxel >> VoxelDimensions.BrickEdgeLog2;
            int3 maxBrick = maxVoxel >> VoxelDimensions.BrickEdgeLog2;
            for (int bz = minBrick.z; bz <= maxBrick.z; bz++)
            for (int by = minBrick.y; by <= maxBrick.y; by++)
            for (int bx = minBrick.x; bx <= maxBrick.x; bx++)
                hidden.Add(new int3(bx, by, bz));
        }

        private void CaptureFoliageProbes(ref RegionTable table, in BrickPool pool)
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                LegacyTreeProxy proxy = _proxies[i];
                proxy.FoliageProbes.Clear();

                int crownStart = proxy.Pine
                    ? proxy.Root.y + proxy.HeightVoxels / 4
                    : proxy.Root.y + math.max(proxy.HeightVoxels / 2,
                                              proxy.HeightVoxels - proxy.RadiusVoxels * 2);
                int crownTop = proxy.Root.y + proxy.HeightVoxels
                             + (proxy.Pine ? 0 : proxy.RadiusVoxels / 2);
                int radius = proxy.RadiusVoxels + 2;

                for (int y = crownStart; y <= crownTop && proxy.FoliageProbes.Count < MaxFoliageProbesPerTree; y += 4)
                for (int z = -radius; z <= radius && proxy.FoliageProbes.Count < MaxFoliageProbesPerTree; z += 4)
                for (int x = -radius; x <= radius && proxy.FoliageProbes.Count < MaxFoliageProbesPerTree; x += 4)
                {
                    if (x * x + z * z > radius * radius) continue;
                    int3 p = new(proxy.Root.x + x, y, proxy.Root.z + z);
                    byte material = VoxelAccess.GetVoxel(ref table, in pool, p);
                    if (material == Mat.Grass || material == Mat.Moss)
                        proxy.FoliageProbes.Add(p);
                }
            }
        }

        private void PollDamage(ref VoxelWorldView view)
        {
            int count = math.min(_proxies.Count, ProceduralTreeRegistry.Instances.Count);
            for (int i = 0; i < count; i++)
            {
                LegacyTreeProxy proxy = _proxies[i];
                int3 region = proxy.Root >> VoxelDimensions.RegionVoxelEdgeLog2;
                if (!view.Table.IsResident(region)) continue;

                bool severed = IsTrunkSevered(ref view.Table, in view.Pool, proxy);
                float foliageHealth = FoliageHealth(ref view.Table, in view.Pool, proxy);
                ProceduralTreeRegistry.SetDamage(i, foliageHealth, severed);
            }
        }

        private static bool IsTrunkSevered(ref RegionTable table, in BrickPool pool,
                                           LegacyTreeProxy proxy)
        {
            int usableHeight = proxy.HeightVoxels - (proxy.Pine ? 8 : 0);
            int scanHeight = math.min(math.max(12, usableHeight / 2), usableHeight - 2);
            if (scanHeight <= 4) return false;

            int consecutiveWeakBands = 0;
            int radial = math.max(1, math.min(2, proxy.RadiusVoxels / 6));
            int2[] offsets =
            {
                new(0, 0), new(radial, 0), new(-radial, 0),
                new(0, radial), new(0, -radial),
            };

            for (int dy = 2; dy <= scanHeight; dy += 2)
            {
                int wood = 0;
                for (int o = 0; o < offsets.Length; o++)
                {
                    int3 p = proxy.Root + new int3(offsets[o].x, dy, offsets[o].y);
                    if (VoxelAccess.GetVoxel(ref table, in pool, p) == ShowcaseWorld.MatWood)
                        wood++;
                }

                consecutiveWeakBands = wood <= 1 ? consecutiveWeakBands + 1 : 0;
                if (consecutiveWeakBands >= 2) return true;
            }

            return false;
        }

        private static float FoliageHealth(ref RegionTable table, in BrickPool pool,
                                           LegacyTreeProxy proxy)
        {
            if (proxy.FoliageProbes.Count == 0) return 1f;

            int remaining = 0;
            for (int i = 0; i < proxy.FoliageProbes.Count; i++)
            {
                byte material = VoxelAccess.GetVoxel(ref table, in pool, proxy.FoliageProbes[i]);
                if (material == Mat.Grass || material == Mat.Moss)
                    remaining++;
            }
            return remaining / (float)proxy.FoliageProbes.Count;
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
    }
}
