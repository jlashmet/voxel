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
        private const float GrassChunkSizeMetres = 32f;

        private readonly List<TreeInstance> _trees = new();
        private readonly List<VegetationSurfaceSample> _samples = new();
        private readonly List<VegetationInstance> _undergrowth = new();
        private readonly List<RegionEcologyGridCell> _eligibleMeadowCells = new();
        private readonly List<RegionEcologyGridCell> _grassMeadowCells = new();
        private readonly Dictionary<long, RegionEcologyGridCell> _meadowCellByPosition = new();
        private readonly Dictionary<RegionEcologyGridCell, int> _grassBladeWeightByCell = new();
        private readonly HashSet<long> _excludedMeadowPositions = new();
        private readonly HashSet<long> _grassChunkKeys = new();
        private readonly List<AmbientLifeHabitatSample> _habitats = new();
        private readonly List<AmbientLifeCluster> _clusters = new();
        private ProceduralVegetationBatchRenderer _vegetationRenderer;
        private ProceduralTreeRenderer _treeRenderer;
        private ProceduralAmbientLifeRenderer _lifeRenderer;

        public int TreeCount => _trees.Count;
        public int UndergrowthCount => _undergrowth.Count;
        public int GrassCount { get; private set; }
        public int GrassBladeCount { get; private set; }
        public int PrimaryMeadowGrassCount { get; private set; }
        public int PrimaryMeadowBladeCount { get; private set; }
        public int ExcludedSurfaceGrassCount { get; private set; }
        public int GrassMeshChunkCount => _grassChunkKeys.Count;
        public int ClusterCount => _clusters.Count;
        public int RouteExclusionCount { get; private set; }
        public int BuiltContentExclusionCount { get; private set; }
        public int WaterExclusionCount { get; private set; }
        public int CultivatedExclusionCount { get; private set; }
        public int SteepOrCliffExclusionCount { get; private set; }
        public int OtherInvalidExclusionCount { get; private set; }

        public void Configure(
            ShowcaseWorld world,
            SettlementPlan settlement,
            RegionThemeMap themes,
            RegionCorridorPlan corridor,
            ProceduralTreeRenderer treeRenderer,
            ProceduralVegetationBatchRenderer vegetationRenderer,
            ProceduralAmbientLifeRenderer lifeRenderer)
        {
            _treeRenderer = treeRenderer;
            _vegetationRenderer = vegetationRenderer;
            _lifeRenderer = lifeRenderer;

            RegionEcologyPolicy ecology = settlement.CountrysideEcology ?? RegionEcologyPolicy.Empty;
            float roadX = corridor.StartDm.x * 0.1f;
            float fromZ = math.min(corridor.StartDm.y, corridor.EndDm.y) * 0.1f;
            float toZ = math.max(corridor.StartDm.y, corridor.EndDm.y) * 0.1f;
            float halfWidth = math.max(20f, corridor.HalfWidthDm * 0.1f + 28f);

            BuildTrees(world, themes, roadX, fromZ, toZ, halfWidth, ecology);
            BuildUndergrowth(world, themes, roadX, fromZ, toZ, halfWidth, ecology);
            BuildWildlife(world, themes, roadX, fromZ, toZ, halfWidth, ecology);
        }

        private void BuildTrees(
            ShowcaseWorld world, RegionThemeMap themes,
            float roadX, float fromZ, float toZ, float halfWidth,
            RegionEcologyPolicy ecology)
        {
            _trees.Clear();
            if (ecology.TreeKinds.Count == 0)
            {
                _treeRenderer.SetTrees(_trees);
                return;
            }

            uint ecologySeed = ecology.DeriveSeed(world.Seed);
            for (float z = fromZ; z <= toZ && _trees.Count < MaxTrees; z += TreeSampleStepMetres)
            for (float x = roadX - halfWidth; x <= roadX + halfWidth && _trees.Count < MaxTrees; x += TreeSampleStepMetres)
            {
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(ecologySeed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm);
                if (Random01(seed) * 1000f > profile.TreeDensityPerMille) continue;
                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out _, out bool builtContent)) continue;
                if (builtContent && ecology.Excludes(RegionEcologyExclusion.BuiltContent)) continue;

                TreeSpeciesSlot authored = profile.TreeSpecies[(int)(seed % (uint)profile.TreeSpecies.Length)];
                string authoredKind = authored.ToString();
                if (!ecology.AllowsTree(authoredKind)) continue;

                _trees.Add(new TreeInstance
                {
                    PositionMetres = grounded,
                    Species = SpeciesFor(authored),
                    Seed = seed,
                    Scale = math.lerp(0.8f, 1.25f, Random01(seed ^ 0x9E3779B9u)),
                });
            }
            _treeRenderer.SetTrees(_trees);
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
            _grassBladeWeightByCell.Clear();
            _excludedMeadowPositions.Clear();
            _grassChunkKeys.Clear();
            ResetExclusionDiagnostics();

            float meadowStep = math.max(0.25f, ecology.MeadowSampleSpacingMetres);
            uint ecologySeed = ecology.DeriveSeed(world.Seed);
            int maxCandidateSamples = MaxUndergrowth * 3;

            for (float z = fromZ; z <= toZ && _samples.Count < maxCandidateSamples; z += meadowStep)
            for (float x = roadX - halfWidth; x <= roadX + halfWidth && _samples.Count < maxCandidateSamples; x += meadowStep)
            {
                int xIndex = Mathf.RoundToInt((x - (roadX - halfWidth)) / meadowStep);
                int zIndex = Mathf.RoundToInt((z - fromZ) / meadowStep);
                var cell = new RegionEcologyGridCell(xIndex, zIndex);
                int zDm = Mathf.RoundToInt(z * 10f);
                RegionThemeProfile profile = themes.ProfileAt(zDm);
                uint seed = Hash(ecologySeed, (uint)Mathf.RoundToInt(x * 10f), (uint)zDm ^ 0x51u);

                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out float3 normal, out bool builtContent))
                {
                    OtherInvalidExclusionCount++;
                    continue;
                }

                float routeDistance = math.abs(x - roadX);
                bool route = routeDistance <= ecology.RouteClearanceMetres;
                bool water = profile.Kind == RegionThemeKind.Riverbank;
                bool cultivated = profile.Kind == RegionThemeKind.TemperateFarmland;
                float slopeDegrees = math.degrees(math.acos(math.clamp(normal.y, -1f, 1f)));
                bool steep = slopeDegrees > ecology.MaxVegetationSlopeDegrees;

                if (route && ecology.Excludes(RegionEcologyExclusion.RoutesAndPaths))
                {
                    RouteExclusionCount++;
                    _excludedMeadowPositions.Add(PositionKey(grounded));
                    continue;
                }
                if (builtContent && ecology.Excludes(RegionEcologyExclusion.BuiltContent))
                {
                    BuiltContentExclusionCount++;
                    _excludedMeadowPositions.Add(PositionKey(grounded));
                    continue;
                }
                if (water && ecology.Excludes(RegionEcologyExclusion.WaterOrWet))
                {
                    WaterExclusionCount++;
                    _excludedMeadowPositions.Add(PositionKey(grounded));
                    continue;
                }
                if (cultivated && ecology.Excludes(RegionEcologyExclusion.Cultivated))
                {
                    CultivatedExclusionCount++;
                    _excludedMeadowPositions.Add(PositionKey(grounded));
                    continue;
                }
                if (steep && ecology.Excludes(RegionEcologyExclusion.SteepOrCliff))
                {
                    SteepOrCliffExclusionCount++;
                    _excludedMeadowPositions.Add(PositionKey(grounded));
                    continue;
                }

                float regionDistance = math.abs(x - (roadX + ecology.MeadowOffsetMetres));
                bool inPrimaryMeadow = regionDistance <= ecology.MeadowRadiusMetres;
                float coverage = inPrimaryMeadow ? ecology.MeadowCoverage : ecology.BackgroundCoverage;
                uint coverageSeed = Hash(seed, 0xD1B54A35u, 0x94D049BBu);
                if (Random01(coverageSeed) > coverage) continue;

                _eligibleMeadowCells.Add(cell);
                _samples.Add(new VegetationSurfaceSample
                {
                    PositionMetres = grounded,
                    SurfaceNormal = normal,
                    Moisture = profile.Kind == RegionThemeKind.Riverbank ? 0.9f : 0.45f,
                    Shade = profile.Kind == RegionThemeKind.PineForest ? 0.7f : 0.25f,
                    Fertility = inPrimaryMeadow ? 1f : 0.65f,
                    SurfaceFlags = VegetationSurfaceFlags.None,
                    Seed = seed,
                });
                _meadowCellByPosition[PositionKey(grounded)] = cell;
            }

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(ecologySeed);
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
            GrassBladeCount = 0;
            ExcludedSurfaceGrassCount = 0;
            for (int i = 0; i < _undergrowth.Count; i++)
            {
                VegetationInstance instance = _undergrowth[i];
                if (instance.Kind != VegetationKind.Grass) continue;

                GrassCount++;
                int blades = ProceduralGrassPresentation.BladeCountForSeed(instance.Seed);
                GrassBladeCount += blades;
                _grassChunkKeys.Add(GrassChunkKey(instance.PositionMetres));

                long positionKey = PositionKey(instance.PositionMetres);
                if (_excludedMeadowPositions.Contains(positionKey))
                    ExcludedSurfaceGrassCount++;

                if (!_meadowCellByPosition.TryGetValue(positionKey, out RegionEcologyGridCell cell))
                    continue;
                _grassMeadowCells.Add(cell);
                _grassBladeWeightByCell[cell] = blades;
            }

            PrimaryMeadowGrassCount = RegionEcologyConnectivity.LargestConnectedOccupiedCount(
                _eligibleMeadowCells,
                _grassMeadowCells);
            PrimaryMeadowBladeCount = RegionEcologyConnectivity.LargestConnectedOccupiedWeight(
                _eligibleMeadowCells,
                _grassBladeWeightByCell);
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
                if (!TryGround(world, new float3(x, 0f, z), out float3 grounded, out _, out bool builtContent))
                    continue;
                if (builtContent && ecology.Excludes(RegionEcologyExclusion.BuiltContent)) continue;

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

        private void ResetExclusionDiagnostics()
        {
            RouteExclusionCount = 0;
            BuiltContentExclusionCount = 0;
            WaterExclusionCount = 0;
            CultivatedExclusionCount = 0;
            SteepOrCliffExclusionCount = 0;
            OtherInvalidExclusionCount = 0;
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

        private static long GrassChunkKey(float3 position)
        {
            int x = Mathf.FloorToInt(position.x / GrassChunkSizeMetres);
            int z = Mathf.FloorToInt(position.z / GrassChunkSizeMetres);
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
            ShowcaseWorld world,
            float3 position,
            out float3 grounded,
            out float3 normal,
            out bool builtContent)
        {
            grounded = default;
            normal = new float3(0f, 1f, 0f);
            int vx = (int)math.floor(position.x / ShowcaseWorld.VoxelSize);
            int vz = (int)math.floor(position.z / ShowcaseWorld.VoxelSize);
            builtContent = world.HasBuiltContentAbove(vx, vz);

            int height = world.SurfaceHeight(vx, vz);
            // SurfaceHeight is the index of the topmost occupied voxel. Presentation roots belong
            // on that voxel's exposed top face, which is one voxel edge above its integer index.
            grounded = new float3(position.x, (height + 1) * ShowcaseWorld.VoxelSize, position.z);
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
