using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.AmbientLife;
using VoxelEngine.Rendering.AmbientLife;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Lightweight standalone showcase for ambient-life rendering. Every semantic ambient-life
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
            const float spacing = 4.2f;
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
                    Count = (ushort)(6 + i % 3),
                    RadiusMetres = 1.65f,
                });
            }
        }
    }
}
