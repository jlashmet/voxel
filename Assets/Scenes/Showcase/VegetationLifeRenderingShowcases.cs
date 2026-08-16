using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.AmbientLife;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.AmbientLife;
using VoxelEngine.Rendering.Vegetation;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Lightweight, standalone showcase for the semantic vegetation renderer. It deliberately
    /// does not allocate a voxel world: every catalogue kind is represented directly through the
    /// public vegetation API and handed to the production instanced renderer.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Vegetation Rendering Showcase")]
    [DisallowMultipleComponent]
    public sealed class VegetationRenderingShowcase : MonoBehaviour
    {
        public const int InstancesPerKind = 3;

        [SerializeField] private uint m_Seed = 0x71E6A710u;
        [SerializeField] private bool m_CreateEnvironment = true;

        private readonly List<VegetationInstance> _instances = new();
        private ProceduralVegetationBatchRenderer _renderer;

        public ProceduralVegetationBatchRenderer Renderer => _renderer;
        public IReadOnlyList<VegetationInstance> Instances => _instances;
        public int InstanceCount => _instances.Count;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_renderer == null)
                _renderer = GetComponent<ProceduralVegetationBatchRenderer>()
                            ?? gameObject.AddComponent<ProceduralVegetationBatchRenderer>();

            if (m_CreateEnvironment)
                SubsystemRenderingShowcaseEnvironment.Ensure(transform);

            BuildInstances(m_Seed, _instances);
            _renderer.SetInstances(_instances);
        }

        public static void BuildInstances(uint seed, List<VegetationInstance> output)
        {
            output.Clear();

            const int columns = 8;
            const float spacing = 2.15f;
            for (int i = 0; i < VegetationCatalogue.Count; i++)
            {
                VegetationKind kind = VegetationCatalogue.KindAt(i);
                VegetationProfile profile = VegetationCatalogue.Get(kind);
                int column = i % columns;
                int row = i / columns;

                for (int sample = 0; sample < InstancesPerKind; sample++)
                {
                    uint instanceSeed = seed
                        + (uint)i * 0x9E3779B9u
                        + (uint)sample * 0x85EBCA6Bu;
                    float sampleOffset = (sample - 1) * 0.34f;
                    float3 normal = new float3(0f, 1f, 0f);
                    float3 position;

                    if (profile.GrowthForm == VegetationGrowthForm.Climber
                        || profile.GrowthForm == VegetationGrowthForm.Hanger)
                    {
                        // Put wall-attached growth against the showcase back wall. A vertical
                        // semantic normal is also important here: using Vector3.up would make the
                        // vine renderer's LookRotation forward/up inputs collinear.
                        normal = new float3(0f, 0f, -1f);
                        position = new float3(
                            (column - 3.5f) * spacing + sampleOffset,
                            0.85f + row * 0.52f + sample * 0.22f,
                            9.72f);
                    }
                    else
                    {
                        position = new float3(
                            (column - 3.5f) * spacing + sampleOffset,
                            0f,
                            1.3f + row * spacing + sample * 0.12f);
                    }

                    output.Add(new VegetationInstance
                    {
                        PositionMetres = position,
                        SurfaceNormal = normal,
                        Kind = kind,
                        Seed = instanceSeed == 0u ? 1u : instanceSeed,
                        Scale = 0.88f + sample * 0.12f,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Lightweight, standalone showcase for ambient-life rendering. Every semantic ambient-life
    /// kind is represented as a deterministic cluster and reconstructed by the production batch
    /// renderer into local visual agents.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Ambient Life Rendering Showcase")]
    [DisallowMultipleComponent]
    public sealed class AmbientLifeRenderingShowcase : MonoBehaviour
    {
        [SerializeField] private uint m_Seed = 0xA6B1E17Eu;
        [SerializeField] private bool m_CreateEnvironment = true;

        private readonly List<AmbientLifeCluster> _clusters = new();
        private ProceduralAmbientLifeBatchRenderer _renderer;

        public ProceduralAmbientLifeBatchRenderer Renderer => _renderer;
        public IReadOnlyList<AmbientLifeCluster> Clusters => _clusters;
        public int ClusterCount => _clusters.Count;
        public int AgentCount => _renderer != null ? _renderer.AgentCount : 0;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_renderer == null)
                _renderer = GetComponent<ProceduralAmbientLifeBatchRenderer>()
                            ?? gameObject.AddComponent<ProceduralAmbientLifeBatchRenderer>();

            if (m_CreateEnvironment)
                SubsystemRenderingShowcaseEnvironment.Ensure(transform);

            BuildClusters(m_Seed, _clusters);
            _renderer.SetClusters(_clusters);
        }

        public static void BuildClusters(uint seed, List<AmbientLifeCluster> output)
        {
            output.Clear();

            const int columns = 4;
            const float spacing = 4.0f;
            for (int i = 0; i < AmbientLifeCatalogue.Count; i++)
            {
                AmbientLifeKind kind = AmbientLifeCatalogue.KindAt(i);
                int column = i % columns;
                int row = i / columns;
                uint clusterSeed = seed + (uint)i * 0x9E3779B9u;

                output.Add(new AmbientLifeCluster
                {
                    PositionMetres = new float3(
                        (column - 1.5f) * spacing,
                        0.05f,
                        1.5f + row * spacing),
                    Kind = kind,
                    Seed = clusterSeed == 0u ? 1u : clusterSeed,
                    Count = (ushort)(8 + i % 5),
                    RadiusMetres = 1.35f,
                });
            }
        }
    }

    /// <summary>
    /// Minimal presentation shell shared by the two subsystem showcases. This is intentionally
    /// small and created only in play mode so the showcase remains cheap enough for PlayMode tests.
    /// </summary>
    internal static class SubsystemRenderingShowcaseEnvironment
    {
        public static void Ensure(Transform root)
        {
            EnsureGround(root);
            EnsureWall(root);
            EnsureCamera(root);
            EnsureLight(root);
        }

        private static void EnsureGround(Transform root)
        {
            if (root.Find("Showcase Ground") != null) return;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Showcase Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0f, -0.02f, 6f);
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.0f);
        }

        private static void EnsureWall(Transform root)
        {
            if (root.Find("Showcase Vine Wall") != null) return;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Showcase Vine Wall";
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = new Vector3(0f, 2.0f, 10f);
            wall.transform.localScale = new Vector3(18f, 4f, 0.22f);
        }

        private static void EnsureCamera(Transform root)
        {
            if (Camera.main != null || root.Find("Showcase Camera") != null) return;
            GameObject cameraObject = new GameObject("Showcase Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.79f, 0.88f, 1f);
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            cameraObject.transform.position = new Vector3(0f, 8.5f, -16f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.4f, 6f));
        }

        private static void EnsureLight(Transform root)
        {
            if (root.Find("Showcase Sun") != null) return;
            GameObject lightObject = new GameObject("Showcase Sun");
            lightObject.transform.SetParent(root, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }
    }
}
