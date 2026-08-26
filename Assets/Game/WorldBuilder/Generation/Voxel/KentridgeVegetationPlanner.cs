using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;
using VoxelEngine.Structures.Api;
// Material identity is game-owned now; the old engine-side Mat constants were removed
// with the game-owned-materials refactor. Aliased so call sites read unchanged.
using Mat = Game.Materials.Api.GameMaterialIds;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Realization adapter for Kentridge's pure semantic vegetation layout.
    ///
    /// Normal runtime generation samples the already-generated voxel column through Storage.Api,
    /// so trees sit on the same district terraces, plot grading, and natural terrain the player
    /// sees. The analytic path exists for deterministic editor diagnostics without resident world
    /// storage.
    /// </summary>
    public static class KentridgeVegetationPlanner
    {
        private const float DecimetreMetres = 0.1f;
        private const int SearchMarginDm = 80;

        public static bool TryBuild(uint seed, VoxelWorldGenSettings settings,
                                    IVoxelSurfaceQuery surfaceQuery,
                                    out List<TreeInstance> instances)
        {
            // Editor-preview overload: no VoxelWorldGenSettings here, so this path stays
            // Kentridge-specific rather than inventing a settlement to preview.
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            List<VegetationCandidate> candidates =
                KentridgeVegetationLayoutPlanner.Build(plan);
            int scale = settings.VoxelsPerDecimetre;
            float voxelSize = DecimetreMetres / scale;
            instances = new List<TreeInstance>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                VegetationCandidate candidate = candidates[i];
                int worldX = candidate.X * scale;
                int worldZ = candidate.Z * scale;
                int natural = TerrainQuery.HeightAt(worldX, worldZ, seed);
                int authored = KentridgeVerticalProfile.SurfaceYAtDm(
                    candidate.X, candidate.Z, seed, scale);
                int maxY = math.max(natural, authored) + SearchMarginDm * scale;
                int minY = math.max(0, math.min(natural, authored) - SearchMarginDm * scale);
                if (!surfaceQuery.TryFindTopSolidExcluding(
                        worldX, worldZ, minY, maxY, Mat.Water, Mat.Cascade,
                        out int surface, out _))
                    continue;

                AddInstance(candidate, worldX, surface + 1, worldZ,
                            voxelSize, seed, instances);
            }

            return instances.Count > 0;
        }

        /// <summary>
        /// Deterministic editor-preview realization. Urban candidates use the authored Kentridge
        /// macro profile; perimeter candidates stay on natural terrain so the vegetation belt does
        /// not inherit the summit height merely because it lies north of town.
        /// </summary>
        public static List<TreeInstance> BuildAnalytic(uint seed, int voxelsPerDecimetre = 1)
        {
            // Editor-preview overload: no VoxelWorldGenSettings here, so this path stays
            // Kentridge-specific rather than inventing a settlement to preview.
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            List<VegetationCandidate> candidates =
                KentridgeVegetationLayoutPlanner.Build(plan);
            float voxelSize = DecimetreMetres / voxelsPerDecimetre;
            var instances = new List<TreeInstance>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                VegetationCandidate candidate = candidates[i];
                int worldX = candidate.X * voxelsPerDecimetre;
                int worldZ = candidate.Z * voxelsPerDecimetre;
                int surface = IsUrban(candidate)
                    ? KentridgeVerticalProfile.SurfaceYAtDm(
                        candidate.X, candidate.Z, seed, voxelsPerDecimetre)
                    : TerrainQuery.HeightAt(worldX, worldZ, seed);

                AddInstance(candidate, worldX, surface + 1, worldZ,
                            voxelSize, seed, instances);
            }

            return instances;
        }

        private static bool IsUrban(in VegetationCandidate candidate)
        {
            // These bounds follow the authored terrace envelope, not the capture boundary.
            return candidate.X >= 620 && candidate.X <= 1830
                && candidate.Z >= 40 && candidate.Z <= 1090;
        }

        private static void AddInstance(in VegetationCandidate candidate,
                                        int worldX, int worldY, int worldZ,
                                        float voxelSize, uint worldSeed,
                                        List<TreeInstance> instances)
        {
            TreeSpecies species = ToRuntimeSpecies(candidate.Species);
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(species);
            float desiredHeight = math.max(2.5f, candidate.HeightUnits * DecimetreMetres);
            float scale = desiredHeight / math.max(0.1f, profile.MidHeight);
            uint seed = math.hash(new int4(
                worldX, worldY, worldZ,
                candidate.Ordinal + (int)species * 131)) ^ worldSeed;
            if (seed == 0) seed = 1u;

            instances.Add(new TreeInstance
            {
                PositionMetres = new float3(worldX, worldY, worldZ) * voxelSize,
                Species = species,
                Seed = seed,
                Scale = scale,
            });
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
