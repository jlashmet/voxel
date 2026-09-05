using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Optional semantic importance supplied by game composition. Normal generated features need
    /// no override; important gameplay features may retain a horizon representation within their
    /// configured cap without introducing producer-specific renderer logic.
    /// </summary>
    public enum FarFeatureImportance : byte
    {
        Default = 0,
        Important = 1,
        Horizon = 2,
    }

    /// <summary>
    /// Generic projected-significance selector for baked far features. All thresholds and distance
    /// caps are configuration; the policy contains no scene coordinates, feature kinds, or names.
    /// </summary>
    public sealed class FarFeatureSelectionPolicy
    {
        public readonly struct Thresholds
        {
            public Thresholds(
                float midEnterPixels,
                float midExitPixels,
                float farEnterPixels,
                float farExitPixels,
                float horizonEnterPixels,
                float horizonExitPixels)
            {
                if (!(midEnterPixels > midExitPixels)) throw new ArgumentOutOfRangeException(nameof(midEnterPixels));
                if (!(midExitPixels > farEnterPixels)) throw new ArgumentOutOfRangeException(nameof(midExitPixels));
                if (!(farEnterPixels > farExitPixels)) throw new ArgumentOutOfRangeException(nameof(farEnterPixels));
                if (!(farExitPixels > horizonEnterPixels)) throw new ArgumentOutOfRangeException(nameof(farExitPixels));
                if (!(horizonEnterPixels > horizonExitPixels) || !(horizonExitPixels > 0f))
                    throw new ArgumentOutOfRangeException(nameof(horizonEnterPixels));

                MidEnterPixels = midEnterPixels;
                MidExitPixels = midExitPixels;
                FarEnterPixels = farEnterPixels;
                FarExitPixels = farExitPixels;
                HorizonEnterPixels = horizonEnterPixels;
                HorizonExitPixels = horizonExitPixels;
            }

            public float MidEnterPixels { get; }
            public float MidExitPixels { get; }
            public float FarEnterPixels { get; }
            public float FarExitPixels { get; }
            public float HorizonEnterPixels { get; }
            public float HorizonExitPixels { get; }
        }

        public readonly struct DistanceCaps
        {
            public DistanceCaps(float defaultMetres, float importantMetres, float horizonMetres)
            {
                if (!(defaultMetres > 0f)) throw new ArgumentOutOfRangeException(nameof(defaultMetres));
                if (!(importantMetres >= defaultMetres)) throw new ArgumentOutOfRangeException(nameof(importantMetres));
                if (!(horizonMetres >= importantMetres)) throw new ArgumentOutOfRangeException(nameof(horizonMetres));

                DefaultMetres = defaultMetres;
                ImportantMetres = importantMetres;
                HorizonMetres = horizonMetres;
            }

            public float DefaultMetres { get; }
            public float ImportantMetres { get; }
            public float HorizonMetres { get; }

            public float For(FarFeatureImportance importance)
            {
                switch (importance)
                {
                    case FarFeatureImportance.Important:
                        return ImportantMetres;
                    case FarFeatureImportance.Horizon:
                        return HorizonMetres;
                    default:
                        return DefaultMetres;
                }
            }
        }

        private readonly Thresholds _thresholds;
        private readonly DistanceCaps _distanceCaps;
        private readonly float _verticalFovDegrees;
        private readonly int _viewportHeightPixels;
        private readonly Dictionary<ulong, FarFeatureTier> _previous = new();

        public FarFeatureSelectionPolicy(
            Thresholds thresholds,
            DistanceCaps distanceCaps,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            ValidateProjection(verticalFovDegrees, viewportHeightPixels);
            _thresholds = thresholds;
            _distanceCaps = distanceCaps;
            _verticalFovDegrees = verticalFovDegrees;
            _viewportHeightPixels = viewportHeightPixels;
        }

        public FarFeatureTier Select(
            ulong stableId,
            float3 boundsCenter,
            float3 boundsExtents,
            float3 cameraPosition,
            FarFeatureImportance importance = FarFeatureImportance.Default)
        {
            float distance = math.max(0.1f, math.distance(cameraPosition, boundsCenter));
            if (distance > _distanceCaps.For(importance))
            {
                _previous.Remove(stableId);
                return FarFeatureTier.Culled;
            }

            float pixels = ProjectedPixels(
                boundsExtents,
                distance,
                _verticalFovDegrees,
                _viewportHeightPixels);
            FarFeatureTier previous = _previous.TryGetValue(stableId, out FarFeatureTier value)
                ? value
                : FarFeatureTier.Culled;
            FarFeatureTier selected = SelectWithHysteresis(
                pixels,
                importance != FarFeatureImportance.Default,
                previous);

            if (selected == FarFeatureTier.Culled && importance != FarFeatureImportance.Default)
                selected = FarFeatureTier.Horizon;

            if (selected == FarFeatureTier.Culled)
                _previous.Remove(stableId);
            else
                _previous[stableId] = selected;
            return selected;
        }

        public void Forget(ulong stableId) => _previous.Remove(stableId);
        public void ClearHistory() => _previous.Clear();

        public static float ProjectedPixels(
            float3 boundsExtents,
            float distanceMetres,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            ValidateProjection(verticalFovDegrees, viewportHeightPixels);
            float diameter = 2f * math.cmax(math.max(boundsExtents, float3.zero));
            float focalPixels = viewportHeightPixels * 0.5f /
                                math.tan(math.radians(verticalFovDegrees) * 0.5f);
            return diameter / math.max(0.1f, distanceMetres) * focalPixels;
        }

        private FarFeatureTier SelectWithHysteresis(
            float pixels,
            bool horizonAllowed,
            FarFeatureTier previous)
        {
            switch (previous)
            {
                case FarFeatureTier.Mid:
                    if (pixels >= _thresholds.MidExitPixels) return FarFeatureTier.Mid;
                    break;
                case FarFeatureTier.Far:
                    if (pixels >= _thresholds.MidEnterPixels) return FarFeatureTier.Mid;
                    if (pixels >= _thresholds.FarExitPixels) return FarFeatureTier.Far;
                    break;
                case FarFeatureTier.Horizon:
                    if (pixels >= _thresholds.MidEnterPixels) return FarFeatureTier.Mid;
                    if (pixels >= _thresholds.FarEnterPixels) return FarFeatureTier.Far;
                    if (horizonAllowed && pixels >= _thresholds.HorizonExitPixels)
                        return FarFeatureTier.Horizon;
                    break;
            }

            if (pixels >= _thresholds.MidEnterPixels) return FarFeatureTier.Mid;
            if (pixels >= _thresholds.FarEnterPixels) return FarFeatureTier.Far;
            if (horizonAllowed && pixels >= _thresholds.HorizonEnterPixels)
                return FarFeatureTier.Horizon;
            return FarFeatureTier.Culled;
        }

        private static void ValidateProjection(float verticalFovDegrees, int viewportHeightPixels)
        {
            if (!(verticalFovDegrees > 1f && verticalFovDegrees < 179f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));
        }
    }

    /// <summary>
    /// Bridges sparse derived feature bakes to the engine render contract without knowing producer
    /// categories. The source remains metadata-only; no voxel region generation/residency is needed.
    /// </summary>
    public sealed class FarFeaturePresentationAdapter
    {
        private readonly IFeaturePresentationSource _source;
        private readonly FarFeatureSelectionPolicy _selection;
        private readonly float _voxelSizeMetres;
        private readonly Func<FeaturePresentationBake, FarFeatureImportance> _importance;
        private readonly List<FarFeatureInstance> _instances = new();

        public FarFeaturePresentationAdapter(
            IFeaturePresentationSource source,
            FarFeatureSelectionPolicy selection,
            float voxelSizeMetres,
            Func<FeaturePresentationBake, FarFeatureImportance> importance = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (!(voxelSizeMetres > 0f) || !math.isfinite(voxelSizeMetres))
                throw new ArgumentOutOfRangeException(nameof(voxelSizeMetres));
            _voxelSizeMetres = voxelSizeMetres;
            _importance = importance;
        }

        public IReadOnlyList<FarFeatureInstance> Query(float3 cameraPosition, float radiusMetres)
        {
            if (!(radiusMetres > 0f) || !math.isfinite(radiusMetres))
                throw new ArgumentOutOfRangeException(nameof(radiusMetres));

            FeaturePresentationBounds queryBounds = BuildQueryBounds(cameraPosition, radiusMetres);
            IReadOnlyList<FeaturePresentationBake> bakes = _source.Query(queryBounds);
            _instances.Clear();
            if (_instances.Capacity < bakes.Count) _instances.Capacity = bakes.Count;

            for (int i = 0; i < bakes.Count; i++)
            {
                FeaturePresentationBake bake = bakes[i];
                BoundsFor(bake, out float3 position, out float3 center, out float3 extents, out float3 scale);
                FarFeatureImportance importance = _importance?.Invoke(bake) ?? FarFeatureImportance.Default;
                FarFeatureTier tier = _selection.Select(
                    bake.SourceId,
                    center,
                    extents,
                    cameraPosition,
                    importance);
                if (tier == FarFeatureTier.Culled) continue;

                _instances.Add(new FarFeatureInstance(
                    bake.SourceId,
                    position,
                    quaternion.identity,
                    scale,
                    center,
                    extents,
                    GeometryKeyFor(bake),
                    StyleKeyFor(bake),
                    tier,
                    FlagsFor(importance),
                    GeometryFor(bake),
                    MaterialFor(bake)));
            }

            return _instances;
        }

        private FeaturePresentationBounds BuildQueryBounds(float3 cameraPosition, float radiusMetres)
        {
            float inverseVoxelSize = 1f / _voxelSizeMetres;
            float3 radius = new float3(radiusMetres);
            int3 min = (int3)math.floor((cameraPosition - radius) * inverseVoxelSize);
            int3 max = (int3)math.ceil((cameraPosition + radius) * inverseVoxelSize) + new int3(1);
            return new FeaturePresentationBounds(min, max);
        }

        private void BoundsFor(
            FeaturePresentationBake bake,
            out float3 position,
            out float3 center,
            out float3 extents,
            out float3 scale)
        {
            int3 minVoxel = bake.BoundsMin;
            int3 maxVoxelExclusive = bake.BoundsMax + new int3(1);
            float3 min = new float3(minVoxel.x, minVoxel.y, minVoxel.z) * _voxelSizeMetres;
            float3 maxExclusive = new float3(
                maxVoxelExclusive.x,
                maxVoxelExclusive.y,
                maxVoxelExclusive.z) * _voxelSizeMetres;
            scale = math.max(maxExclusive - min, new float3(_voxelSizeMetres));
            extents = scale * 0.5f;
            center = min + extents;
            position = new float3(center.x, min.y, center.z);
        }

        private static FarFeatureGeometry GeometryFor(FeaturePresentationBake bake)
        {
            var primitives = new List<FarFeatureGeometryPrimitive>(bake.PrimitiveCount);
            float3 bakeMin = new float3(bake.BoundsMin.x, bake.BoundsMin.y, bake.BoundsMin.z);
            int3 maxExclusiveVoxel = bake.BoundsMax + new int3(1);
            float3 bakeSize = math.max(
                new float3(
                    maxExclusiveVoxel.x - bake.BoundsMin.x,
                    maxExclusiveVoxel.y - bake.BoundsMin.y,
                    maxExclusiveVoxel.z - bake.BoundsMin.z),
                new float3(1f));
            var originOffset = new float3(0.5f, 0f, 0.5f);

            for (int i = 0; i < bake.PrimitiveCount; i++)
            {
                Primitive primitive = bake.GetPrimitive(i);
                if (primitive.Mode != PrimitiveMode.Fill && primitive.Mode != PrimitiveMode.FillIfEmpty)
                    continue;

                primitive.Bounds(out int3 primitiveMinVoxel, out int3 primitiveMaxVoxel);
                int3 primitiveMaxExclusiveVoxel = primitiveMaxVoxel + new int3(1);
                float3 primitiveMin = new float3(
                    primitiveMinVoxel.x,
                    primitiveMinVoxel.y,
                    primitiveMinVoxel.z);
                float3 primitiveMaxExclusive = new float3(
                    primitiveMaxExclusiveVoxel.x,
                    primitiveMaxExclusiveVoxel.y,
                    primitiveMaxExclusiveVoxel.z);
                float3 normalizedMin = (primitiveMin - bakeMin) / bakeSize - originOffset;
                float3 normalizedMax = (primitiveMaxExclusive - bakeMin) / bakeSize - originOffset;
                ProfileFor(in primitive, out float startRadiusScale, out float endRadiusScale);
                primitives.Add(new FarFeatureGeometryPrimitive(
                    (FarFeatureGeometryShape)(byte)primitive.Shape,
                    normalizedMin,
                    normalizedMax,
                    primitive.Axis,
                    startRadiusScale,
                    endRadiusScale));
            }

            return primitives.Count == 0 ? null : new FarFeatureGeometry(primitives.ToArray());
        }

        private static void ProfileFor(
            in Primitive primitive,
            out float startRadiusScale,
            out float endRadiusScale)
        {
            startRadiusScale = 1f;
            endRadiusScale = 1f;
            if (primitive.Shape != PrimitiveShape.Frustum) return;

            int maxRadius = math.max(primitive.Radius, primitive.InnerRadius);
            if (maxRadius <= 0) return;

            int startRadius = primitive.Direction < 0 ? primitive.InnerRadius : primitive.Radius;
            int endRadius = primitive.Direction < 0 ? primitive.Radius : primitive.InnerRadius;
            startRadiusScale = math.max(0f, startRadius / (float)maxRadius);
            endRadiusScale = math.max(0f, endRadius / (float)maxRadius);
        }

        private static string GeometryKeyFor(FeaturePresentationBake bake) =>
            $"bake-{bake.Revision:X16}";

        private static string StyleKeyFor(FeaturePresentationBake bake)
        {
            for (int i = 0; i < bake.PrimitiveCount; i++)
            {
                Primitive primitive = bake.GetPrimitive(i);
                if (primitive.Mode == PrimitiveMode.Carve) continue;
                return $"m{primitive.Material:X2}-s{primitive.SurfaceStyle:X4}-c{primitive.Coating:X2}";
            }
            return "empty";
        }

        private static byte MaterialFor(FeaturePresentationBake bake)
        {
            for (int i = 0; i < bake.PrimitiveCount; i++)
            {
                Primitive primitive = bake.GetPrimitive(i);
                if (primitive.Mode == PrimitiveMode.Fill || primitive.Mode == PrimitiveMode.FillIfEmpty)
                    return primitive.Material;
            }
            return 0;
        }

        private static FarFeatureVisualFlags FlagsFor(FarFeatureImportance importance)
        {
            switch (importance)
            {
                case FarFeatureImportance.Horizon:
                    return FarFeatureVisualFlags.Landmark | FarFeatureVisualFlags.HorizonLandmark;
                case FarFeatureImportance.Important:
                    return FarFeatureVisualFlags.Landmark;
                default:
                    return FarFeatureVisualFlags.None;
            }
        }
    }
}