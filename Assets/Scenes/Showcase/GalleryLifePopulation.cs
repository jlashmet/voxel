using System.Collections.Generic;
using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Vegetation.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
// UnityEngine has its own TreeInstance — a terrain-system record with nothing to do with this one.
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scatters low vegetation and ambient life across the gallery district.
    ///
    /// The placement policies for both systems already existed and were pure — they turn surface
    /// samples into instances and habitat samples into clusters — but nothing in the project ever
    /// fed them from a real world. They were exercised only by unit tests and by two lookdev
    /// scenes that lay their catalogues out on a flat grid, so the vegetation and wildlife systems
    /// had never actually stood on generated terrain next to authored buildings. This is the step
    /// that was missing: discover the surfaces, then hand them to the policies that already know
    /// what grows and what lives there.
    ///
    /// Two properties matter more than density here. Scatter must not land on built content — a
    /// grass tuft growing out of a cathedral roof reads as a bug and undoes the thing this is for
    /// — and the instance count has to stay bounded, because this is presentation competing for
    /// frame time with the renderer it is decorating.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GalleryLifePopulation : MonoBehaviour
    {
        /// <summary>
        /// Metres between vegetation samples. The gallery is walked at eye level, so the scatter
        /// has to be dense enough to read as ground cover rather than as scattered props. Placement
        /// thins these samples further by suitability and density, so this is an upper bound on
        /// resolution, not on instance count.
        /// </summary>
        private const float VegetationSampleStepMetres = 1.1f;

        /// <summary>
        /// Radius of the scattered disc. The exhibits, promenade and both guild houses fall inside
        /// roughly 120 m of the centre; past that the player is in open procedural terrain where
        /// far-field terrain, not scatter, is what sells the distance.
        /// </summary>
        private const float VegetationRadiusMetres = 80f;

        /// <summary>
        /// Ambient life is a cluster population, not a per-agent one: each sample that survives
        /// becomes a swarm the renderer reconstructs locally. Sampling it as finely as vegetation
        /// would produce overlapping swarms rather than more life.
        /// </summary>
        private const float HabitatSampleStepMetres = 18f;
        private const float HabitatRadiusMetres = 150f;

        /// <summary>
        /// Ceilings, enforced after placement. A density that looks right on a gentle slope can
        /// produce several times as much on broken ground, and the frame budget does not care why.
        /// Exceeding these is a content problem to fix, not something to discover as a frame spike,
        /// so crossing one is logged.
        /// </summary>
        // Ground cover has to be dense to read as ground cover. At one instance per 15 m² — which
        // is what a 2.5 m step over a 130 m disc produced — 3,584 plants were published, drawn,
        // and completely invisible from eye level. Concentrating a larger budget into the 80 m the
        // player actually walks is what makes the vegetation system visible at all, and the A/B
        // measurement says it is close to free: disabling vegetation entirely moved the frame rate
        // by less than run-to-run variance.
        private const int MaxVegetationInstances = 14000;
        private const int MaxAmbientClusters = 96;

        /// <summary>
        /// Trees are scattered in an annulus. The inner radius keeps the promenade and the
        /// approach to each exhibit clear — a gallery whose exhibits are hidden behind a wood is
        /// not showing them — and the outer radius stops where the district does.
        /// </summary>
        private const float TreeInnerRadiusMetres = 46f;
        private const float TreeOuterRadiusMetres = 150f;
        private const float TreeSampleStepMetres = 13f;
        private const int MaxGalleryTrees = 140;

        private readonly List<VegetationSurfaceSample> _samples = new();
        private readonly List<VegetationInstance> _vegetation = new();
        private readonly List<AmbientLifeHabitatSample> _habitats = new();
        private readonly List<AmbientLifeCluster> _clusters = new();
        private readonly List<TreeInstance> _trees = new();

        private IVegetationBatchRenderer _vegetationRenderer;
        private IAmbientLifeBatchRenderer _lifeRenderer;

        public int VegetationCount => _vegetation.Count;
        public int ClusterCount => _clusters.Count;
        public int TreeCount => _trees.Count;

        /// <summary>
        /// Fills the district around <paramref name="centreMetres"/>. The world must already hold
        /// its regions: scatter reads the built surface to decide where not to go, and a region
        /// that is not resident yet reads as open ground, which would drop grass through the
        /// promenade it was supposed to grow beside.
        /// </summary>
        public void Populate(ShowcaseWorld world, float3 centreMetres)
        {
            if (world == null) return;

            _vegetationRenderer ??=
                VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            _lifeRenderer ??=
                VegetationLifeRenderingComposition.EnsureAmbientLifeBatchRenderer(gameObject);

            BuildVegetation(world, centreMetres);
            BuildAmbientLife(world, centreMetres);
            BuildTrees(world, centreMetres);

            Debug.Log($"Gallery life: {_vegetation.Count} vegetation instances, "
                    + $"{_clusters.Count} ambient clusters, {_trees.Count} trees "
                    + $"around {centreMetres}.");
        }

        /// <summary>
        /// Publishes one tree world for the whole scene: the castle's own wood plus a scatter
        /// around the gallery district.
        ///
        /// Both have to be published together because the tree world is replaced wholesale, not
        /// appended to. Publishing them separately means whichever runs second silently deletes
        /// the other's trees — which is why <see cref="ShowcaseTreePopulation"/> stands down when
        /// this component is present rather than both of them writing.
        /// </summary>
        private void BuildTrees(ShowcaseWorld world, float3 centreMetres)
        {
            _trees.Clear();

            int castleCentreX = ShowcaseWorld.LandmarkCentreX;
            int castleCentreZ = ShowcaseWorld.LandmarkCentreZ;
            int ground = TerrainSampler.HeightAt(castleCentreX, castleCentreZ, world.Seed);
            CastlePlan plan = StructuresComposition.PlanCastle(
                new int3(castleCentreX, ground, castleCentreZ), world.Seed);
            Game.Structures.Api.CastlePlan gamePlan = plan;

            if (CastleVegetationPlanner.TryBuild(
                    in gamePlan, world.ReadStorage, world.Seed, out List<TreeInstance> castleTrees)
                && castleTrees != null)
                _trees.AddRange(castleTrees);

            int castleTreeCount = _trees.Count;

            int steps = Mathf.CeilToInt(TreeOuterRadiusMetres / TreeSampleStepMetres);
            float innerSq = TreeInnerRadiusMetres * TreeInnerRadiusMetres;
            float outerSq = TreeOuterRadiusMetres * TreeOuterRadiusMetres;

            for (int iz = -steps; iz <= steps; iz++)
            for (int ix = -steps; ix <= steps; ix++)
            {
                if (_trees.Count - castleTreeCount >= MaxGalleryTrees) break;

                float offsetX = ix * TreeSampleStepMetres;
                float offsetZ = iz * TreeSampleStepMetres;
                float distanceSq = offsetX * offsetX + offsetZ * offsetZ;
                if (distanceSq < innerSq || distanceSq > outerSq) continue;

                // Jitter off the lattice, deterministically. An unjittered grid of trees reads as
                // an orchard from any elevated vantage, and the gallery has several.
                uint seed = Hash(world.Seed, (uint)(ix * 73856093 ^ iz * 19349663));
                float3 jittered = centreMetres + new float3(
                    offsetX + (Random01(seed) - 0.5f) * TreeSampleStepMetres * 0.8f,
                    0f,
                    offsetZ + (Random01(seed ^ 0x9E3779B9u) - 0.5f) * TreeSampleStepMetres * 0.8f);

                if (!TryGroundSample(world, jittered, out float3 grounded, out float3 normal))
                    continue;

                // Trees need level footing far more than grass does: one planted on a steep bank
                // hangs half its trunk in the air.
                if (normal.y < 0.86f) continue;

                _trees.Add(new TreeInstance
                {
                    PositionMetres = grounded,
                    Species = SpeciesFor(seed),
                    Seed = seed == 0u ? 1u : seed,
                    Scale = 0.85f + Random01(seed ^ 0x85EBCA6Bu) * 0.45f,
                });
            }

            VegetationComposition.ReplaceTreeWorld(_trees);
        }

        private static TreeSpecies SpeciesFor(uint seed) => (Random01(seed ^ 0xC2B2AE35u)) switch
        {
            < 0.34f => TreeSpecies.Oak,
            < 0.58f => TreeSpecies.Pine,
            < 0.76f => TreeSpecies.Birch,
            < 0.90f => TreeSpecies.Maple,
            _ => TreeSpecies.Willow,
        };

        private static uint Hash(uint seed, uint value)
        {
            uint h = seed ^ 0x9E3779B9u;
            h ^= value + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= h >> 15;
            h *= 0x2545F491u;
            h ^= h >> 13;
            return h == 0u ? 1u : h;
        }

        private static float Random01(uint seed)
        {
            uint h = Hash(seed, 0x27D4EB2Fu);
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }

        private void BuildVegetation(ShowcaseWorld world, float3 centreMetres)
        {
            _samples.Clear();

            int steps = Mathf.CeilToInt(VegetationRadiusMetres / VegetationSampleStepMetres);
            float radiusSq = VegetationRadiusMetres * VegetationRadiusMetres;

            for (int iz = -steps; iz <= steps; iz++)
            for (int ix = -steps; ix <= steps; ix++)
            {
                float offsetX = ix * VegetationSampleStepMetres;
                float offsetZ = iz * VegetationSampleStepMetres;
                if (offsetX * offsetX + offsetZ * offsetZ > radiusSq) continue;

                float3 position = centreMetres + new float3(offsetX, 0f, offsetZ);
                if (!TryGroundSample(world, position, out float3 grounded, out float3 normal))
                    continue;

                // Slope drives what can grow as much as it drives whether anything does, so it is
                // passed through as the normal rather than filtered here; placement owns that
                // policy and already has a maximum ground slope.
                _samples.Add(new VegetationSurfaceSample
                {
                    PositionMetres = grounded,
                    Normal = normal,
                    Surface = VegetationSurface.Ground,
                    Moisture = Moisture(grounded),
                    Shade = Shade(normal),
                    ArcaneSaturation = 0f,
                });
            }

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(world.Seed);
            _vegetation.Clear();
            VegetationPlacement.Generate(_samples, in settings, _vegetation);
            Trim(_vegetation, MaxVegetationInstances, "vegetation instances");
            _vegetationRenderer.SetInstances(_vegetation);
        }

        private void BuildAmbientLife(ShowcaseWorld world, float3 centreMetres)
        {
            _habitats.Clear();

            int steps = Mathf.CeilToInt(HabitatRadiusMetres / HabitatSampleStepMetres);
            float radiusSq = HabitatRadiusMetres * HabitatRadiusMetres;

            for (int iz = -steps; iz <= steps; iz++)
            for (int ix = -steps; ix <= steps; ix++)
            {
                float offsetX = ix * HabitatSampleStepMetres;
                float offsetZ = iz * HabitatSampleStepMetres;
                if (offsetX * offsetX + offsetZ * offsetZ > radiusSq) continue;

                float3 position = centreMetres + new float3(offsetX, 0f, offsetZ);
                if (!TryGroundSample(world, position, out float3 grounded, out float3 normal))
                    continue;

                // Swarms hover rather than crawl, so a habitat sits above its ground sample.
                _habitats.Add(new AmbientLifeHabitatSample
                {
                    PositionMetres = grounded + new float3(0f, 1.6f, 0f),
                    RadiusMetres = 0f,
                    Moisture = Moisture(grounded),
                    Shade = Shade(normal),
                    FlowerDensity = 0.4f,
                    WaterPresence = 0f,
                    FungusDensity = 0.15f,
                    DeadwoodDensity = 0.2f,
                    ArcaneSaturation = 0f,
                });
            }

            AmbientLifePopulationSettings settings =
                AmbientLifePopulationSettings.Default(world.Seed);
            _clusters.Clear();
            AmbientLifePopulation.Generate(_habitats, in settings, _clusters);
            Trim(_clusters, MaxAmbientClusters, "ambient clusters");
            _lifeRenderer.SetClusters(_clusters);
        }

        /// <summary>
        /// Places a sample on the natural ground, or rejects the column outright.
        ///
        /// Rejection is the important half. Anything standing above terrain here is authored
        /// content — a plinth, a nave, a roof, the promenade — and scatter belongs beside it, not
        /// on it.
        /// </summary>
        private static bool TryGroundSample(
            ShowcaseWorld world, float3 position, out float3 grounded, out float3 normal)
        {
            grounded = default;
            normal = new float3(0f, 1f, 0f);

            int vx = (int)math.floor(position.x / ShowcaseWorld.VoxelSize);
            int vz = (int)math.floor(position.z / ShowcaseWorld.VoxelSize);
            if (world.HasBuiltContentAbove(vx, vz)) return false;

            int height = world.SurfaceHeight(vx, vz);
            grounded = new float3(position.x, height * ShowcaseWorld.VoxelSize, position.z);
            normal = SurfaceNormal(world, vx, vz);
            return true;
        }

        /// <summary>
        /// Terrain normal by central difference on the height field. The voxel surface is a
        /// staircase, so a normal taken from immediate neighbours is always one of a handful of
        /// values; sampling a few voxels out recovers the slope the terrain actually has.
        /// </summary>
        private static float3 SurfaceNormal(ShowcaseWorld world, int vx, int vz)
        {
            const int Step = 6;
            float dx = world.SurfaceHeight(vx + Step, vz) - world.SurfaceHeight(vx - Step, vz);
            float dz = world.SurfaceHeight(vx, vz + Step) - world.SurfaceHeight(vx, vz - Step);
            return math.normalizesafe(
                new float3(-dx, 2f * Step, -dz), new float3(0f, 1f, 0f));
        }

        /// <summary>
        /// Moisture rises in hollows and falls on ridges. This is a presentation heuristic, not a
        /// simulation: the gallery has no hydrology, and vegetation only needs a reason to vary
        /// across the district rather than to look uniformly sprinkled.
        /// </summary>
        private static float Moisture(float3 grounded) =>
            math.saturate(0.65f - (grounded.y - ShowcaseWorld.BaseHeightVoxels
                                   * ShowcaseWorld.VoxelSize) * 0.02f);

        private static float Shade(float3 normal) => math.saturate(1f - normal.y);

        private static void Trim<T>(List<T> values, int maximum, string label)
        {
            if (values.Count <= maximum) return;
            Debug.LogWarning($"Gallery life: {values.Count} {label} exceeds the {maximum} ceiling; "
                           + "trimming. Lower the sample density or the placement density.");
            values.RemoveRange(maximum, values.Count - maximum);
        }
    }
}
