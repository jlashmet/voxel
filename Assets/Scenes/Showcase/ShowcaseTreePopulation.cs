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
    /// Publishes the Showcase's semantic vegetation directly from the deterministic castle plan.
    /// Trees are not represented by voxel trunks/crowns and there is no legacy proxy or migration
    /// ownership. Terrain remains voxel-authoritative; tree identity/geometry is semantic.
    /// </summary>
    [DefaultExecutionOrder(350)]
    public sealed class ShowcaseTreePopulation : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private bool _done;

        public static bool Completed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Completed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("Showcase Tree Population")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.AddComponent<ShowcaseTreePopulation>();
        }

        private void Update()
        {
            if (_done) return;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView view)) return;

            uint worldSeed = VoxelRenderBridge.TerrainSeed;
            int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = TerrainSampler.HeightAt(cx, cz, worldSeed);
            CastlePlan plan = CastleBuilder.Plan(new int3(cx, ground, cz), worldSeed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;

            // The world bridge can exist before the origin castle region has completed generation.
            // Use an authored masonry probe rather than any tree material as the readiness gate.
            int3 gateProbe = CastleBuilder.FrontGateMinimum(in plan) +
                             new int3(-plan.WallThickness, 0, 0);
            if (!HasGround(ref view.Table, in view.Pool, gateProbe.x, gateProbe.z,
                           top + plan.WallHeight + 16, top - 24))
                return;

            var instances = new List<TreeInstance>(40);
            int ordinal = 0;

            AddTreeBelt(in plan, top, worldSeed, instances, ref ordinal);
            AddApproachTrees(in plan, gateZ, top, ref view.Table, in view.Pool,
                             worldSeed, instances, ref ordinal);
            AddForegroundCopse(in plan, gateZ, top, ref view.Table, in view.Pool,
                               worldSeed, instances, ref ordinal);
            AddWaterfallTrees(in plan, top, ref view.Table, in view.Pool,
                              worldSeed, instances, ref ordinal);

            ProceduralTreeRegistry.Replace(instances);
            _done = true;
            Completed = true;
            enabled = false;
            Debug.Log($"Procedural vegetation: published {instances.Count} semantic Showcase trees.");
        }

        private static void AddTreeBelt(in CastlePlan plan, int top, uint worldSeed,
                                        List<TreeInstance> instances, ref int ordinal)
        {
            var rng = new Random(plan.Seed ^ 0x7EE5u);
            int built = 0;
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
                TreeSpecies species = BroadleafSpecies(built);
                AddInstance(plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                            height, species, worldSeed, instances, ref ordinal);
                built++;
            }
        }

        private static void AddApproachTrees(in CastlePlan plan, int gateZ, int top,
                                             ref RegionTable table, in BrickPool pool,
                                             uint worldSeed, List<TreeInstance> instances,
                                             ref int ordinal)
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
                int surface = FindSurface(ref table, in pool, x, z, top + 20, top - 170);
                if (surface == int.MinValue) continue;

                if ((i & 1) == 0)
                    AddInstance(x, surface + 1, z, 58 + (i % 3) * 8, TreeSpecies.Pine,
                                worldSeed, instances, ref ordinal);
                else
                    AddInstance(x, surface + 1, z, 44 + (i % 3) * 6,
                                BroadleafSpecies(i + 3), worldSeed, instances, ref ordinal);
            }
        }

        private static void AddForegroundCopse(in CastlePlan plan, int gateZ, int top,
                                               ref RegionTable table, in BrickPool pool,
                                               uint worldSeed, List<TreeInstance> instances,
                                               ref int ordinal)
        {
            int2[] offsets =
            {
                new(-260, -82), new(-282, -48), new(266, -62), new(292, -30),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = plan.Centre.x + offsets[i].x;
                int z = gateZ + offsets[i].y;
                int surface = FindSurface(ref table, in pool, x, z, top + 18, top - 120);
                if (surface == int.MinValue) continue;
                AddInstance(x, surface + 1, z, 44 + i * 5, TreeSpecies.Pine,
                            worldSeed, instances, ref ordinal);
            }
        }

        private static void AddWaterfallTrees(in CastlePlan plan, int top,
                                              ref RegionTable table, in BrickPool pool,
                                              uint worldSeed, List<TreeInstance> instances,
                                              ref int ordinal)
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
                int surface = FindSurface(ref table, in pool, x, z, top + 24, top - 180);
                if (surface == int.MinValue) continue;

                TreeSpecies species = (i & 1) == 0
                    ? (i == 0 ? TreeSpecies.Sakura : TreeSpecies.Willow)
                    : TreeSpecies.Pine;
                AddInstance(x, surface + 1, z, (i & 1) == 0 ? 40 + i * 3 : 45 + i * 3,
                            species, worldSeed, instances, ref ordinal);
            }
        }

        private static void AddInstance(int x, int y, int z, int heightVoxels,
                                        TreeSpecies species, uint worldSeed,
                                        List<TreeInstance> instances, ref int ordinal)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(species);
            float desiredHeight = math.max(2.5f, heightVoxels * VoxelSize);
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

        private static bool HasGround(ref RegionTable table, in BrickPool pool,
                                      int x, int z, int maxY, int minY) =>
            FindSurface(ref table, in pool, x, z, maxY, minY) != int.MinValue;

        private static int FindSurface(ref RegionTable table, in BrickPool pool,
                                       int x, int z, int maxY, int minY)
        {
            for (int y = maxY; y >= minY; y--)
            {
                byte material = VoxelAccess.GetVoxel(ref table, in pool, new int3(x, y, z));
                if (material != Mat.Empty && material != Mat.Water && material != Mat.Cascade)
                    return y;
            }
            return int.MinValue;
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
    }
}
