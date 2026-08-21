using System.Collections.Generic;
using MountingForce.WorldGen;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Turns the slice's authored region themes into the trees, ground cover and wildlife that make
    /// each stretch of country read as a different place.
    ///
    /// The themes are decided in worldgen — farmland around the towns, pine forest between them,
    /// riverbank at the crossing — and this is the step that realizes them. Everything is placed by
    /// asking the theme map what a spot is, so moving the forest is a worldbuilding edit rather
    /// than a scattering edit, and the forest stays where the bridge crosses because both were
    /// derived from the same crossing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KentridgeRegionLife : MonoBehaviour
    {
        /// <summary>
        /// Metres between tree samples. Density is then applied per theme, so this only bounds the
        /// finest spacing a dense wood can reach.
        /// </summary>
        private const float TreeSampleStepMetres = 6f;

        /// <summary>Ground cover is sampled finer, because it has to read as cover rather than props.</summary>
        private const float UndergrowthSampleStepMetres = 1.4f;
        private const float HabitatSampleStepMetres = 20f;

        // Ceilings, enforced after placement. A theme tuned on gentle ground can produce several
        // times as much on broken ground, and the frame budget does not care why.
        private const int MaxTrees = 900;
        private const int MaxUndergrowth = 12000;
        private const int MaxClusters = 110;

        private readonly List<TreeInstance> _trees = new();
        private readonly List<VegetationSurfaceSample> _samples = new();
        private readonly List<VegetationInstance> _undergrowth = new();
        private readonly List<AmbientLifeHabitatSample> _habitats = new();
        private readonly List<AmbientLifeCluster> _clusters = new();

        private IVegetationBatchRenderer _vegetationRenderer;
        private IAmbientLifeBatchRenderer _lifeRenderer;

        public int TreeCount => _trees.Count;
        public int UndergrowthCount => _undergrowth.Count;
        public int ClusterCount => _clusters.Count;

        /// <summary>
        /// Fills the corridor between the two towns. <paramref name="halfWidthMetres"/> is how far
        /// either side of the road the country is dressed: beyond it the player is off the route
        /// and the far field carries the distance.
        /// </summary>
        public void Populate(
            ShowcaseWorld world,
            RegionThemeMap themes,
            float roadXMetres,
            float fromZMetres,
            float toZMetres,
            float halfWidthMetres)
        {
            if (world == null || themes == null) return;

            _vegetationRenderer ??=
                VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            _lifeRenderer ??=
                VegetationLifeRenderingComposition.EnsureAmbientLifeBatchRenderer(gameObject);

            BuildTrees(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres);
            BuildUndergrowth(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres);
            BuildWildlife(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres);

            Debug.Log($"Kentridge region life: {_trees.Count} trees, {_undergrowth.Count} "
                    + $"ground cover, {_clusters.Count} wildlife clusters.");
        }

        private void BuildTrees(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth)
        {
            _trees.Clear();

            for (float z = fromZ; z <= toZ; z += TreeSampleStepMetres)
            for (float x = roadX - halfWidth; x <= roadX + halfWidth; x += TreeSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);

                uint seed = Hash(world.Seed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm);

                // Trees per hectare against the area one sample represents. This is what makes the
                // pine forest dense and the farmland a scatter of copses from the same code.
                float sampleAreaHectares =
                    TreeSampleStepMetres * TreeSampleStepMetres / 10000f;
                float expected = profile.TreesPerHectare * sampleAreaHectares;
                if (Random01(seed) > expected) continue;

                // Keep the carriageway clear: a road you cannot walk down is not a road.
                if (Mathf.Abs(x - roadX) < 5f) continue;

                float3 jittered = new float3(
                    x + (Random01(seed ^ 0x51u) - 0.5f) * TreeSampleStepMetres,
                    0f,
                    z + (Random01(seed ^ 0x77u) - 0.5f) * TreeSampleStepMetres);

                if (!TryGround(world, jittered, out float3 grounded, out float3 normal)) continue;
                if (normal.y < 0.86f) continue;

                TreeSpeciesSlot slot = RegionThemeCatalog.SpeciesFor(
                    in profile, (int)(Random01(seed ^ 0x9Eu) * 1000f));
                if (slot == TreeSpeciesSlot.None) continue;

                _trees.Add(new TreeInstance
                {
                    PositionMetres = grounded,
                    Species = SpeciesFor(slot),
                    Seed = seed == 0u ? 1u : seed,
                    Scale = 0.85f + Random01(seed ^ 0xC3u) * 0.5f,
                });

                if (_trees.Count >= MaxTrees) break;
            }

            VegetationComposition.ReplaceTreeWorld(_trees);
        }

        private void BuildUndergrowth(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth)
        {
            _samples.Clear();

            // Ground cover is only worth sampling near the route: it is invisible past a few tens
            // of metres and it is the densest thing here.
            float coverHalfWidth = Mathf.Min(halfWidth, 45f);

            for (float z = fromZ; z <= toZ; z += UndergrowthSampleStepMetres)
            for (float x = roadX - coverHalfWidth; x <= roadX + coverHalfWidth;
                 x += UndergrowthSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(world.Seed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm ^ 0x1234u);

                if (Random01(seed) * 1000f > profile.UndergrowthPerMille) continue;
                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out float3 normal))
                    continue;

                _samples.Add(new VegetationSurfaceSample
                {
                    PositionMetres = grounded,
                    Normal = normal,
                    Surface = VegetationSurface.Ground,
                    Moisture = profile.Kind == RegionThemeKind.Riverbank ? 0.9f : 0.5f,
                    Shade = profile.Kind == RegionThemeKind.PineForest ? 0.8f : 0.3f,
                    ArcaneSaturation = 0f,
                });

                if (_samples.Count >= MaxUndergrowth) break;
            }

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(world.Seed);
            _undergrowth.Clear();
            VegetationPlacement.Generate(_samples, in settings, _undergrowth);
            _vegetationRenderer.SetInstances(_undergrowth);
        }

        private void BuildWildlife(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth)
        {
            _habitats.Clear();

            for (float z = fromZ; z <= toZ; z += HabitatSampleStepMetres)
            for (float x = roadX - halfWidth; x <= roadX + halfWidth; x += HabitatSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(world.Seed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm ^ 0x99u);

                if (Random01(seed) * 1000f > profile.WildlifePerMille) continue;
                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out float3 normal))
                    continue;

                _habitats.Add(new AmbientLifeHabitatSample
                {
                    PositionMetres = grounded + new float3(0f, 1.6f, 0f),
                    RadiusMetres = 0f,
                    Moisture = profile.Kind == RegionThemeKind.Riverbank ? 0.95f : 0.45f,
                    Shade = profile.Kind == RegionThemeKind.PineForest ? 0.85f : 0.3f,
                    FlowerDensity = profile.Kind == RegionThemeKind.TemperateFarmland ? 0.7f : 0.3f,
                    WaterPresence = profile.Kind == RegionThemeKind.Riverbank ? 0.9f : 0f,
                    FungusDensity = profile.Kind == RegionThemeKind.PineForest ? 0.6f : 0.15f,
                    DeadwoodDensity = profile.Kind == RegionThemeKind.PineForest ? 0.55f : 0.2f,
                    ArcaneSaturation = 0f,
                });

                if (_habitats.Count >= MaxClusters * 3) break;
            }

            AmbientLifePopulationSettings settings =
                AmbientLifePopulationSettings.Default(world.Seed);
            _clusters.Clear();
            AmbientLifePopulation.Generate(_habitats, in settings, _clusters);
            if (_clusters.Count > MaxClusters)
                _clusters.RemoveRange(MaxClusters, _clusters.Count - MaxClusters);
            _lifeRenderer.SetClusters(_clusters);
        }

        private static TreeSpecies SpeciesFor(TreeSpeciesSlot slot)
        {
            switch (slot)
            {
                case TreeSpeciesSlot.Pine: return TreeSpecies.Pine;
                case TreeSpeciesSlot.Birch: return TreeSpecies.Birch;
                case TreeSpeciesSlot.Maple: return TreeSpecies.Maple;
                case TreeSpeciesSlot.Willow: return TreeSpecies.Willow;
                case TreeSpeciesSlot.Dead: return TreeSpecies.Dead;
                default: return TreeSpecies.Oak;
            }
        }

        private static bool TryGround(
            ShowcaseWorld world, float3 position, out float3 grounded, out float3 normal)
        {
            grounded = default;
            normal = new float3(0f, 1f, 0f);

            int vx = (int)math.floor(position.x / ShowcaseWorld.VoxelSize);
            int vz = (int)math.floor(position.z / ShowcaseWorld.VoxelSize);

            // Nothing grows on the road, the bridge, or a rooftop.
            if (world.HasBuiltContentAbove(vx, vz)) return false;

            int height = world.SurfaceHeight(vx, vz);
            grounded = new float3(position.x, height * ShowcaseWorld.VoxelSize, position.z);

            const int Step = 6;
            float dx = world.SurfaceHeight(vx + Step, vz) - world.SurfaceHeight(vx - Step, vz);
            float dz = world.SurfaceHeight(vx, vz + Step) - world.SurfaceHeight(vx, vz - Step);
            normal = math.normalizesafe(new float3(-dx, 2f * Step, -dz), new float3(0f, 1f, 0f));
            return true;
        }

        private static uint Hash(uint seed, uint x, uint z)
        {
            uint h = seed ^ 0x9E3779B9u;
            h ^= x + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= z + 0xC2B2AE35u + (h << 6) + (h >> 2);
            h ^= h >> 15;
            h *= 0x2545F491u;
            h ^= h >> 13;
            return h == 0u ? 1u : h;
        }

        private static float Random01(uint seed)
        {
            uint h = Hash(seed, 0x27D4EB2Fu, 0x165667B1u);
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }
    }
}
