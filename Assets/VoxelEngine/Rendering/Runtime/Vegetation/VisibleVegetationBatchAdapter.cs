using System;
using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Reusable bridge from deterministic visibility-query results to the existing instanced
    /// vegetation renderer. It owns presentation scratch only; placement/sector membership stays
    /// authoritative in <see cref="VegetationVisibility"/> and its source instances.
    /// </summary>
    public sealed class VisibleVegetationBatchAdapter
    {
        private readonly List<VegetationInstance> _visible = new List<VegetationInstance>();

        public int VisibleCount => _visible.Count;
        public IReadOnlyList<VegetationInstance> VisibleInstances => _visible;

        public void Apply(
            ProceduralVegetationBatchRenderer renderer,
            IReadOnlyList<VegetationVisibilityEntry> visibleEntries)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            _visible.Clear();
            if (visibleEntries != null)
            {
                if (_visible.Capacity < visibleEntries.Count)
                    _visible.Capacity = visibleEntries.Count;
                for (int i = 0; i < visibleEntries.Count; i++)
                    _visible.Add(visibleEntries[i].Instance);
            }

            renderer.SetInstances(_visible);
        }

        public void Clear(ProceduralVegetationBatchRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            _visible.Clear();
            renderer.Clear();
        }
    }
}
