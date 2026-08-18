using System.Collections.Generic;
using MountingForce.WorldGen;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Storage.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;
using VoxelEngine.Structures.Api;
// CastlePlan moved to the game layer: the game owns castle semantics, the engine only
// realizes the geometry it is handed.
using Game.Structures.Api;
// Material identity is game-owned now; the old engine-side Mat constants were removed
// with the game-owned-materials refactor. Aliased so call sites read unchanged.
using Mat = Game.Materials.Api.GameMaterialIds;

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

        private struct RegionCursor
        {
            public bool HasLookup;
            public bool Resident;
            public int3 RegionCoord;
            public RegionReadView View;
        }

        public static bool TryBuild(in CastlePlan plan,
                                    IRegionReadSource storage,
                                    uint worldSeed, out List<TreeInstance> instances)
        {
            if (storage == null)
            {
                instances = null;
                return false;
            }

            int top = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int3 gateProbe = CastleLayout.FrontGateMinimum(in plan)
                           + new int3(-plan.WallThickness, 0, 0);
            RegionCursor cursor = default;

            if (FindSurface(storage, ref cursor, gateProbe.x, gateProbe.z,
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
                CastleLayout.WaterfallLipZ(in plan),
                CastleLayout.LowerRiverZAt(in plan, streamX));

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
                    int surface = FindSurface(storage, ref cursor,
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

        private static int FindSurface(IRegionReadSource storage, ref RegionCursor cursor,
                                       int x, int z, int maxY, int minY)
        {
            for (int y = maxY; y >= minY; y--)
            {
                int3 voxel = new int3(x, y, z);
                int3 regionCoord = new int3(
                    FloorDiv(x, VoxelGrid.RegionVoxelEdge),
                    FloorDiv(y, VoxelGrid.RegionVoxelEdge),
                    FloorDiv(z, VoxelGrid.RegionVoxelEdge));
                if (!cursor.HasLookup || math.any(cursor.RegionCoord != regionCoord))
                {
                    cursor.HasLookup = true;
                    cursor.RegionCoord = regionCoord;
                    cursor.Resident = storage.TryAcquireRegion(regionCoord, out cursor.View);
                }
                if (!cursor.Resident) continue;

                int3 localVoxel = voxel - regionCoord * VoxelGrid.RegionVoxelEdge;
                if (!cursor.View.TryReadCell(localVoxel, out VoxelCell cell)) continue;
                byte material = cell.BaseMaterialId;
                if (material != Mat.Empty && material != Mat.Water && material != Mat.Cascade)
                    return y;
            }
            return int.MinValue;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
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
