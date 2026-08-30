using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum WorldRoadSemanticClass : byte
    {
        Vehicle = 0,
        Pedestrian = 1,
    }

    public enum WorldRoadMarkingPolicy : byte
    {
        None = 0,
        CentreMarkers = 1,
    }

    public enum WorldRoadCrosswalkPolicy : byte
    {
        None = 0,
        AtSharedJunctions = 1,
    }

    public enum WorldRoadJunctionKind : byte
    {
        Join = 0,
        Intersection = 1,
    }

    public sealed class WorldRoadNetworkRoute
    {
        public ResolvedWorldRoad Road { get; }
        public WorldRoadSemanticClass SemanticClass { get; }
        public int ShoulderWidthDm { get; }
        public int ClearanceWidthDm { get; }
        public WorldRoadMarkingPolicy MarkingPolicy { get; }
        public WorldRoadCrosswalkPolicy CrosswalkPolicy { get; }

        public string Id => Road.Intent.Id;
        public int CarriagewayWidthDm => Road.Intent.Profile.CarriagewayWidthDm;
        public int SurfaceRadiusDm => Road.Intent.Profile.CoreRadiusDm + ShoulderWidthDm;
        public int GradeRadiusDm => Math.Max(SurfaceRadiusDm, Road.Intent.Profile.InfluenceRadiusDm);
        public int ClearanceRadiusDm => GradeRadiusDm + ClearanceWidthDm;

        public WorldRoadNetworkRoute(
            ResolvedWorldRoad road,
            WorldRoadSemanticClass semanticClass,
            int shoulderWidthDm,
            int clearanceWidthDm,
            WorldRoadMarkingPolicy markingPolicy = WorldRoadMarkingPolicy.None,
            WorldRoadCrosswalkPolicy crosswalkPolicy = WorldRoadCrosswalkPolicy.None)
        {
            Road = road ?? throw new ArgumentNullException(nameof(road));
            if (!road.IsResolved || road.Points.Count < 2)
                throw new ArgumentException("A network route requires resolved road geometry.", nameof(road));
            if (shoulderWidthDm < 0) throw new ArgumentOutOfRangeException(nameof(shoulderWidthDm));
            if (clearanceWidthDm < 0) throw new ArgumentOutOfRangeException(nameof(clearanceWidthDm));

            SemanticClass = semanticClass;
            ShoulderWidthDm = shoulderWidthDm;
            ClearanceWidthDm = clearanceWidthDm;
            MarkingPolicy = markingPolicy;
            CrosswalkPolicy = crosswalkPolicy;
        }
    }

    public readonly struct WorldRoadNetworkSample
    {
        public readonly WorldRoadNetworkRoute Route;
        public readonly WorldRoadInfluenceSample Influence;
        public readonly int TangentXdm;
        public readonly int TangentZdm;
        public readonly int RightXdm;
        public readonly int RightZdm;
        public readonly int ClearanceCoverage31;

        public WorldRoadNetworkSample(
            WorldRoadNetworkRoute route,
            WorldRoadInfluenceSample influence,
            int tangentXdm,
            int tangentZdm,
            int clearanceCoverage31)
        {
            Route = route;
            Influence = influence;
            TangentXdm = tangentXdm;
            TangentZdm = tangentZdm;
            RightXdm = -tangentZdm;
            RightZdm = tangentXdm;
            ClearanceCoverage31 = clearanceCoverage31;
        }
    }

    public readonly struct WorldRoadJunction
    {
        public readonly int Xdm;
        public readonly int Zdm;
        public readonly int Degree;
        public readonly WorldRoadJunctionKind Kind;

        public WorldRoadJunction(int xdm, int zdm, int degree)
        {
            Xdm = xdm;
            Zdm = zdm;
            Degree = degree;
            Kind = degree > 2 ? WorldRoadJunctionKind.Intersection : WorldRoadJunctionKind.Join;
        }
    }

    /// <summary>
    /// Deterministic aggregate over resolved road geometry. All spatial consumers query this object
    /// rather than reproducing polyline-distance, shoulder, clearance, or local-frame logic.
    /// The aggregate stores route-local resolved points, derives exact shared-vertex junctions, and
    /// caches presentation paths/influences after topology is known. Streaming order cannot change
    /// its answers and repeated spatial queries allocate no presentation geometry.
    /// </summary>
    public sealed class WorldRoadNetwork
    {
        private readonly WorldRoadNetworkRoute[] _routes;
        private readonly WorldRoadJunction[] _junctions;
        private readonly IReadOnlyList<ResolvedWorldRoadPoint>[] _presentationPaths;
        private readonly WorldRoadInfluence[] _influences;

        public IReadOnlyList<WorldRoadNetworkRoute> Routes => _routes;
        public IReadOnlyList<WorldRoadJunction> Junctions => _junctions;

        public WorldRoadNetwork(IReadOnlyList<WorldRoadNetworkRoute> routes)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            _routes = new WorldRoadNetworkRoute[routes.Count];
            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i] == null) throw new ArgumentException("Road network cannot contain null routes.", nameof(routes));
                _routes[i] = routes[i];
            }

            Array.Sort(_routes, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (var i = 1; i < _routes.Length; i++)
                if (string.Equals(_routes[i - 1].Id, _routes[i].Id, StringComparison.Ordinal))
                    throw new ArgumentException("Road network contains duplicate route id '" + _routes[i].Id + "'.", nameof(routes));

            _junctions = BuildJunctions(_routes);
            _presentationPaths = new IReadOnlyList<ResolvedWorldRoadPoint>[_routes.Length];
            _influences = new WorldRoadInfluence[_routes.Length];
            for (var i = 0; i < _routes.Length; i++)
            {
                _presentationPaths[i] = WorldRoadPresentationPath.Build(_routes[i].Road, _junctions);
                _influences[i] = new WorldRoadInfluence(_routes[i].Road, _junctions);
            }
        }

        public bool TryGetRoute(string id, out WorldRoadNetworkRoute route)
        {
            if (string.IsNullOrEmpty(id)) { route = null; return false; }
            var low = 0;
            var high = _routes.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int compare = string.CompareOrdinal(_routes[middle].Id, id);
                if (compare == 0) { route = _routes[middle]; return true; }
                if (compare < 0) low = middle + 1;
                else high = middle - 1;
            }
            route = null;
            return false;
        }

        public bool TrySample(int xdm, int zdm, out WorldRoadNetworkSample sample)
        {
            bool found = false;
            WorldRoadNetworkSample best = default;
            for (var i = 0; i < _routes.Length; i++)
            {
                WorldRoadNetworkRoute route = _routes[i];
                if (!_influences[i].TrySample(xdm, zdm, out WorldRoadInfluenceSample roadSample)) continue;
                ClosestSegment(_presentationPaths[i], xdm, zdm, out int distance, out int tangentX, out int tangentZ);
                int clearanceCoverage = Coverage(distance, route.ClearanceRadiusDm);
                var candidate = new WorldRoadNetworkSample(route, roadSample, tangentX, tangentZ, clearanceCoverage);
                if (!found || Better(candidate, best)) { best = candidate; found = true; }
            }
            sample = best;
            return found;
        }

        public bool TrySampleClearance(int xdm, int zdm, out WorldRoadNetworkSample sample)
        {
            bool found = false;
            WorldRoadNetworkSample best = default;
            for (var i = 0; i < _routes.Length; i++)
            {
                WorldRoadNetworkRoute route = _routes[i];
                ClosestSegment(_presentationPaths[i], xdm, zdm, out int distance, out int tangentX, out int tangentZ);
                if (distance > route.ClearanceRadiusDm) continue;

                WorldRoadInfluenceSample physical;
                if (!_influences[i].TrySample(xdm, zdm, out physical))
                    physical = new WorldRoadInfluenceSample(distance, 0, 0, 0, false);
                int clearanceCoverage = Coverage(distance, route.ClearanceRadiusDm);
                var candidate = new WorldRoadNetworkSample(route, physical, tangentX, tangentZ, clearanceCoverage);
                if (!found || candidate.ClearanceCoverage31 > best.ClearanceCoverage31
                    || candidate.ClearanceCoverage31 == best.ClearanceCoverage31
                       && string.CompareOrdinal(candidate.Route.Id, best.Route.Id) < 0)
                {
                    best = candidate;
                    found = true;
                }
            }
            sample = best;
            return found;
        }

        private static bool Better(WorldRoadNetworkSample candidate, WorldRoadNetworkSample best)
        {
            if (candidate.Influence.Coverage31 != best.Influence.Coverage31)
                return candidate.Influence.Coverage31 > best.Influence.Coverage31;
            if (candidate.Influence.DistanceDm != best.Influence.DistanceDm)
                return candidate.Influence.DistanceDm < best.Influence.DistanceDm;
            return string.CompareOrdinal(candidate.Route.Id, best.Route.Id) < 0;
        }

        private static int Coverage(int distanceDm, int radiusDm)
        {
            if (radiusDm <= 0) return distanceDm == 0 ? 31 : 0;
            if (distanceDm >= radiusDm) return distanceDm == radiusDm ? 1 : 0;
            return Math.Max(1, Math.Min(31, ((radiusDm - distanceDm) * 31 + radiusDm - 1) / radiusDm));
        }

        private static void ClosestSegment(
            IReadOnlyList<ResolvedWorldRoadPoint> points,
            int xdm,
            int zdm,
            out int distanceDm,
            out int tangentXdm,
            out int tangentZdm)
        {
            long bestDistanceSquared = long.MaxValue;
            tangentXdm = 0;
            tangentZdm = 1;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                ResolvedWorldRoadPoint a = points[i];
                ResolvedWorldRoadPoint b = points[i + 1];
                long dx = (long)b.Xdm - a.Xdm;
                long dz = (long)b.Zdm - a.Zdm;
                long lengthSquared = dx * dx + dz * dz;
                if (lengthSquared <= 0) continue;
                long dot = ((long)xdm - a.Xdm) * dx + ((long)zdm - a.Zdm) * dz;
                dot = Math.Max(0L, Math.Min(lengthSquared, dot));
                long qx = (long)a.Xdm + DivideRounded(dx * dot, lengthSquared);
                long qz = (long)a.Zdm + DivideRounded(dz * dot, lengthSquared);
                long ex = (long)xdm - qx;
                long ez = (long)zdm - qz;
                long squared = ex * ex + ez * ez;
                if (squared >= bestDistanceSquared) continue;
                bestDistanceSquared = squared;
                tangentXdm = ClampToInt(dx);
                tangentZdm = ClampToInt(dz);
            }
            distanceDm = IntegerSqrt(bestDistanceSquared == long.MaxValue ? 0 : bestDistanceSquared);
        }

        private static WorldRoadJunction[] BuildJunctions(WorldRoadNetworkRoute[] routes)
        {
            var counts = new Dictionary<long, int>();
            for (var r = 0; r < routes.Length; r++)
            {
                IReadOnlyList<ResolvedWorldRoadPoint> points = routes[r].Road.Points;
                for (var p = 0; p < points.Count; p++)
                {
                    long key = Key(points[p].Xdm, points[p].Zdm);
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
            }

            var result = new List<WorldRoadJunction>();
            foreach (KeyValuePair<long, int> pair in counts)
            {
                if (pair.Value < 2) continue;
                Decode(pair.Key, out int x, out int z);
                result.Add(new WorldRoadJunction(x, z, pair.Value));
            }
            result.Sort((a, b) => a.Xdm != b.Xdm ? a.Xdm.CompareTo(b.Xdm) : a.Zdm.CompareTo(b.Zdm));
            return result.ToArray();
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        private static void Decode(long key, out int x, out int z) { x = (int)(key >> 32); z = unchecked((int)(uint)key); }

        private static long DivideRounded(long numerator, long denominator)
        {
            if (numerator >= 0) return (numerator + denominator / 2) / denominator;
            return -((-numerator + denominator / 2) / denominator);
        }

        private static int ClampToInt(long value)
            => value < int.MinValue ? int.MinValue : value > int.MaxValue ? int.MaxValue : (int)value;

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }
            return high > int.MaxValue ? int.MaxValue : (int)high;
        }
    }
}