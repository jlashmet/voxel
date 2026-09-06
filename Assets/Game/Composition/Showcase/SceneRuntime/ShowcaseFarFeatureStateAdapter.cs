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

        public IReadOnlyList<FarFeatureInstance> Query(
            float3 cameraPosition,
            float radiusMetres,
            float nearSurfaceRadiusMetres = 0f)
        {
            return Apply(
                _presentation.Query(cameraPosition, radiusMetres),
                cameraPosition,
                nearSurfaceRadiusMetres);
        }

        /// <summary>
        /// Applies authoritative semantic state to already-selected render instances. Keeping this
        /// operation independent of voxel residency makes removal/ruin survive detailed-region unload.
        /// The optional near-surface radius comes from the same published-coverage handoff used by
        /// far terrain. Semantic proxies are whole-feature conservative masses and cannot clip against
        /// the detailed surface, so they retire as soon as their horizontal bounds overlap published
        /// near coverage; otherwise a landmark spanning the handoff boundary is drawn on top of its
        /// detailed representation as one large duplicate mass.
        /// </summary>
        public IReadOnlyList<FarFeatureInstance> Apply(
            IReadOnlyList<FarFeatureInstance> selected,
            float3 nearSurfaceCentre = default,
            float nearSurfaceRadiusMetres = 0f)
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
                if (OverlapsPublishedNearSurface(instance, nearSurfaceCentre, nearSurfaceRadiusMetres))
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
                    instance.MaterialIndex));
            }

            return _instances;
        }

        private static bool OverlapsPublishedNearSurface(
            FarFeatureInstance instance,
            float3 nearSurfaceCentre,
            float nearSurfaceRadiusMetres)
        {
            if (!(nearSurfaceRadiusMetres > 0f) || !math.isfinite(nearSurfaceRadiusMetres))
                return false;

            float2 offset = math.abs(new float2(
                instance.BoundsCenter.x - nearSurfaceCentre.x,
                instance.BoundsCenter.z - nearSurfaceCentre.z));
            float2 extents = math.max(
                new float2(instance.BoundsExtents.x, instance.BoundsExtents.z),
                float2.zero);
            float2 distanceToBounds = math.max(offset - extents, float2.zero);
            return math.lengthsq(distanceToBounds) <= nearSurfaceRadiusMetres * nearSurfaceRadiusMetres;
        }
    }
}
