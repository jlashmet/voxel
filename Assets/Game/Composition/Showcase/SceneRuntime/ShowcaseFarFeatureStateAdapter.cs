using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase composition boundary that joins renderer-neutral far-feature instances with
    /// authoritative coarse semantic structure state. WorldBuilder does not depend on Rendering;
    /// Rendering does not depend on WorldBuilder.
    /// </summary>
    public sealed class ShowcaseFarFeatureStateAdapter
    {
        private readonly FarFeaturePresentationAdapter _presentation;
        private readonly IStructureVisualStateSource _states;
        private readonly List<FarFeatureInstance> _instances = new();

        public ShowcaseFarFeatureStateAdapter(
            FarFeaturePresentationAdapter presentation,
            IStructureVisualStateSource states)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _states = states ?? throw new ArgumentNullException(nameof(states));
        }

        public IReadOnlyList<FarFeatureInstance> Query(float3 cameraPosition, float radiusMetres)
        {
            return Apply(_presentation.Query(cameraPosition, radiusMetres));
        }

        /// <summary>
        /// Applies authoritative semantic state to already-selected render instances. Keeping this
        /// operation independent of voxel residency makes removal/ruin survive detailed-region unload.
        /// </summary>
        public IReadOnlyList<FarFeatureInstance> Apply(IReadOnlyList<FarFeatureInstance> selected)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));

            _instances.Clear();
            if (_instances.Capacity < selected.Count) _instances.Capacity = selected.Count;

            for (int i = 0; i < selected.Count; i++)
            {
                FarFeatureInstance instance = selected[i];
                StructureVisualState state = _states.Get(instance.StableId);
                if (state == StructureVisualState.Removed)
                    continue;

                FarFeatureVisualFlags flags = instance.Flags;
                if (state == StructureVisualState.Ruined)
                    flags |= FarFeatureVisualFlags.Ruined;

                _instances.Add(new FarFeatureInstance(
                    instance.StableId,
                    instance.Position,
                    instance.Rotation,
                    instance.Scale,
                    instance.BoundsCenter,
                    instance.BoundsExtents,
                    instance.GeometryKey,
                    instance.StyleKey,
                    instance.Tier,
                    flags,
                    instance.Geometry,
                    instance.Presentation));
            }

            return _instances;
        }
    }
}
