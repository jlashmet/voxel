using System.Collections.Generic;
using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene lifecycle adapter only. Deterministic vegetation placement belongs to the worldgen
    /// package; this component waits for the Showcase world, realizes that plan against the resident
    /// terrain surface, then publishes one semantic tree snapshot through application composition.
    /// Far visibility reads that same snapshot through TreeWorldReadRegistry; it never owns a second
    /// tree list or requests voxel residency.
    /// </summary>
    [DefaultExecutionOrder(350)]
    public sealed class ShowcaseTreePopulation : MonoBehaviour
    {
        internal const float VisibilitySectorSizeMetres = 64f;
        internal const float NaturalLandmarkHeightMetres = 28f;

        private bool _done;

        public static bool Completed { get; private set; }
        public static int PublishedTreeCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Completed = false;
            PublishedTreeCount = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureInstance();

        /// <summary>
        /// A single-scene load destroys this adapter along with the rest of the scene, and
        /// AfterSceneLoad only fires once per play session, so without this a second showcase
        /// load would come up with no vegetation at all — and anything reading the tree registry
        /// would silently observe the previous world's trees, damage included.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            Completed = false;
            PublishedTreeCount = 0;
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (FindFirstObjectByType<ShowcaseTreePopulation>() != null) return;
            var go = new GameObject("Showcase Tree Population")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.AddComponent<ShowcaseTreePopulation>();
        }

        private void Update()
        {
            if (_done) return;
            if (!RenderingComposition.TryGetWorld(out RenderingWorldBinding world, out uint worldSeed)) return;

            // These trees are sited against the castle plan, which sculpts the outcrop they stand
            // on. A scene that builds no castle never sculpts it, so publishing them anyway leaves
            // a wood hanging in the air above untouched ground.
            var showcase = FindFirstObjectByType<VoxelShowcase>();
            if (showcase != null && showcase.Features != ShowcaseFeatureContent.Full)
            {
                _done = true;
                Completed = true;
                enabled = false;
                return;
            }

            // The tree world is replaced wholesale rather than appended to, so exactly one
            // component may publish it. A scene that scatters its own trees publishes the castle's
            // as well; if this ran too it would delete them a frame later.
            if (FindFirstObjectByType<GalleryLifePopulation>() != null)
            {
                _done = true;
                Completed = true;
                enabled = false;
                return;
            }

            int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = TerrainSampler.HeightAt(cx, cz, worldSeed);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), worldSeed);

            // The showcase CastlePlan is a wrapper around the authoritative
            // Game.Structures.Api one and converts implicitly — but an implicit conversion
            // cannot be applied to an `in` parameter, so unwrap it explicitly first.
            Game.Structures.Api.CastlePlan gamePlan = plan;

            if (!CastleVegetationPlanner.TryBuild(
                    in gamePlan, world.Storage, worldSeed, out var instances))
                return;

            VegetationComposition.ReplaceTreeWorld(instances);
            PublishedTreeCount = instances.Count;
            _done = true;
            Completed = true;
            enabled = false;
            Debug.Log($"Procedural vegetation: worldgen published {instances.Count} semantic Showcase trees.");
        }

        /// <summary>
        /// Showcase far-presentation composition queries the authoritative tree registry by sectors.
        /// This is deliberately a projection only: generation, damage and sever state stay in the
        /// existing tree world and no distant voxel region is generated to answer the query.
        /// </summary>
        internal static void QueryPublishedTrees(
            float3 cameraPosition,
            float radiusMetres,
            List<TreeVisibilityEntry> output)
        {
            VisibilitySectorBounds sectors = VisibilitySectorBounds.Around(
                new float2(cameraPosition.x, cameraPosition.z),
                radiusMetres,
                VisibilitySectorSizeMetres);
            VegetationVisibility.QueryTrees(
                TreeWorldReadRegistry.Current,
                VisibilitySectorSizeMetres,
                in sectors,
                output);
        }

        /// <summary>
        /// Scene policy for exceptional natural features. The shared far-feature selector remains
        /// producer-agnostic; Showcase only promotes a tree when its semantic species/scale implies
        /// a genuinely landmark-sized silhouette.
        /// </summary>
        internal static FarFeatureImportance ImportanceFor(in TreeVisibilityEntry tree)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(tree.Instance.Species);
            float height = profile.MidHeight * math.max(0.05f, tree.Instance.Scale);
            return height >= NaturalLandmarkHeightMetres
                ? FarFeatureImportance.Horizon
                : FarFeatureImportance.Default;
        }
    }
}
