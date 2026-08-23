using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic, art-directed growth for the ArchLookdev reference-match preset. The plants
    /// stay semantic and render through the production instanced vegetation path; there are no
    /// per-plant GameObjects or scene-owned vegetation prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchReferenceGrowth : MonoBehaviour
    {
        private readonly List<VegetationInstance> _instances = new(40);
        private IVegetationBatchRenderer _renderer;

        public int InstanceCount => _renderer?.InstanceCount ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachStartupScene()
        {
            AttachToArchLookdev();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToArchLookdev();
        }

        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowth>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowth>();
        }

        private void OnEnable()
        {
            _renderer = VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            BuildReferenceGrowth();
            _renderer.SetInstances(_instances);
        }

        private void OnDisable()
        {
            _renderer?.Clear();
        }

        private void BuildReferenceGrowth()
        {
            _instances.Clear();

            // The camera views the negative-Z face. Keep cards several centimetres in front of the
            // proud masonry so a future bevel/profile adjustment cannot turn vegetation into a
            // z-fighting or burial problem.
            const float frontZ = -0.06f;
            float3 front = new(0f, 0f, -1f);

            // Reference-dominant left pier: repeated leafy masses with a few long climbers joining
            // them into one continuous visual path from the foundation through the haunch.
            Add(VegetationKind.Ivy, -1.92f, 0.72f, frontZ, front, 0.72f, 0xA01u);
            Add(VegetationKind.Ivy, -1.76f, 1.55f, frontZ, front, 0.68f, 0xA02u);
            Add(VegetationKind.ClimbingVine, -1.70f, 2.10f, frontZ, front, 0.66f, 0xA03u);
            Add(VegetationKind.Ivy, -1.83f, 2.72f, frontZ, front, 0.74f, 0xA04u);
            Add(VegetationKind.Ivy, -1.58f, 3.55f, frontZ, front, 0.70f, 0xA05u);
            Add(VegetationKind.ClimbingVine, -1.56f, 4.22f, frontZ, front, 0.64f, 0xA06u);
            Add(VegetationKind.Ivy, -1.72f, 4.86f, frontZ, front, 0.76f, 0xA07u);
            Add(VegetationKind.Ivy, -1.52f, 5.56f, frontZ, front, 0.72f, 0xA08u);
            Add(VegetationKind.ClimbingVine, -1.38f, 6.12f, frontZ, front, 0.62f, 0xA09u);
            Add(VegetationKind.Ivy, -1.18f, 6.62f, frontZ, front, 0.74f, 0xA0Au);

            // Left haunch and crown should read as one lush mass instead of evenly distributed moss.
            Add(VegetationKind.Ivy, -1.56f, 6.96f, frontZ, front, 0.82f, 0xB01u);
            Add(VegetationKind.Ivy, -1.15f, 7.46f, frontZ, front, 0.86f, 0xB02u);
            Add(VegetationKind.Ivy, -0.66f, 7.90f, frontZ, front, 0.82f, 0xB03u);
            Add(VegetationKind.Ivy, -0.15f, 8.24f, frontZ, front, 0.76f, 0xB04u);
            Add(VegetationKind.Ivy, 0.42f, 8.42f, frontZ, front, 0.68f, 0xB05u);
            Add(VegetationKind.HangingVine, -1.40f, 7.55f, frontZ - 0.015f, front, 0.74f, 0xB06u);
            Add(VegetationKind.HangingVine, -0.78f, 8.00f, frontZ - 0.015f, front, 0.62f, 0xB07u);
            Add(VegetationKind.HangingVine, 0.20f, 8.38f, frontZ - 0.015f, front, 0.52f, 0xB08u);

            // Sparse right-hand counterweight. The reference has readable growth here, but much less
            // than the left side, with one longer descending strand.
            Add(VegetationKind.Ivy, 1.78f, 1.10f, frontZ, front, 0.42f, 0xC01u);
            Add(VegetationKind.Ivy, 1.82f, 2.28f, frontZ, front, 0.46f, 0xC02u);
            Add(VegetationKind.ClimbingVine, 1.70f, 3.30f, frontZ, front, 0.46f, 0xC03u);
            Add(VegetationKind.Ivy, 1.72f, 4.28f, frontZ, front, 0.48f, 0xC04u);
            Add(VegetationKind.HangingVine, 1.55f, 5.42f, frontZ - 0.015f, front, 0.52f, 0xC05u);
            Add(VegetationKind.Ivy, 1.50f, 6.30f, frontZ, front, 0.50f, 0xC06u);
            Add(VegetationKind.Ivy, 1.26f, 7.22f, frontZ, front, 0.48f, 0xC07u);

            // Low-frequency surface patches bridge a few joints without becoming the uniform green
            // seam noise that the reference does not have.
            Add(VegetationKind.Moss, -2.00f, 0.40f, frontZ - 0.01f, front, 0.46f, 0xD01u);
            Add(VegetationKind.Lichen, -1.82f, 2.38f, frontZ - 0.01f, front, 0.38f, 0xD02u);
            Add(VegetationKind.Moss, -1.55f, 4.72f, frontZ - 0.01f, front, 0.42f, 0xD03u);
            Add(VegetationKind.Lichen, -0.92f, 7.34f, frontZ - 0.01f, front, 0.36f, 0xD04u);
            Add(VegetationKind.Moss, 1.82f, 4.12f, frontZ - 0.01f, front, 0.32f, 0xD05u);
            Add(VegetationKind.Lichen, 1.42f, 6.80f, frontZ - 0.01f, front, 0.30f, 0xD06u);

            // Small foundation plants help the freestanding ruin meet the ground naturally.
            float3 up = new(0f, 1f, 0f);
            Add(VegetationKind.Fern, -2.05f, 0.02f, -0.12f, up, 0.62f, 0xE01u);
            Add(VegetationKind.Flower, -1.58f, 0.02f, -0.15f, up, 0.48f, 0xE02u);
            Add(VegetationKind.Fern, 1.92f, 0.02f, -0.08f, up, 0.42f, 0xE03u);
        }

        private void Add(
            VegetationKind kind,
            float x,
            float y,
            float z,
            float3 normal,
            float scale,
            uint seed)
        {
            _instances.Add(new VegetationInstance
            {
                PositionMetres = new float3(x, y, z),
                SurfaceNormal = normal,
                Kind = kind,
                Seed = seed,
                Scale = scale,
            });
        }
    }
}
