using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    [AddComponentMenu("VoxelEngine/Showcases/Vegetation Rendering Showcase")]
    [DisallowMultipleComponent]
    public sealed class VegetationRenderingShowcase : MonoBehaviour
    {
        public const int InstancesPerKind = 3;

        [SerializeField] private uint m_Seed = 0x71E6A710u;
        [SerializeField] private bool m_CreateEnvironment = true;

        private readonly List<VegetationInstance> _instances = new();
        private IVegetationBatchRenderer _renderer;

        public IVegetationBatchRenderer Renderer => _renderer;
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
                _renderer = VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);

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
                    uint instanceSeed = seed + (uint)i * 0x9E3779B9u + (uint)sample * 0x85EBCA6Bu;
                    float sampleOffset = (sample - 1) * 0.34f;
                    float3 normal = new float3(0f, 1f, 0f);
                    float3 position;

                    if (profile.GrowthForm == VegetationGrowthForm.Climber
                        || profile.GrowthForm == VegetationGrowthForm.Hanger)
                    {
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
}
