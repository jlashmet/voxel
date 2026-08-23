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
        private readonly List<VegetationInstance> _instances = new(32);
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
            ApplyReferenceEnvironment();
        }

        private void OnDisable()
        {
            _renderer?.Clear();
        }

        private void ApplyReferenceEnvironment()
        {
            // The target is a warm ruin against a near-black olive woodland, not an open blue sky.
            // Keep a little olive horizon energy so the stone and foliage retain readable fill.
            Camera camera = GetComponent<Camera>();
            if (camera != null)
                camera.backgroundColor = new Color(0.035f, 0.040f, 0.028f, 1f);
            RenderingComposition.SetSky(
                new Color(0.24f, 0.25f, 0.13f, 1f),
                new Color(0.035f, 0.042f, 0.028f, 1f));
        }

        private void BuildReferenceGrowth()
        {
            _instances.Clear();

            // The camera views the negative-Z face. Keep cards several centimetres in front of the
            // proud masonry so a future bevel/profile adjustment cannot turn vegetation into a
            // z-fighting or burial problem.
            const float frontZ = -0.06f;
            float3 front = new(0f, 0f, -1f);

            // Dense but broken-up left-side growth. Short overlapping climbers should read as leafy
            // masses attached to the stone instead of the continuous vertical cords from the first
            // vegetation pass.
            Add(VegetationKind.Ivy,          -1.96f, 0.48f, frontZ, front, 0.58f, 0xA01u);
            Add(VegetationKind.Ivy,          -1.68f, 1.36f, frontZ, front, 0.54f, 0xA02u);
            Add(VegetationKind.ClimbingVine, -1.92f, 2.26f, frontZ, front, 0.48f, 0xA03u);
            Add(VegetationKind.Ivy,          -1.60f, 3.08f, frontZ, front, 0.52f, 0xA04u);
            Add(VegetationKind.Ivy,          -1.88f, 4.02f, frontZ, front, 0.50f, 0xA05u);
            Add(VegetationKind.ClimbingVine, -1.54f, 4.88f, frontZ, front, 0.46f, 0xA06u);
            Add(VegetationKind.Ivy,          -1.72f, 5.66f, frontZ, front, 0.54f, 0xA07u);

            // The reference grows outward across the left haunch and then thins over the crown.
            Add(VegetationKind.Ivy, -1.46f, 6.38f, frontZ, front, 0.62f, 0xB01u);
            Add(VegetationKind.Ivy, -1.08f, 6.98f, frontZ, front, 0.66f, 0xB02u);
            Add(VegetationKind.Ivy, -0.58f, 7.48f, frontZ, front, 0.64f, 0xB03u);
            Add(VegetationKind.Ivy, -0.05f, 7.84f, frontZ, front, 0.58f, 0xB04u);
            Add(VegetationKind.Ivy,  0.46f, 8.02f, frontZ, front, 0.48f, 0xB05u);
            Add(VegetationKind.HangingVine, -1.24f, 7.34f, frontZ - 0.015f, front, 0.40f, 0xB06u);
            Add(VegetationKind.HangingVine, -0.50f, 7.84f, frontZ - 0.015f, front, 0.36f, 0xB07u);

            // Sparse right-hand counterweight. Keep visible interruptions so it never becomes a
            // mirrored second curtain.
            Add(VegetationKind.Ivy,          1.78f, 1.62f, frontZ, front, 0.34f, 0xC01u);
            Add(VegetationKind.Ivy,          1.70f, 3.48f, frontZ, front, 0.36f, 0xC02u);
            Add(VegetationKind.ClimbingVine, 1.56f, 5.04f, frontZ, front, 0.34f, 0xC03u);
            Add(VegetationKind.Ivy,          1.34f, 6.46f, frontZ, front, 0.36f, 0xC04u);
            Add(VegetationKind.HangingVine,  1.36f, 7.08f, frontZ - 0.015f, front, 0.32f, 0xC05u);

            // The voxel coating already provides low-frequency moss weathering. Avoid semantic
            // surface patches here: their planar silhouette competes with the leafy reference.
            float3 up = new(0f, 1f, 0f);
            Add(VegetationKind.Fern,   -2.04f, 0.02f, -0.12f, up, 0.52f, 0xE01u);
            Add(VegetationKind.Flower, -1.58f, 0.02f, -0.15f, up, 0.42f, 0xE02u);
            Add(VegetationKind.Fern,    1.92f, 0.02f, -0.08f, up, 0.34f, 0xE03u);
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
