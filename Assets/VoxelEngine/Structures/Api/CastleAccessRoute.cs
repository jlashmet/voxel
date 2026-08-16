using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Derived walkable approach from the primary gate to the physical keep entrance. The route is
    /// pure planning geometry: Runtime never pathfinds or chooses access during voxel realization.
    /// </summary>
    public readonly struct CastleAccessRoute
    {
        public const float CorridorHalfWidth = 18f;

        private readonly int2 _primaryGate;
        private readonly int2 _innerGate;
        private readonly int2 _keepEntrance;
        private readonly bool _hasInnerGate;

        private CastleAccessRoute(
            int2 primaryGate,
            bool hasInnerGate,
            int2 innerGate,
            int2 keepEntrance)
        {
            _primaryGate = primaryGate;
            _hasInnerGate = hasInnerGate;
            _innerGate = innerGate;
            _keepEntrance = keepEntrance;
        }

        public int WaypointCount => _hasInnerGate ? 3 : 2;
        public int2 PrimaryGateCentre => _primaryGate;
        public int2 KeepEntranceCentre => _keepEntrance;

        public int2 Waypoint(int index)
        {
            if (index == 0) return _primaryGate;
            if (_hasInnerGate && index == 1) return _innerGate;
            if (index == WaypointCount - 1) return _keepEntrance;
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public static CastleAccessRoute Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle access route requires a terrain-resolved keep placement.");
            }

            CastleGatePlacementSpec primary = spatial.PrimaryGate;
            CastleGatePlacementSpec inner = spatial.InnerGate;
            return Create(
                in plan,
                in primary,
                spatial.HasInnerGate,
                in inner,
                spatial.KeepCentre);
        }

        internal static CastleAccessRoute Create(
            in CastlePlan plan,
            in CastleGatePlacementSpec primaryGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre) =>
            new CastleAccessRoute(
                primaryGate.Centre,
                hasInnerGate,
                innerGate.Centre,
                KeepEntrance(in plan, keepCentre));

        /// <summary>
        /// The current keep recipe cuts its main arch in the centre of the keep's local -Z face.
        /// Keeping this compatibility fact here prevents planning/presentation from guessing it.
        /// </summary>
        public static int2 KeepEntrance(in CastlePlan plan, int2 keepCentre) =>
            new int2(keepCentre.x, keepCentre.y - plan.KeepHalfZ);

        /// <summary>True when a circular obstacle leaves the full access corridor unobstructed.</summary>
        public bool ClearsPoint(int2 point, float obstacleRadius)
        {
            float clearance = CorridorHalfWidth + math.max(0f, obstacleRadius);
            float clearanceSquared = clearance * clearance;
            for (int segment = 0; segment < WaypointCount - 1; segment++)
            {
                if (DistanceSquaredToSegment(point, Waypoint(segment), Waypoint(segment + 1))
                    <= clearanceSquared)
                    return false;
            }
            return true;
        }

        /// <summary>True when an oriented courtyard building does not overlap the access corridor.</summary>
        public bool ClearsBuilding(in CastleCourtyardBuildingSpec building)
        {
            if (building.Width <= 0 || building.Depth <= 0)
                return true;

            float halfAlong = building.Width * 0.5f + CorridorHalfWidth;
            float halfInward = building.Depth * 0.5f + CorridorHalfWidth;
            for (int segment = 0; segment < WaypointCount - 1; segment++)
            {
                float2 a = BuildingLocal(Waypoint(segment), in building);
                float2 b = BuildingLocal(Waypoint(segment + 1), in building);
                if (SegmentIntersectsBox(a, b, halfAlong, halfInward))
                    return false;
            }
            return true;
        }

        private static float2 BuildingLocal(
            int2 point,
            in CastleCourtyardBuildingSpec building)
        {
            float2 delta = new float2(
                point.x - building.Centre.x,
                point.y - building.Centre.y);
            return new float2(
                math.dot(delta, building.Tangent),
                math.dot(delta, building.Inward));
        }

        private static float DistanceSquaredToSegment(int2 point, int2 a, int2 b)
        {
            float2 start = new float2(a.x, a.y);
            float2 end = new float2(b.x, b.y);
            float2 delta = end - start;
            float lengthSquared = math.lengthsq(delta);
            if (lengthSquared < 0.0001f)
                return math.lengthsq(new float2(point.x, point.y) - start);

            float2 toPoint = new float2(point.x, point.y) - start;
            float t = math.saturate(math.dot(toPoint, delta) / lengthSquared);
            return math.lengthsq(toPoint - delta * t);
        }

        private static bool SegmentIntersectsBox(
            float2 a,
            float2 b,
            float halfX,
            float halfY)
        {
            float2 delta = b - a;
            float minT = 0f;
            float maxT = 1f;
            return ClipAxis(a.x, delta.x, halfX, ref minT, ref maxT)
                && ClipAxis(a.y, delta.y, halfY, ref minT, ref maxT);
        }

        private static bool ClipAxis(
            float origin,
            float delta,
            float halfExtent,
            ref float minT,
            ref float maxT)
        {
            if (math.abs(delta) < 0.0001f)
                return math.abs(origin) <= halfExtent;

            float inverse = 1f / delta;
            float first = (-halfExtent - origin) * inverse;
            float second = (halfExtent - origin) * inverse;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }

            minT = math.max(minT, first);
            maxT = math.min(maxT, second);
            return minT <= maxT;
        }
    }
}
