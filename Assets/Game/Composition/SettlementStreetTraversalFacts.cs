using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Traversal facts backed by the semantic settlement street graph. This implementation supports
    /// the current WorldGen contract of orthogonal street polylines and explicit per-site access
    /// points. It never treats straight-line site distance as reachability.
    /// </summary>
    public sealed class SettlementStreetTraversalFacts : ISettlementTraversalFacts
    {
        private readonly Dictionary<int, RouteEndpoint> _sites =
            new Dictionary<int, RouteEndpoint>();
        private readonly Dictionary<PointKey, List<Edge>> _graph =
            new Dictionary<PointKey, List<Edge>>();

        public SettlementStreetTraversalFacts(
            SettlementPlan plan,
            ISettlementSiteProjectionProvider projections)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (projections == null) throw new ArgumentNullException(nameof(projections));

            List<Segment> segments = BuildSegments(plan);
            AddStreetIntersections(segments);
            AddProjectedSiteEndpoints(plan, projections, segments);
            MaterializeStreetEdges(segments);
        }

        public bool IsReachable(
            int subjectRoleId,
            int targetRoleId,
            TraversalProfile traversal)
        {
            double distanceDm;
            return TryDistanceDm(subjectRoleId, targetRoleId, traversal, out distanceDm);
        }

        public int TraversalDistanceMetres(
            int subjectRoleId,
            int targetRoleId,
            TraversalProfile traversal)
        {
            double distanceDm;
            if (!TryDistanceDm(subjectRoleId, targetRoleId, traversal, out distanceDm))
                return int.MaxValue;

            return (int)Math.Round(distanceDm / 10.0, MidpointRounding.AwayFromZero);
        }

        private bool TryDistanceDm(
            int subjectRoleId,
            int targetRoleId,
            TraversalProfile traversal,
            out double distanceDm)
        {
            distanceDm = 0.0;
            if (traversal != TraversalProfile.NormalParty)
                return false;

            RouteEndpoint subject;
            RouteEndpoint target;
            if (!_sites.TryGetValue(subjectRoleId, out subject)
                || !_sites.TryGetValue(targetRoleId, out target))
                return false;

            if (subjectRoleId == targetRoleId)
                return true;

            double networkDistance = ShortestNetworkDistanceDm(
                subject.NetworkPoint,
                target.NetworkPoint);
            if (double.IsPositiveInfinity(networkDistance))
                return false;

            distanceDm = subject.ConnectorDistanceDm
                       + networkDistance
                       + target.ConnectorDistanceDm;
            return true;
        }

        private void AddProjectedSiteEndpoints(
            SettlementPlan plan,
            ISettlementSiteProjectionProvider projections,
            List<Segment> segments)
        {
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                if (!projections.TryProject(site, out projection))
                    continue;
                if (!site.Access.IsSpecified)
                    continue;

                var networkPoint = new PointKey(
                    site.Access.NetworkPointDm.X,
                    site.Access.NetworkPointDm.Y);

                bool validAccess;
                switch (site.Access.Kind)
                {
                    case SiteAccessKind.Street:
                        validAccess = AddStreetAccessPoint(
                            site.Access.TargetId,
                            networkPoint,
                            segments);
                        break;
                    case SiteAccessKind.Plaza:
                        validAccess = string.Equals(
                                          site.Access.TargetId,
                                          plan.Plaza.Id,
                                          StringComparison.Ordinal)
                                      && IsInsidePlaza(networkPoint, plan.Plaza);
                        if (validAccess)
                            AddPointToContainingSegments(networkPoint, segments);
                        break;
                    default:
                        validAccess = false;
                        break;
                }

                if (!validAccess)
                    continue;

                EnsureNode(networkPoint);
                double connector = DistanceDm(
                    projection.PublicEntranceDm.X,
                    projection.PublicEntranceDm.Y,
                    networkPoint.X,
                    networkPoint.Z);
                _sites[site.RoleId] = new RouteEndpoint(networkPoint, connector);
            }
        }

        private bool AddStreetAccessPoint(
            string streetId,
            PointKey point,
            List<Segment> segments)
        {
            bool found = false;
            for (var i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                if (!string.Equals(segment.StreetId, streetId, StringComparison.Ordinal))
                    continue;
                if (!segment.Contains(point))
                    continue;

                segment.Points.Add(point);
                found = true;
            }

            return found;
        }

        private static void AddPointToContainingSegments(
            PointKey point,
            List<Segment> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i].Contains(point))
                    segments[i].Points.Add(point);
            }
        }

        private static bool IsInsidePlaza(PointKey point, PlannedPlaza plaza)
        {
            int halfX = plaza.SizeDm.X / 2;
            int halfZ = plaza.SizeDm.Y / 2;
            return point.X >= plaza.CentreDm.X - halfX
                && point.X <= plaza.CentreDm.X + halfX
                && point.Z >= plaza.CentreDm.Y - halfZ
                && point.Z <= plaza.CentreDm.Y + halfZ;
        }

        private static List<Segment> BuildSegments(SettlementPlan plan)
        {
            var segments = new List<Segment>();
            for (var streetIndex = 0; streetIndex < plan.Streets.Count; streetIndex++)
            {
                PlannedStreet street = plan.Streets[streetIndex];
                for (var pointIndex = 1; pointIndex < street.Points.Count; pointIndex++)
                {
                    var segment = new Segment(
                        street.Id,
                        new PointKey(street.Points[pointIndex - 1].X, street.Points[pointIndex - 1].Y),
                        new PointKey(street.Points[pointIndex].X, street.Points[pointIndex].Y));
                    if (!segment.IsOrthogonal || segment.A.Equals(segment.B))
                        continue;

                    segment.Points.Add(segment.A);
                    segment.Points.Add(segment.B);
                    segments.Add(segment);
                }
            }

            return segments;
        }

        private static void AddStreetIntersections(List<Segment> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                for (var j = i + 1; j < segments.Count; j++)
                    AddIntersections(segments[i], segments[j]);
            }
        }

        private static void AddIntersections(Segment a, Segment b)
        {
            if (a.IsHorizontal && b.IsVertical)
            {
                AddCrossIntersection(a, b);
                return;
            }
            if (a.IsVertical && b.IsHorizontal)
            {
                AddCrossIntersection(b, a);
                return;
            }

            if (a.IsHorizontal && b.IsHorizontal && a.A.Z == b.A.Z)
            {
                AddOverlappingEndpoints(a, b);
                return;
            }
            if (a.IsVertical && b.IsVertical && a.A.X == b.A.X)
                AddOverlappingEndpoints(a, b);
        }

        private static void AddCrossIntersection(Segment horizontal, Segment vertical)
        {
            var point = new PointKey(vertical.A.X, horizontal.A.Z);
            if (!horizontal.Contains(point) || !vertical.Contains(point))
                return;

            horizontal.Points.Add(point);
            vertical.Points.Add(point);
        }

        private static void AddOverlappingEndpoints(Segment a, Segment b)
        {
            if (b.Contains(a.A))
            {
                a.Points.Add(a.A);
                b.Points.Add(a.A);
            }
            if (b.Contains(a.B))
            {
                a.Points.Add(a.B);
                b.Points.Add(a.B);
            }
            if (a.Contains(b.A))
            {
                a.Points.Add(b.A);
                b.Points.Add(b.A);
            }
            if (a.Contains(b.B))
            {
                a.Points.Add(b.B);
                b.Points.Add(b.B);
            }
        }

        private void MaterializeStreetEdges(List<Segment> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                var ordered = new List<PointKey>(segment.Points);
                if (segment.IsHorizontal)
                    ordered.Sort(CompareXThenZ);
                else
                    ordered.Sort(CompareZThenX);

                for (var p = 0; p < ordered.Count; p++)
                    EnsureNode(ordered[p]);

                for (var p = 1; p < ordered.Count; p++)
                {
                    PointKey from = ordered[p - 1];
                    PointKey to = ordered[p];
                    if (from.Equals(to))
                        continue;
                    double distance = DistanceDm(from.X, from.Z, to.X, to.Z);
                    AddEdge(from, to, distance);
                    AddEdge(to, from, distance);
                }
            }
        }

        private double ShortestNetworkDistanceDm(PointKey start, PointKey target)
        {
            if (start.Equals(target))
                return 0.0;
            if (!_graph.ContainsKey(start) || !_graph.ContainsKey(target))
                return double.PositiveInfinity;

            var distances = new Dictionary<PointKey, double>();
            var visited = new HashSet<PointKey>();
            foreach (PointKey node in _graph.Keys)
                distances[node] = double.PositiveInfinity;
            distances[start] = 0.0;

            while (visited.Count < _graph.Count)
            {
                PointKey current = default(PointKey);
                double best = double.PositiveInfinity;
                bool found = false;
                foreach (KeyValuePair<PointKey, double> pair in distances)
                {
                    if (visited.Contains(pair.Key) || pair.Value >= best)
                        continue;
                    current = pair.Key;
                    best = pair.Value;
                    found = true;
                }

                if (!found)
                    break;
                if (current.Equals(target))
                    return best;

                visited.Add(current);
                List<Edge> edges = _graph[current];
                for (var i = 0; i < edges.Count; i++)
                {
                    Edge edge = edges[i];
                    if (visited.Contains(edge.Target))
                        continue;
                    double candidate = best + edge.DistanceDm;
                    if (candidate < distances[edge.Target])
                        distances[edge.Target] = candidate;
                }
            }

            return double.PositiveInfinity;
        }

        private void EnsureNode(PointKey point)
        {
            if (!_graph.ContainsKey(point))
                _graph.Add(point, new List<Edge>());
        }

        private void AddEdge(PointKey from, PointKey to, double distanceDm)
        {
            EnsureNode(from);
            EnsureNode(to);
            _graph[from].Add(new Edge(to, distanceDm));
        }

        private static int CompareXThenZ(PointKey a, PointKey b)
        {
            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Z.CompareTo(b.Z);
        }

        private static int CompareZThenX(PointKey a, PointKey b)
        {
            int z = a.Z.CompareTo(b.Z);
            return z != 0 ? z : a.X.CompareTo(b.X);
        }

        private static double DistanceDm(int ax, int az, int bx, int bz)
        {
            double dx = ax - bx;
            double dz = az - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private readonly struct RouteEndpoint
        {
            public PointKey NetworkPoint { get; }
            public double ConnectorDistanceDm { get; }

            public RouteEndpoint(PointKey networkPoint, double connectorDistanceDm)
            {
                NetworkPoint = networkPoint;
                ConnectorDistanceDm = connectorDistanceDm;
            }
        }

        private readonly struct Edge
        {
            public PointKey Target { get; }
            public double DistanceDm { get; }

            public Edge(PointKey target, double distanceDm)
            {
                Target = target;
                DistanceDm = distanceDm;
            }
        }

        private sealed class Segment
        {
            public string StreetId { get; }
            public PointKey A { get; }
            public PointKey B { get; }
            public HashSet<PointKey> Points { get; } = new HashSet<PointKey>();

            public bool IsHorizontal => A.Z == B.Z;
            public bool IsVertical => A.X == B.X;
            public bool IsOrthogonal => IsHorizontal || IsVertical;

            public Segment(string streetId, PointKey a, PointKey b)
            {
                StreetId = streetId;
                A = a;
                B = b;
            }

            public bool Contains(PointKey point)
            {
                if (IsHorizontal)
                    return point.Z == A.Z
                        && point.X >= Math.Min(A.X, B.X)
                        && point.X <= Math.Max(A.X, B.X);
                if (IsVertical)
                    return point.X == A.X
                        && point.Z >= Math.Min(A.Z, B.Z)
                        && point.Z <= Math.Max(A.Z, B.Z);
                return false;
            }
        }

        private readonly struct PointKey : IEquatable<PointKey>
        {
            public int X { get; }
            public int Z { get; }

            public PointKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(PointKey other) => X == other.X && Z == other.Z;

            public override bool Equals(object obj) =>
                obj is PointKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }
    }
}
