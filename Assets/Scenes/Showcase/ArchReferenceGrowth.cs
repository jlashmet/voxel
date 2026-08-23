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
        private readonly List<VegetationInstance> _instances = new(48);
        private IVegetationBatchRenderer _renderer;
        private float _originalCloudOpacity;
        private bool _environmentApplied;

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
            _originalCloudOpacity = RenderingComposition.GetCloudOpacity();
            ApplyReferenceEnvironment();
            _environmentApplied = true;
        }

        private void OnDisable()
        {
            _renderer?.Clear();
            if (_environmentApplied)
            {
                RenderingComposition.SetCloudOpacity(_originalCloudOpacity);
                _environmentApplied = false;
            }
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
            RenderingComposition.SetCloudOpacity(0f);
        }

        private void BuildReferenceGrowth()
        {
            _instances.Clear();

            // The camera views the negative-Z face. Keep cards several centimetres in front of the
            // proud masonry so a future bevel/profile adjustment cannot turn vegetation into a
            // z-fighting or burial problem.
            const float frontZ = -0.06f;
            float3 front = new(0f, 0f, -1f);

            // The reference is dominated by broad overlapping leaf masses down the left pier.
            // WallFern supplies that volume; short ivy runs stitch the masses together without
            // returning to the repeated vertical-chain silhouette of the earlier pass.
            Add(VegetationKind.WallFern, -1.98f, 0.42f, frontZ, front, 0.72f, 0xA01u);
            Add(VegetationKind.Ivy,      -1.72f, 0.82f, frontZ - 0.01f, front, 0.32f, 0xA02u);
            Add(VegetationKind.WallFern, -1.66f, 1.22f, frontZ, front, 0.82f, 0xA03u);
            Add(VegetationKind.WallFern, -1.98f, 1.92f, frontZ, front, 0.78f, 0xA04u);
            Add(VegetationKind.Ivy,      -1.70f, 2.22f, frontZ - 0.01f, front, 0.30f, 0xA05u);
            Add(VegetationKind.Flower,   -1.54f, 2.54f, frontZ - 0.02f, front, 0.38f, 0xA06u);
            Add(VegetationKind.WallFern, -1.70f, 2.82f, frontZ, front, 0.92f, 0xA07u);
            Add(VegetationKind.WallFern, -1.96f, 3.58f, frontZ, front, 0.84f, 0xA08u);
            Add(VegetationKind.Ivy,      -1.68f, 3.88f, frontZ - 0.01f, front, 0.30f, 0xA09u);
            Add(VegetationKind.Flower,   -1.50f, 4.16f, frontZ - 0.02f, front, 0.34f, 0xA0Au);
            Add(VegetationKind.WallFern, -1.62f, 4.42f, frontZ, front, 0.94f, 0xA0Bu);
            Add(VegetationKind.Ivy,      -1.82f, 4.82f, frontZ - 0.01f, front, 0.32f, 0xA0Cu);
            Add(VegetationKind.WallFern, -1.76f, 5.18f, frontZ, front, 0.90f, 0xA0Du);
            Add(VegetationKind.WallFern, -1.48f, 5.82f, frontZ, front, 0.88f, 0xA0Eu);
            Add(VegetationKind.Flower,   -1.28f, 6.10f, frontZ - 0.02f, front, 0.36f, 0xA0Fu);

            // Push the same leafy mass across the left haunch and crown. The reference becomes most
            // lush here, with a few flowers rising out of the foliage and only short hanging tips.
            Add(VegetationKind.WallFern, -1.44f, 6.34f, frontZ, front, 0.96f, 0xB01u);
            Add(VegetationKind.Ivy,      -1.18f, 6.64f, frontZ - 0.01f, front, 0.32f, 0xB02u);
            Add(VegetationKind.WallFern, -1.08f, 6.78f, frontZ, front, 0.98f, 0xB03u);
            Add(VegetationKind.WallFern, -0.74f, 7.16f, frontZ, front, 0.92f, 0xB04u);
            Add(VegetationKind.Flower,   -0.56f, 7.38f, frontZ - 0.02f, front, 0.36f, 0xB05u);
            Add(VegetationKind.WallFern, -0.36f, 7.50f, frontZ, front, 0.84f, 0xB06u);
            Add(VegetationKind.WallFern,  0.04f, 7.72f, frontZ, front, 0.76f, 0xB07u);
            Add(VegetationKind.Flower,    0.22f, 7.92f, frontZ - 0.02f, front, 0.32f, 0xB08u);
            Add(VegetationKind.WallFern,  0.44f, 7.92f, frontZ, front, 0.62f, 0xB09u);
            Add(VegetationKind.HangingVine, -1.10f, 7.18f, frontZ - 0.025f, front, 0.34f, 0xB0Au);
            Add(VegetationKind.HangingVine, -0.30f, 7.68f, frontZ - 0.025f, front, 0.28f, 0xB0Bu);

            // The right side stays deliberately sparse, matching the reference's asymmetry. Use a
            // handful of leafy islands with one climbing connector rather than a mirrored curtain.
            Add(VegetationKind.WallFern,     1.78f, 1.52f, frontZ, front, 0.42f, 0xC01u);
            Add(VegetationKind.Ivy,          1.68f, 2.18f, frontZ - 0.01f, front, 0.26f, 0xC02u);
            Add(VegetationKind.WallFern,     1.62f, 3.62f, frontZ, front, 0.46f, 0xC03u);
            Add(VegetationKind.ClimbingVine, 1.56f, 4.36f, frontZ - 0.01f, front, 0.28f, 0xC04u);
            Add(VegetationKind.WallFern,     1.46f, 5.18f, frontZ, front, 0.52f, 0xC05u);
            Add(VegetationKind.WallFern,     1.28f, 6.30f, frontZ, front, 0.50f, 0xC06u);
            Add(VegetationKind.HangingVine,  1.32f, 6.86f, frontZ - 0.025f, front, 0.26f, 0xC07u);

            // Ground accents remain small; the visual focus should stay on masonry-bound growth.
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
