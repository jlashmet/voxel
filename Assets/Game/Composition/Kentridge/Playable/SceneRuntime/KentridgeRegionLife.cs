using System;
using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Realizes the WorldBuilder region/ecology policy through the shared vegetation and ambient
    /// life systems. Placement stays derived from generated terrain; no scene-local scatter is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KentridgeRegionLife : MonoBehaviour
    {
        private const float TreeSampleStepMetres = 6f;
        private const float HabitatSampleStepMetres = 20f;
        private const int MaxTrees = 900;
        private const int MaxUndergrowth = 12000;
        private const int MaxClusters = 110;

        private readonly List<TreeInstance> _trees = new();
        private readonly List<VegetationSurfaceSample> _samples = new();
        private readonly List<VegetationInstance> _undergrowth = new();
        private readonly List<RegionEcologyGridCell> _eligibleMeadowCells = new();
        private readonly List<RegionEcologyGridCell> _grassMeadowCells = new();
        private readonly Dictionary<long, RegionEcologyGridCell> _meadowCellByPosition = new();
        private readonly List<AmbientLifeHabitatSample> _habitats = new();
        private readonly List<AmbientLifeCluster> _clusters = new();

        private IVegetationBatchRenderer _vegetationRenderer;
        private IAmbientLifeBatchRenderer _lifeRenderer;

        public int TreeCount => _trees.Count;
        public int UndergrowthCount => _undergrowth.Count;
        public int ClusterCount => _clusters.Count;
        public int GrassCount { get; private set; }
        public int PrimaryMeadowGrassCount { get; private set; }

        public void Populate(
            ShowcaseWorld world,
            RegionThemeMap themes,
            float roadXMetres,
            float fromZMetres,
            float toZMetres,
            float halfWidthMetres,
            RegionEcologyPolicy ecology = null)
        {
            if (world == null || themes == null) return;
            ecology ??= KentridgeDefinition.CountrysideEcology;

            _vegetationRenderer ??=
                VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            _lifeRenderer ??=
                VegetationLifeRenderingComposition.EnsureAmbientLifeBatchRenderer(gameObject);

            BuildTrees(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres, ecology);
            BuildUndergrowth(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres, ecology);
            BuildWildlife(world, themes, roadXMetres, fromZMetres, toZMetres, halfWidthMetres, ecology);

            Debug.Log($"Kentridge region life: {_trees.Count} trees, {_undergrowth.Count} ground cover, "
                    + $"{_clusters.Count} wildlife clusters; grass instances total={GrassCount}, "
                    + $"primary-contiguous-meadow-instances={PrimaryMeadowGrassCount}.");
        }

        private void BuildTrees(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth,
            RegionEcologyPolicy ecology)
        {
            _trees.Clear();
            if (ecology.TreeKinds.Count == 0)
            {
                VegetationComposition.ReplaceTreeWorld(_trees);
                return;
            }

            uint ecologySeed = ecology.DeriveSeed(world.Seed);
            for (float z = fromZ; z <= toZ && _trees.Count < MaxTrees; z += TreeSampleStepMetres)
            for (float x = roadX - halfWidth;
                 x <= roadX + halfWidth && _trees.Count < MaxTrees;
                 x += TreeSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(ecologySeed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm);
                float sampleAreaHectares = TreeSampleStepMetres * TreeSampleStepMetres / 10000f;
                float expected = profile.TreesPerHectare * sampleAreaHectares;
                if (Random01(seed) > expected) continue;
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
                TreeSpecies species = SpeciesFor(slot);
                if (!ecology.AllowsTree(species.ToString())) continue;

                _trees.Add(new TreeInstance
                {
                    PositionMetres = grounded,
                    Species = species,
                    Seed = seed == 0u ? 1u : seed,
                    Scale = 0.85f + Random01(seed ^ 0xC3u) * 0.5f,
                });
            }
            VegetationComposition.ReplaceTreeWorld(_trees);
        }

        private void BuildUndergrowth(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth,
            RegionEcologyPolicy ecology)
        {
            _samples.Clear();
            _eligibleMeadowCells.Clear();
            _grassMeadowCells.Clear();
            _meadowCellByPosition.Clear();

            float coverHalfWidth = Mathf.Min(halfWidth, 45f);
            float sampleStep = ecology.VegetationSampleSpacingMetres;
            float routeClearance = ecology.RouteClearanceMetres;
            int xCellCount = Mathf.FloorToInt((coverHalfWidth * 2f) / sampleStep) + 1;
            int zCellCount = Mathf.FloorToInt(Mathf.Max(0f, toZ - fromZ) / sampleStep) + 1;

            for (int zCell = 0; zCell < zCellCount && _samples.Count < MaxUndergrowth; zCell++)
            for (int xCell = 0; xCell < xCellCount && _samples.Count < MaxUndergrowth; xCell++)
            {
                float z = fromZ + zCell * sampleStep;
                float x = roadX - coverHalfWidth + xCell * sampleStep;
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                if (profile.Kind == RegionThemeKind.Riverbank) continue;
                if (routeClearance > 0f && Mathf.Abs(x - roadX) < routeClearance) continue;

                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out float3 normal))
                    continue;

                float slopeDegrees = math.degrees(math.acos(math.clamp(normal.y, -1f, 1f)));
                if (slopeDegrees > ecology.MaxVegetationSlopeDegrees) continue;

                var cell = new RegionEcologyGridCell(xCell, zCell);
                _eligibleMeadowCells.Add(cell);
                _meadowCellByPosition[PositionKey(grounded)] = cell;
                _samples.Add(new VegetationSurfaceSample
                {
                    PositionMetres = grounded,
                    Normal = normal,
                    Surface = VegetationSurface.Ground,
                    Moisture = 0.5f,
                    Shade = profile.Kind == RegionThemeKind.PineForest ? 0.8f : 0.3f,
                    ArcaneSaturation = 0f,
                });
            }

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(
                ecology.DeriveSeed(world.Seed));
            settings.Density = ecology.VegetationDensity;
            settings.MaxGroundSlopeDegrees = ecology.MaxVegetationSlopeDegrees;
            settings.RestrictKinds = true;
            settings.AllowedKindsMask = BuildVegetationMask(ecology);

            _undergrowth.Clear();
            VegetationPlacement.Generate(_samples, in settings, _undergrowth);
            if (_undergrowth.Count > MaxUndergrowth)
                _undergrowth.RemoveRange(MaxUndergrowth, _undergrowth.Count - MaxUndergrowth);
            _vegetationRenderer.SetInstances(_undergrowth);

            GrassCount = 0;
            for (int i = 0; i < _undergrowth.Count; i++)
            {
                VegetationInstance instance = _undergrowth[i];
                if (instance.Kind != VegetationKind.Grass) continue;
                GrassCount++;
                if (_meadowCellByPosition.TryGetValue(PositionKey(instance.PositionMetres), out RegionEcologyGridCell cell))
                    _grassMeadowCells.Add(cell);
            }

            PrimaryMeadowGrassCount = RegionEcologyConnectivity.LargestConnectedOccupiedCount(
                _eligibleMeadowCells,
                _grassMeadowCells);
        }

        private void BuildWildlife(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth,
            RegionEcologyPolicy ecology)
        {
            _habitats.Clear();
            _clusters.Clear();
            if (ecology.AmbientAnimalKinds.Count == 0)
            {
                _lifeRenderer.SetClusters(_clusters);
                return;
            }

            uint ecologySeed = ecology.DeriveSeed(world.Seed);
            int maxHabitatSamples = MaxClusters * 3;
            for (float z = fromZ; z <= toZ && _habitats.Count < maxHabitatSamples;
                 z += HabitatSampleStepMetres)
            for (float x = roadX - halfWidth;
                 x <= roadX + halfWidth && _habitats.Count < maxHabitatSamples;
                 x += HabitatSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(ecologySeed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm ^ 0x99u);
                if (Random01(seed) * 1000f > profile.WildlifePerMille) continue;
                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out _)) continue;

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
            }

            AmbientLifePopulationSettings settings = AmbientLifePopulationSettings.Default(ecologySeed);
            AmbientLifePopulation.Generate(_habitats, in settings, _clusters);
            for (int i = _clusters.Count - 1; i >= 0; i--)
                if (!ecology.AllowsAmbientAnimal(_clusters[i].Kind.ToString())) _clusters.RemoveAt(i);
            if (_clusters.Count > MaxClusters)
                _clusters.RemoveRange(MaxClusters, _clusters.Count - MaxClusters);
            _lifeRenderer.SetClusters(_clusters);
        }

        private static ulong BuildVegetationMask(RegionEcologyPolicy ecology)
        {
            ulong mask = 0UL;
            for (int i = 0; i < ecology.VegetationKinds.Count; i++)
            {
                if (!Enum.TryParse(ecology.VegetationKinds[i], false, out VegetationKind kind)) continue;
                int bit = (int)kind;
                if (bit >= 0 && bit < 64) mask |= 1UL << bit;
            }
            return mask;
        }

        private static long PositionKey(float3 position)
        {
            int x = Mathf.RoundToInt(position.x * 1000f);
            int z = Mathf.RoundToInt(position.z * 1000f);
            return ((long)x << 32) ^ (uint)z;
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
