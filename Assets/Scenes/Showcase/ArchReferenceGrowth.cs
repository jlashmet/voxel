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
        private readonly List<VegetationInstance> _instances = new(80);
        private IVegetationBatchRenderer _renderer;
        private float _originalCloudOpacity;
        private bool _environmentApplied;

        public int InstanceCount => _renderer?.InstanceCount ?? 0;
        public IReadOnlyList<VegetationInstance> Instances => _instances;

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
            // Match the reference's clean studio-like presentation. A nearly white horizon and
            // zenith keep the foliage colors honest and let the warm masonry silhouette read clearly.
            Camera camera = GetComponent<Camera>();
            if (camera != null)
                camera.backgroundColor = new Color(0.985f, 0.985f, 0.980f, 1f);
            RenderingComposition.SetSky(
                new Color(0.990f, 0.990f, 0.985f, 1f),
                new Color(0.965f, 0.970f, 0.965f, 1f));
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

            // Build the left side from overlapping short ivy instances. Each individual climber is
            // intentionally modest; the reference's broad leafy blankets emerge from several offset
            // instances merging together instead of one stretched plant or a fern-shaped stand-in.
            AddIvyCluster(-1.90f, 0.45f, 0.56f, frontZ, front, 0xA100u);
            AddIvyCluster(-1.72f, 1.25f, 0.58f, frontZ, front, 0xA200u);
            AddIvyCluster(-1.90f, 2.08f, 0.60f, frontZ, front, 0xA300u);
            AddIvyCluster(-1.68f, 2.92f, 0.62f, frontZ, front, 0xA400u);
            AddIvyCluster(-1.88f, 3.78f, 0.62f, frontZ, front, 0xA500u);
            AddIvyCluster(-1.64f, 4.66f, 0.64f, frontZ, front, 0xA600u);
            AddIvyCluster(-1.72f, 5.50f, 0.64f, frontZ, front, 0xA700u);

            // Flower heads are deliberately large enough to survive the hero-camera distance and
            // cluster asymmetrically inside the ivy blanket like the reference rather than forming
            // an evenly-spaced decorative stripe.
            Add(VegetationKind.Flower, -1.48f, 2.66f, frontZ - 0.025f, front, 0.56f, 0xAF01u);
            Add(VegetationKind.Flower, -1.82f, 3.56f, frontZ - 0.030f, front, 0.48f, 0xAF04u);
            Add(VegetationKind.Flower, -1.52f, 4.22f, frontZ - 0.025f, front, 0.58f, 0xAF02u);
            Add(VegetationKind.Flower, -1.26f, 5.14f, frontZ - 0.030f, front, 0.52f, 0xAF05u);
            Add(VegetationKind.Flower, -1.30f, 5.96f, frontZ - 0.025f, front, 0.60f, 0xAF03u);

            // The densest mass crosses the left haunch and upper crown, then breaks apart before the
            // far-right shoulder so the composition keeps the target's strong asymmetry.
            AddIvyCluster(-1.42f, 6.26f, 0.68f, frontZ, front, 0xB100u);
            AddIvyCluster(-1.08f, 6.76f, 0.70f, frontZ, front, 0xB200u);
            AddIvyCluster(-0.70f, 7.18f, 0.68f, frontZ, front, 0xB300u);
            AddIvyCluster(-0.28f, 7.48f, 0.64f, frontZ, front, 0xB400u);
            AddIvyCluster( 0.18f, 7.68f, 0.56f, frontZ, front, 0xB500u);
            Add(VegetationKind.Flower, -1.04f, 6.74f, frontZ - 0.030f, front, 0.54f, 0xBF03u);
            Add(VegetationKind.Flower, -0.62f, 7.48f, frontZ - 0.025f, front, 0.60f, 0xBF01u);
            Add(VegetationKind.Flower, -0.18f, 7.34f, frontZ - 0.030f, front, 0.50f, 0xBF04u);
            Add(VegetationKind.Flower,  0.12f, 7.88f, frontZ - 0.025f, front, 0.54f, 0xBF02u);
            Add(VegetationKind.HangingVine, -1.12f, 7.16f, frontZ - 0.03f, front, 0.32f, 0xB601u);
            Add(VegetationKind.HangingVine, -0.30f, 7.62f, frontZ - 0.03f, front, 0.28f, 0xB602u);

            // Sparse right-hand counterweight: small paired growth islands with visible bare masonry
            // between them, plus one short connector near the upper pier.
            AddIvyPair(1.76f, 1.60f, 0.34f, frontZ, front, 0xC100u);
            AddIvyPair(1.66f, 3.58f, 0.36f, frontZ, front, 0xC200u);
            AddIvyPair(1.48f, 5.20f, 0.38f, frontZ, front, 0xC300u);
            AddIvyPair(1.28f, 6.42f, 0.38f, frontZ, front, 0xC400u);
            Add(VegetationKind.ClimbingVine, 1.48f, 4.42f, frontZ - 0.02f, front, 0.26f, 0xC501u);
            Add(VegetationKind.HangingVine,  1.30f, 6.84f, frontZ - 0.03f, front, 0.24f, 0xC502u);

            // Ground accents remain small; the visual focus should stay on masonry-bound growth.
            float3 up = new(0f, 1f, 0f);
            Add(VegetationKind.Fern,   -2.04f, 0.02f, -0.12f, up, 0.46f, 0xE01u);
            Add(VegetationKind.Flower, -1.58f, 0.02f, -0.15f, up, 0.42f, 0xE02u);
            Add(VegetationKind.Fern,    1.92f, 0.02f, -0.08f, up, 0.30f, 0xE03u);
        }

        private void AddIvyCluster(
            float x, float y, float scale, float z, float3 normal, uint seed)
        {
            Add(VegetationKind.Ivy, x - 0.18f, y - 0.10f, z, normal, scale * 0.92f, seed + 1u);
            Add(VegetationKind.Ivy, x + 0.12f, y + 0.02f, z - 0.008f, normal, scale, seed + 2u);
            Add(VegetationKind.Ivy, x - 0.02f, y + 0.24f, z - 0.016f, normal, scale * 0.86f, seed + 3u);
        }

        private void AddIvyPair(
            float x, float y, float scale, float z, float3 normal, uint seed)
        {
            Add(VegetationKind.Ivy, x - 0.10f, y, z, normal, scale, seed + 1u);
            Add(VegetationKind.Ivy, x + 0.10f, y + 0.18f, z - 0.010f, normal, scale * 0.86f, seed + 2u);
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
