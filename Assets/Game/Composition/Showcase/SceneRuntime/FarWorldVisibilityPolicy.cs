using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-composition policy for semantic far-structure visibility. Thresholds are screen-space
    /// pixels and remain configurable here rather than becoming renderer constants.
    /// </summary>
    public sealed class FarWorldVisibilityPolicy
    {
        private const float DmToMetres = 0.1f;

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

        private readonly Thresholds _thresholds;
        private readonly float _verticalFovDegrees;
        private readonly int _viewportHeightPixels;
        private readonly Dictionary<ulong, FarStructureTier> _previous =
            new Dictionary<ulong, FarStructureTier>();

        public FarWorldVisibilityPolicy(
            Thresholds thresholds,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            if (!(verticalFovDegrees > 1f && verticalFovDegrees < 179f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));

            _thresholds = thresholds;
            _verticalFovDegrees = verticalFovDegrees;
            _viewportHeightPixels = viewportHeightPixels;
        }

        public FarStructureTier Select(StructureFarPresentation record, float2 cameraXZMetres)
        {
            float pixels = ProjectedPixels(record, cameraXZMetres, _verticalFovDegrees, _viewportHeightPixels);
            bool horizonAllowed = record.VisibilityClass != StructureVisibilityClass.OrdinaryStructure;
            FarStructureTier previous = _previous.TryGetValue(record.StructureKey, out FarStructureTier value)
                ? value
                : FarStructureTier.Culled;

            FarStructureTier selected = SelectWithHysteresis(pixels, horizonAllowed, previous);
            if (selected == FarStructureTier.Culled)
                _previous.Remove(record.StructureKey);
            else
                _previous[record.StructureKey] = selected;
            return selected;
        }

        public void Forget(ulong structureKey)
        {
            _previous.Remove(structureKey);
        }

        public void ClearHistory()
        {
            _previous.Clear();
        }

        public static float ProjectedPixels(
            StructureFarPresentation record,
            float2 cameraXZMetres,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            if (!(verticalFovDegrees > 1f && verticalFovDegrees < 179f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));

            float minX = record.FootprintMinDm.X * DmToMetres;
            float minZ = record.FootprintMinDm.Y * DmToMetres;
            float maxX = record.FootprintMaxDm.X * DmToMetres;
            float maxZ = record.FootprintMaxDm.Y * DmToMetres;
            float2 center = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float distance = math.max(0.1f, math.distance(cameraXZMetres, center));
            float width = maxX - minX;
            float depth = maxZ - minZ;
            float height = record.HeightDm * DmToMetres;
            float diameter = math.max(height, math.max(width, depth));
            float focalPixels = viewportHeightPixels * 0.5f /
                                math.tan(math.radians(verticalFovDegrees) * 0.5f);
            return diameter / distance * focalPixels;
        }

        private FarStructureTier SelectWithHysteresis(
            float pixels,
            bool horizonAllowed,
            FarStructureTier previous)
        {
            switch (previous)
            {
                case FarStructureTier.Mid:
                    if (pixels >= _thresholds.MidExitPixels) return FarStructureTier.Mid;
                    break;
                case FarStructureTier.Far:
                    if (pixels >= _thresholds.MidEnterPixels) return FarStructureTier.Mid;
                    if (pixels >= _thresholds.FarExitPixels) return FarStructureTier.Far;
                    break;
                case FarStructureTier.Horizon:
                    if (pixels >= _thresholds.MidEnterPixels) return FarStructureTier.Mid;
                    if (pixels >= _thresholds.FarEnterPixels) return FarStructureTier.Far;
                    if (horizonAllowed && pixels >= _thresholds.HorizonExitPixels)
                        return FarStructureTier.Horizon;
                    break;
            }

            if (pixels >= _thresholds.MidEnterPixels) return FarStructureTier.Mid;
            if (pixels >= _thresholds.FarEnterPixels) return FarStructureTier.Far;
            if (horizonAllowed && pixels >= _thresholds.HorizonEnterPixels)
                return FarStructureTier.Horizon;
            return FarStructureTier.Culled;
        }
    }
}
