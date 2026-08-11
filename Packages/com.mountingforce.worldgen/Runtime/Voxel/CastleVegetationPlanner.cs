using System.Collections.Generic;
using MountingForce.WorldGen;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Structures;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel realization adapter for the pure castle vegetation grammar. Core worldgen chooses
    /// semantic candidates; this layer only waits for resident geometry, samples surface heights,
    /// and translates candidates into runtime TreeInstance identities.
    /// </summary>
    public static class CastleVegetationPlanner
    {
        private const float VoxelSize = 0.1f;

        public static bool TryBuild(in CastlePlan plan,
                                    ref RegionTable table, in BrickPool pool,
                                    uint worldSeed, out List<TreeInstance> instances)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int streamX = CastleBuilder.WaterfallStreamX(in plan);
            int3 gateProbe = CastleBuilder.FrontGateMinimum(in plan)
                           + new int3(-plan.WallThickness, 0, 0);

            if (FindSurface(ref table, in pool, gateProbe.x, gateProbe.z,
                            top + plan.WallHeight + 16, top - 24) == int.MinValue)
            {
                instances = null;
                return false;
            }

            var context = new CastleVegetationContext(
                plan.Seed,
                plan.Centre.x, plan.Centre.z,
                top, gateZ,
                plan.PlateauRadius,
                plan.BaileyHalfX, plan.BaileyHalfZ,
                plan.TowerRadius,
                streamX,
                CastleBuilder.WaterfallLipZ(in plan),
                CastleBuilder.LowerRiverZAt(in plan, streamX));

            List<VegetationCandidate> candidates = CastleVegetationLayoutPlanner.Build(in context);
            instances = new List<TreeInstance>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                VegetationCandidate candidate = candidates[i];
                int rootY;
                if (candidate.HeightMode == VegetationHeightMode.Fixed)
                {
                    rootY = candidate.FixedRootY;
                }
                else
                {
                    int surface = FindSurface(ref table, in pool,
                                              candidate.X, candidate.Z,
                                              candidate.SurfaceMaxY, candidate.SurfaceMinY);
                    if (surface == int.MinValue) continue;
                    rootY = surface + 1;
                }

                AddInstance(candidate, rootY, worldSeed, instances);
            }
            return true;
        }

        private static void AddInstance(in VegetationCandidate candidate, int y,
                                        uint worldSeed, List<TreeInstance> instances)
        {
            TreeSpecies species = ToRuntimeSpecies(candidate.Species);
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(species);
            float desiredHeight = math.max(2.5f, candidate.HeightUnits * VoxelSize);
            float scale = desiredHeight / math.max(0.1f, profile.MidHeight);
            uint seed = math.hash(new int4(
                candidate.X, y, candidate.Z,
                candidate.Ordinal + (int)species * 131)) ^ worldSeed;
            if (seed == 0) seed = 1u;

            instances.Add(new TreeInstance
            {
                PositionMetres = new float3(candidate.X, y, candidate.Z) * VoxelSize,
                Species = species,
                Seed = seed,
                Scale = scale,
            });
        }

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

        private static TreeSpecies ToRuntimeSpecies(SemanticTreeSpecies species) => species switch
        {
            SemanticTreeSpecies.Oak => TreeSpecies.Oak,
            SemanticTreeSpecies.Pine => TreeSpecies.Pine,
            SemanticTreeSpecies.Birch => TreeSpecies.Birch,
            SemanticTreeSpecies.Maple => TreeSpecies.Maple,
            SemanticTreeSpecies.Sakura => TreeSpecies.Sakura,
            SemanticTreeSpecies.Willow => TreeSpecies.Willow,
            _ => TreeSpecies.Dead,
        };
    }
}
