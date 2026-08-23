using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Art-directed, deterministic vegetation for the ArchLookdev hero preset.
    ///
    /// The stone coating remains the low-frequency weathering layer; these semantic vegetation
    /// instances provide the readable ivy, hanging vines and localized moss silhouettes present in
    /// the reference. Rendering still goes through the production instanced vegetation path rather
    /// than scene-owned prefabs or one GameObject per plant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchReferenceGrowth : MonoBehaviour
    {
        private readonly List<VegetationInstance> _instances = new List<VegetationInstance>(32);
        private IVegetationBatchRenderer _renderer;

        // Attach after the first scene is fully active. The previous sceneLoaded subscription was
        // fragile in standalone capture: the component compiled into the player but no vegetation
        // ever reached the frame. A direct post-load bootstrap is deterministic for this one-scene
        // lookdev bench and still keeps all actual rendering behind Rendering.Api.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null)
            {
                Debug.LogWarning("ARCH_GROWTH no ArchLookdev found after scene load");
                return;
            }

            ArchReferenceGrowth growth = lookdev.GetComponent<ArchReferenceGrowth>();
            if (growth == null)
                growth = lookdev.gameObject.AddComponent<ArchReferenceGrowth>();
            Debug.Log($"ARCH_GROWTH attached={growth != null}");
        }

        private void OnEnable()
        {
            _renderer = VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            BuildReferenceGrowth();
            _renderer.SetInstances(_instances);
            Debug.Log($"ARCH_GROWTH instances={_renderer.InstanceCount}");
        }

        private void OnDisable()
        {
            _renderer?.Clear();
        }

        private void BuildReferenceGrowth()
        {
            _instances.Clear();

            // Camera-facing masonry is the negative-Z side of the authored bay. The hero preset is
            // centred at X=0 and begins at Z=0; the proud retained ring projects to about Z=0.05 m.
            // Keep the vegetation a few centimetres forward so leaves never z-fight the stone.
            const float frontZ = 0.015f;
            float3 front = new float3(0f, 0f, -1f);

            // Dense, climbing ivy on the left pier that gathers into the left haunch/crown.
            Add(VegetationKind.Ivy, -1.86f, 1.05f, frontZ, front, 0.48f, 0xA01u);
            Add(VegetationKind.ClimbingVine, -1.78f, 2.10f, frontZ, front, 0.52f, 0xA02u);
            Add(VegetationKind.Ivy, -1.68f, 3.28f, frontZ, front, 0.55f, 0xA03u);
            Add(VegetationKind.Ivy, -1.54f, 4.42f, frontZ, front, 0.58f, 0xA04u);
            Add(VegetationKind.ClimbingVine, -1.36f, 5.48f, frontZ, front, 0.56f, 0xA05u);
            Add(VegetationKind.Ivy, -1.10f, 6.42f, frontZ, front, 0.52f, 0xA06u);
            Add(VegetationKind.Ivy, -0.74f, 7.22f, frontZ, front, 0.48f, 0xA07u);
            Add(VegetationKind.Ivy, -0.30f, 7.78f, frontZ, front, 0.44f, 0xA08u);

            // A leafy crown with two downward strands gives the ruin the asymmetrical overgrown
            // silhouette of the reference without hiding the voussoir rhythm.
            Add(VegetationKind.Ivy, -1.72f, 7.52f, frontZ, front, 0.48f, 0xB01u);
            Add(VegetationKind.Ivy, -1.18f, 8.16f, frontZ, front, 0.50f, 0xB02u);
            Add(VegetationKind.Ivy, -0.56f, 8.48f, frontZ, front, 0.46f, 0xB03u);
            Add(VegetationKind.HangingVine, -1.36f, 8.42f, frontZ - 0.01f, front, 0.60f, 0xB04u);
            Add(VegetationKind.HangingVine, -0.78f, 8.63f, frontZ - 0.01f, front, 0.46f, 0xB05u);

            // Sparse counter-weight on the right side: enough to integrate the silhouette, but the
            // reference remains visibly heavier on the left/top.
            Add(VegetationKind.Ivy, 1.86f, 3.55f, frontZ, front, 0.36f, 0xC01u);
            Add(VegetationKind.ClimbingVine, 1.78f, 4.55f, frontZ, front, 0.38f, 0xC02u);
            Add(VegetationKind.Ivy, 1.64f, 5.58f, frontZ, front, 0.36f, 0xC03u);
            Add(VegetationKind.HangingVine, 1.42f, 6.64f, frontZ, front, 0.34f, 0xC04u);

            // Localized surface growth replaces the previous visual impression of uniform green
            // noise. Small patches bridge joints and sit beneath the larger ivy masses.
            Add(VegetationKind.Moss, -2.02f, 0.66f, frontZ - 0.01f, front, 0.50f, 0xD01u);
            Add(VegetationKind.Lichen, -1.90f, 2.74f, frontZ - 0.01f, front, 0.42f, 0xD02u);
            Add(VegetationKind.Moss, -1.52f, 5.78f, frontZ - 0.01f, front, 0.46f, 0xD03u);
            Add(VegetationKind.Lichen, -0.86f, 7.72f, frontZ - 0.01f, front, 0.38f, 0xD04u);
            Add(VegetationKind.Moss, 1.88f, 4.20f, frontZ - 0.01f, front, 0.34f, 0xD05u);

            // A few small plants at the foundations help the freestanding arch sit in a natural
            // setting even in the stripped-down lookdev scene.
            float3 up = new float3(0f, 1f, 0f);
            Add(VegetationKind.Fern, -2.02f, 0.02f, -0.08f, up, 0.50f, 0xE01u);
            Add(VegetationKind.Flower, -1.58f, 0.02f, -0.12f, up, 0.42f, 0xE02u);
            Add(VegetationKind.Fern, 1.92f, 0.02f, -0.02f, up, 0.38f, 0xE03u);
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
