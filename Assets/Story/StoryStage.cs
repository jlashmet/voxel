using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    /// <summary>Integer world coordinate used by story staging; no scene Transform is authoritative.</summary>
    public readonly struct StoryInt3 : IEquatable<StoryInt3>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public StoryInt3(int x, int y, int z) { X = x; Y = y; Z = z; }

        public bool Equals(StoryInt3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is StoryInt3 other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((X * 397) ^ Y) * 397 ^ Z; }
        }
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }

    public readonly struct StoryStagePoint
    {
        public StoryInt3 Position { get; }
        public StoryInt3 Forward { get; }

        public StoryStagePoint(StoryInt3 position, StoryInt3 forward)
        {
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>
    /// Per-instance mapping from semantic stage points to deterministic world positions.
    /// Procedural environment code owns how the points are chosen; sequences only consume them.
    /// </summary>
    public sealed class StoryStageBinding
    {
        private readonly Dictionary<StoryStagePointId, StoryStagePoint> _points =
            new Dictionary<StoryStagePointId, StoryStagePoint>();

        public StoryStageBinding Bind(StoryStagePointId id, StoryStagePoint point)
        {
            _points[id] = point;
            return this;
        }

        public bool TryResolve(StoryStagePointId id, out StoryStagePoint point) => _points.TryGetValue(id, out point);

        public StoryStagePoint Resolve(StoryStagePointId id)
        {
            if (_points.TryGetValue(id, out StoryStagePoint point)) return point;
            throw new KeyNotFoundException("Story stage point '" + id + "' was not bound for this sequence instance.");
        }
    }
}
