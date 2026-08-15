using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene lifecycle adapter only. Deterministic vegetation placement belongs to the worldgen
    /// package; this component waits for the Showcase world, realizes that plan against the resident
    /// terrain surface, then publishes one semantic tree snapshot into Core runtime state.
    /// </summary>
    [DefaultExecutionOrder(350)]
    public sealed class ShowcaseTreePopulation : MonoBehaviour
    {
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

            if (!CastleVegetationPlanner.TryBuild(
                    in plan, view.Storage, worldSeed, out var instances))
                return;

            TreeWorldState.Replace(instances);
            _done = true;
            Completed = true;
            enabled = false;
            Debug.Log($"Procedural vegetation: worldgen published {instances.Count} semantic Showcase trees.");
        }
    }
}
