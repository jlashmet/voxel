using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene lifecycle adapter only. Deterministic vegetation placement belongs to the worldgen
    /// package; this component waits for the Showcase world, realizes that plan against the resident
    /// terrain surface, then publishes one semantic tree snapshot through application composition.
    /// </summary>
    [DefaultExecutionOrder(350)]
    public sealed class ShowcaseTreePopulation : MonoBehaviour
    {
        private bool _done;

        public static bool Completed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Completed = false;
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
            _done = true;
            Completed = true;
            enabled = false;
            Debug.Log($"Procedural vegetation: worldgen published {instances.Count} semantic Showcase trees.");
        }
    }
}
