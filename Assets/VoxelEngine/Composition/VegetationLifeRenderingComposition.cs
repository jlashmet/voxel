using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition-root bridge for lightweight vegetation and ambient-life renderers. Scene code
    /// receives only Rendering.Api contracts; concrete Rendering.Runtime types stay confined here.
    /// </summary>
    public static class VegetationLifeRenderingComposition
    {
        public static IVegetationBatchRenderer EnsureVegetationBatchRenderer(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            ProceduralVegetationBatchRenderer renderer =
                host.GetComponent<ProceduralVegetationBatchRenderer>()
                ?? host.AddComponent<ProceduralVegetationBatchRenderer>();
            return new VegetationRendererHandle(renderer);
        }

        public static IAmbientLifeBatchRenderer EnsureAmbientLifeBatchRenderer(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            ProceduralAmbientLifeBatchRenderer renderer =
                host.GetComponent<ProceduralAmbientLifeBatchRenderer>()
                ?? host.AddComponent<ProceduralAmbientLifeBatchRenderer>();
            return new AmbientLifeRendererHandle(renderer);
        }

        private sealed class VegetationRendererHandle : IVegetationBatchRenderer
        {
            private readonly ProceduralVegetationBatchRenderer _renderer;

            public VegetationRendererHandle(ProceduralVegetationBatchRenderer renderer)
            {
                _renderer = renderer;
            }

            public int InstanceCount => _renderer.InstanceCount;
            public bool enabled { get => _renderer.enabled; set => _renderer.enabled = value; }
            public void SetInstances(IReadOnlyList<VegetationInstance> instances) => _renderer.SetInstances(instances);
            public void Clear() => _renderer.Clear();
            public void DrawNow() => _renderer.DrawNow();
        }

        private sealed class AmbientLifeRendererHandle : IAmbientLifeBatchRenderer
        {
            private readonly ProceduralAmbientLifeBatchRenderer _renderer;

            public AmbientLifeRendererHandle(ProceduralAmbientLifeBatchRenderer renderer)
            {
                _renderer = renderer;
            }

            public int AgentCount => _renderer.AgentCount;
            public bool enabled { get => _renderer.enabled; set => _renderer.enabled = value; }
            public void SetClusters(IReadOnlyList<AmbientLifeCluster> clusters) => _renderer.SetClusters(clusters);
            public void Clear() => _renderer.Clear();
            public void DrawNow() => _renderer.DrawNow();
        }
    }
}
