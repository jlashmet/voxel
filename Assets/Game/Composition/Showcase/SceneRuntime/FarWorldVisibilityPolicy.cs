using System;
using System.Collections.Generic;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-composition policy for semantic far-structure visibility. Screen-space thresholds and
    /// semantic distance caps are configuration here rather than renderer constants.
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

        public readonly struct DistanceCaps
        {
            public DistanceCaps(
                float ordinaryMetres,
                float settlementAnchorMetres,
                float landmarkMetres,
                float horizonLandmarkMetres)
            {
                if (!(ordinaryMetres > 0f)) throw new ArgumentOutOfRangeException(nameof(ordinaryMetres));
                if (!(settlementAnchorMetres >= ordinaryMetres)) throw new ArgumentOutOfRangeException(nameof(settlementAnchorMetres));
                if (!(landmarkMetres >= settlementAnchorMetres)) throw new ArgumentOutOfRangeException(nameof(landmarkMetres));
                if (!(horizonLandmarkMetres >= landmarkMetres)) throw new ArgumentOutOfRangeException(nameof(horizonLandmarkMetres));

                OrdinaryMetres = ordinaryMetres;
                SettlementAnchorMetres = settlementAnchorMetres;
                LandmarkMetres = landmarkMetres;
                HorizonLandmarkMetres = horizonLandmarkMetres;
            }

            public float OrdinaryMetres { get; }
            public float SettlementAnchorMetres { get; }
            public float LandmarkMetres { get; }
            public float HorizonLandmarkMetres { get; }

            public float For(StructureVisibilityClass visibilityClass)
            {
                switch (visibilityClass)
                {
                    case StructureVisibilityClass.SettlementAnchor:
                        return SettlementAnchorMetres;
                    case StructureVisibilityClass.Landmark:
                        return LandmarkMetres;
                    case StructureVisibilityClass.HorizonLandmark:
                        return HorizonLandmarkMetres;
                    default:
                        return OrdinaryMetres;
                }
            }
        }

        private readonly Thresholds _thresholds;
        private readonly DistanceCaps _distanceCaps;
        private readonly float _verticalFovDegrees;
        private readonly int _viewportHeightPixels;
        private readonly Dictionary<ulong, FarStructureTier> _previous =
            new Dictionary<ulong, FarStructureTier>();
        private readonly Dictionary<ulong, FarStructureTier> _clusterPrevious =
            new Dictionary<ulong, FarStructureTier>();

        public FarWorldVisibilityPolicy(
            Thresholds thresholds,
            DistanceCaps distanceCaps,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            if (!(verticalFovDegrees > 1f && verticalFovDegrees < 179f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));

            _thresholds = thresholds;
            _distanceCaps = distanceCaps;
            _verticalFovDegrees = verticalFovDegrees;
            _viewportHeightPixels = viewportHeightPixels;
        }

        public FarStructureTier Select(StructureFarPresentation record, float2 cameraXZMetres)
        {
            float distance = DistanceMetres(record, cameraXZMetres);
            if (distance > _distanceCaps.For(record.VisibilityClass))
            {
                _previous.Remove(record.StructureKey);
                return FarStructureTier.Culled;
            }

            float pixels = ProjectedPixels(record, distance, _verticalFovDegrees, _viewportHeightPixels);
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

        /// <summary>
        /// Settlement clusters are a far-only representation. They activate only after the member
        /// representation drops below the mid exit threshold, remain active until the mid enter
        /// threshold on approach, and use the settlement-anchor distance cap.
        /// </summary>
        public FarStructureTier SelectCluster(
            WorldVisibilityClusterBuilder.Cluster cluster,
            float2 cameraXZMetres)
        {
            float distance = DistanceMetres(cluster, cameraXZMetres);
            if (distance > _distanceCaps.SettlementAnchorMetres)
            {
                _clusterPrevious.Remove(cluster.ClusterKey);
                return FarStructureTier.Culled;
            }

            float pixels = ProjectedPixels(cluster, distance, _verticalFovDegrees, _viewportHeightPixels);
            FarStructureTier previous = _clusterPrevious.TryGetValue(
                cluster.ClusterKey, out FarStructureTier value)
                ? value
                : FarStructureTier.Culled;
            FarStructureTier selected = SelectClusterWithHysteresis(pixels, previous);
            if (selected == FarStructureTier.Culled)
                _clusterPrevious.Remove(cluster.ClusterKey);
            else
                _clusterPrevious[cluster.ClusterKey] = selected;
            return selected;
        }

        public void Forget(ulong structureKey)
        {
            _previous.Remove(structureKey);
        }

        public void ForgetCluster(ulong clusterKey)
        {
            _clusterPrevious.Remove(clusterKey);
        }

        public void ClearHistory()
        {
            _previous.Clear();
            _clusterPrevious.Clear();
        }

        public static float ProjectedPixels(
            StructureFarPresentation record,
            float2 cameraXZMetres,
            float verticalFovDegrees,
            int viewportHeightPixels) =>
            ProjectedPixels(
                record,
                DistanceMetres(record, cameraXZMetres),
                verticalFovDegrees,
                viewportHeightPixels);

        public static float ProjectedPixels(
            WorldVisibilityClusterBuilder.Cluster cluster,
            float2 cameraXZMetres,
            float verticalFovDegrees,
            int viewportHeightPixels) =>
            ProjectedPixels(
                cluster,
                DistanceMetres(cluster, cameraXZMetres),
                verticalFovDegrees,
                viewportHeightPixels);

        private static float ProjectedPixels(
            StructureFarPresentation record,
            float distanceMetres,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            ValidateProjection(verticalFovDegrees, viewportHeightPixels);
            float width = (record.FootprintMaxDm.X - record.FootprintMinDm.X) * DmToMetres;
            float depth = (record.FootprintMaxDm.Y - record.FootprintMinDm.Y) * DmToMetres;
            float height = record.HeightDm * DmToMetres;
            return ProjectedPixels(width, depth, height, distanceMetres, verticalFovDegrees, viewportHeightPixels);
        }

        private static float ProjectedPixels(
            WorldVisibilityClusterBuilder.Cluster cluster,
            float distanceMetres,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            ValidateProjection(verticalFovDegrees, viewportHeightPixels);
            float width = (cluster.FootprintMaxDm.X - cluster.FootprintMinDm.X) * DmToMetres;
            float depth = (cluster.FootprintMaxDm.Y - cluster.FootprintMinDm.Y) * DmToMetres;
            float height = cluster.MaxHeightDm * DmToMetres;
            return ProjectedPixels(width, depth, height, distanceMetres, verticalFovDegrees, viewportHeightPixels);
        }

        private static float ProjectedPixels(
            float width,
            float depth,
            float height,
            float distanceMetres,
            float verticalFovDegrees,
            int viewportHeightPixels)
        {
            float diameter = math.max(height, math.max(width, depth));
            float focalPixels = viewportHeightPixels * 0.5f /
                                math.tan(math.radians(verticalFovDegrees) * 0.5f);
            return diameter / math.max(0.1f, distanceMetres) * focalPixels;
        }

        private static float DistanceMetres(
            StructureFarPresentation record,
            float2 cameraXZMetres)
        {
            float minX = record.FootprintMinDm.X * DmToMetres;
            float minZ = record.FootprintMinDm.Y * DmToMetres;
            float maxX = record.FootprintMaxDm.X * DmToMetres;
            float maxZ = record.FootprintMaxDm.Y * DmToMetres;
            float2 center = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            return math.max(0.1f, math.distance(cameraXZMetres, center));
        }

        private static float DistanceMetres(
            WorldVisibilityClusterBuilder.Cluster cluster,
            float2 cameraXZMetres)
        {
            float minX = cluster.FootprintMinDm.X * DmToMetres;
            float minZ = cluster.FootprintMinDm.Y * DmToMetres;
            float maxX = cluster.FootprintMaxDm.X * DmToMetres;
            float maxZ = cluster.FootprintMaxDm.Y * DmToMetres;
            float2 center = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            return math.max(0.1f, math.distance(cameraXZMetres, center));
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

        private FarStructureTier SelectClusterWithHysteresis(
            float pixels,
            FarStructureTier previous)
        {
            switch (previous)
            {
                case FarStructureTier.Far:
                    if (pixels >= _thresholds.MidEnterPixels) return FarStructureTier.Culled;
                    if (pixels >= _thresholds.FarExitPixels) return FarStructureTier.Far;
                    if (pixels >= _thresholds.HorizonExitPixels) return FarStructureTier.Horizon;
                    return FarStructureTier.Culled;
                case FarStructureTier.Horizon:
                    if (pixels >= _thresholds.MidEnterPixels) return FarStructureTier.Culled;
                    if (pixels >= _thresholds.FarEnterPixels) return FarStructureTier.Far;
                    if (pixels >= _thresholds.HorizonExitPixels) return FarStructureTier.Horizon;
                    return FarStructureTier.Culled;
                default:
                    if (pixels >= _thresholds.MidExitPixels) return FarStructureTier.Culled;
                    if (pixels >= _thresholds.FarEnterPixels) return FarStructureTier.Far;
                    if (pixels >= _thresholds.HorizonEnterPixels) return FarStructureTier.Horizon;
                    return FarStructureTier.Culled;
            }
        }

        private static void ValidateProjection(float verticalFovDegrees, int viewportHeightPixels)
        {
            if (!(verticalFovDegrees > 1f && verticalFovDegrees < 179f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));
        }
    }
}
