using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum TopDownWorldNodeKind
    {
        Settlement,
        Region,
        Route,
        Landmark
    }

    public enum TopDownWorldEvidenceStrength
    {
        VerifiedHardConstraint,
        InferredSoftGuidance
    }

    public sealed class TopDownWorldEvidence
    {
        public string Source { get; }
        public TopDownWorldEvidenceStrength Strength { get; }

        public TopDownWorldEvidence(string source, TopDownWorldEvidenceStrength strength)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("World-layout evidence requires a source.", nameof(source));
            Source = source;
            Strength = strength;
        }
    }

    public readonly struct TopDownWorldGridPoint : IEquatable<TopDownWorldGridPoint>
    {
        public int X { get; }
        public int Y { get; }

        public TopDownWorldGridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TopDownWorldGridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is TopDownWorldGridPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";

        public static TopDownWorldGridPoint operator +(
            TopDownWorldGridPoint a,
            TopDownWorldGridPoint b) => new TopDownWorldGridPoint(a.X + b.X, a.Y + b.Y);
    }

    public sealed class TopDownWorldNodeSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public TopDownWorldNodeKind Kind { get; }
        public int EnvelopeHalfExtentDm { get; }

        public TopDownWorldNodeSpec(
            string id,
            string displayName,
            TopDownWorldNodeKind kind,
            int envelopeHalfExtentDm)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A world-layout node requires an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A world-layout node requires a display name.", nameof(displayName));
            if (envelopeHalfExtentDm < 1)
                throw new ArgumentOutOfRangeException(nameof(envelopeHalfExtentDm));

            Id = id;
            DisplayName = displayName;
            Kind = kind;
            EnvelopeHalfExtentDm = envelopeHalfExtentDm;
        }
    }

    public sealed class TopDownWorldRouteSpec
    {
        public string FromId { get; }
        public string ToId { get; }
        public TopDownWorldGridPoint PlacementDelta { get; }
        public int CorridorWidthDm { get; }
        public TopDownWorldEvidence TopologyEvidence { get; }
        public TopDownWorldEvidence PlacementEvidence { get; }

        public TopDownWorldRouteSpec(
            string fromId,
            string toId,
            TopDownWorldGridPoint placementDelta,
            int corridorWidthDm,
            TopDownWorldEvidence topologyEvidence,
            TopDownWorldEvidence placementEvidence)
        {
            if (string.IsNullOrWhiteSpace(fromId))
                throw new ArgumentException("A world route requires a source node.", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId))
                throw new ArgumentException("A world route requires a destination node.", nameof(toId));
            if (placementDelta.X == 0 && placementDelta.Y == 0)
                throw new ArgumentException("A world route requires a non-zero placement delta.", nameof(placementDelta));
            if (corridorWidthDm < 10)
                throw new ArgumentOutOfRangeException(nameof(corridorWidthDm));
            if (topologyEvidence == null)
                throw new ArgumentNullException(nameof(topologyEvidence));
            if (placementEvidence == null)
                throw new ArgumentNullException(nameof(placementEvidence));
            if (topologyEvidence.Strength != TopDownWorldEvidenceStrength.VerifiedHardConstraint)
                throw new ArgumentException("Route topology evidence must be a verified hard constraint.", nameof(topologyEvidence));
            if (placementEvidence.Strength != TopDownWorldEvidenceStrength.InferredSoftGuidance)
                throw new ArgumentException("Route placement evidence must remain soft guidance.", nameof(placementEvidence));

            FromId = fromId;
            ToId = toId;
            PlacementDelta = placementDelta;
            CorridorWidthDm = corridorWidthDm;
            TopologyEvidence = topologyEvidence;
            PlacementEvidence = placementEvidence;
        }

        public string Key => FromId + "->" + ToId;
    }

    public sealed class TopDownWorldLayoutSpec
    {
        private readonly TopDownWorldNodeSpec[] _nodes;
        private readonly TopDownWorldRouteSpec[] _routes;

        public string RootId { get; }
        public IReadOnlyList<TopDownWorldNodeSpec> Nodes => _nodes;
        public IReadOnlyList<TopDownWorldRouteSpec> Routes => _routes;

        public TopDownWorldLayoutSpec(
            string rootId,
            IReadOnlyList<TopDownWorldNodeSpec> nodes,
            IReadOnlyList<TopDownWorldRouteSpec> routes)
        {
            if (string.IsNullOrWhiteSpace(rootId))
                throw new ArgumentException("A world layout requires a root node.", nameof(rootId));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (routes == null) throw new ArgumentNullException(nameof(routes));

            RootId = rootId;
            _nodes = Copy(nodes);
            _routes = Copy(routes);
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    public sealed class TopDownWorldNodePlacement
    {
        public TopDownWorldNodeSpec Node { get; }
        public TopDownWorldGridPoint Position { get; }

        public TopDownWorldNodePlacement(TopDownWorldNodeSpec node, TopDownWorldGridPoint position)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Position = position;
        }
    }

    public sealed class TopDownWorldLayout
    {
        private readonly TopDownWorldNodePlacement[] _nodes;
        private readonly TopDownWorldRouteSpec[] _routes;

        public string RootId { get; }
        public uint Seed { get; }
        public IReadOnlyList<TopDownWorldNodePlacement> Nodes => _nodes;
        public IReadOnlyList<TopDownWorldRouteSpec> Routes => _routes;

        public TopDownWorldLayout(
            string rootId,
            uint seed,
            IReadOnlyList<TopDownWorldNodePlacement> nodes,
            IReadOnlyList<TopDownWorldRouteSpec> routes)
        {
            RootId = rootId ?? throw new ArgumentNullException(nameof(rootId));
            Seed = seed;
            _nodes = Copy(nodes ?? throw new ArgumentNullException(nameof(nodes)));
            _routes = Copy(routes ?? throw new ArgumentNullException(nameof(routes)));
        }

        public bool TryGetPosition(string nodeId, out TopDownWorldGridPoint position)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (!string.Equals(_nodes[i].Node.Id, nodeId, StringComparison.Ordinal))
                    continue;

                position = _nodes[i].Position;
                return true;
            }

            position = default;
            return false;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }
}
