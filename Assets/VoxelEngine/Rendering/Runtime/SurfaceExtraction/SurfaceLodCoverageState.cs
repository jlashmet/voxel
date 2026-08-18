using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    public enum SurfaceLodCompletionKind : byte
    {
        Incomplete = 0,
        Ready = 1,
        KnownEmpty = 2,
    }

    public readonly struct SurfaceLodNodeKey : IEquatable<SurfaceLodNodeKey>
    {
        public readonly int SourceStep;
        public readonly int3 Coordinate;

        public SurfaceLodNodeKey(int sourceStep, int3 coordinate)
        {
            if (!SurfaceLodHierarchy.IsSupportedSourceStep(sourceStep))
                throw new ArgumentOutOfRangeException(nameof(sourceStep), sourceStep,
                    "Surface LOD source step must be one of 1, 2, 4, or 8.");
            SourceStep = sourceStep;
            Coordinate = coordinate;
        }

        public bool Equals(SurfaceLodNodeKey other) =>
            SourceStep == other.SourceStep && Coordinate.Equals(other.Coordinate);
        public override bool Equals(object obj) => obj is SurfaceLodNodeKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((int)math.hash(Coordinate) * 397) ^ SourceStep; }
        }
        public override string ToString() => $"step {SourceStep} @ {Coordinate}";
    }

    public readonly struct SurfaceLodNodeState
    {
        public readonly ulong DesiredGeneration;
        public readonly ulong DrawableGeneration;
        public readonly SurfaceLodCompletionKind DrawableKind;

        public bool HasDrawableProof => DrawableKind != SurfaceLodCompletionKind.Incomplete;
        public bool IsDrawableGeometry => DrawableKind == SurfaceLodCompletionKind.Ready;
        public bool IsKnownEmpty => DrawableKind == SurfaceLodCompletionKind.KnownEmpty;
        public bool IsDesiredComplete => HasDrawableProof && DrawableGeneration == DesiredGeneration;

        internal SurfaceLodNodeState(ulong desiredGeneration, ulong drawableGeneration,
                                     SurfaceLodCompletionKind drawableKind)
        {
            DesiredGeneration = desiredGeneration;
            DrawableGeneration = drawableGeneration;
            DrawableKind = drawableKind;
        }

        internal SurfaceLodNodeState WithDesiredGeneration(ulong generation) =>
            new(generation, DrawableGeneration, DrawableKind);
        internal SurfaceLodNodeState WithCompletion(ulong generation,
                                                    SurfaceLodCompletionKind kind) =>
            new(DesiredGeneration, generation, kind);
    }

    /// <summary>
    /// Scheduler-owned logical completion state. Old drawable proof is retained across
    /// invalidation, but hierarchy transitions require completion for the current desired
    /// generation. Known-empty counts as complete coverage just like ready geometry.
    /// </summary>
    public sealed class SurfaceLodCoverageState
    {
        private readonly Dictionary<SurfaceLodNodeKey, SurfaceLodNodeState> _nodes = new();
        public int Count => _nodes.Count;

        public SurfaceLodNodeState GetOrDefault(in SurfaceLodNodeKey key) =>
            _nodes.TryGetValue(key, out SurfaceLodNodeState state) ? state : default;
        public bool TryGet(in SurfaceLodNodeKey key, out SurfaceLodNodeState state) =>
            _nodes.TryGetValue(key, out state);

        public void Observe(in SurfaceLodNodeKey key, ulong desiredGeneration,
                            ulong drawableGeneration, SurfaceLodCompletionKind drawableKind)
        {
            if (_nodes.TryGetValue(key, out SurfaceLodNodeState previous)
                && desiredGeneration < previous.DesiredGeneration)
                throw new InvalidOperationException(
                    $"Cannot move {key} desired generation backward from {previous.DesiredGeneration} to {desiredGeneration}.");
            if (drawableKind == SurfaceLodCompletionKind.Incomplete)
                drawableGeneration = 0;
            else if (drawableGeneration > desiredGeneration)
                throw new InvalidOperationException(
                    $"{key} drawable generation {drawableGeneration} cannot be newer than desired generation {desiredGeneration}.");
            _nodes[key] = new SurfaceLodNodeState(desiredGeneration, drawableGeneration, drawableKind);
        }

        public void SetDesiredGeneration(in SurfaceLodNodeKey key, ulong generation)
        {
            if (_nodes.TryGetValue(key, out SurfaceLodNodeState state))
            {
                if (generation < state.DesiredGeneration)
                    throw new InvalidOperationException(
                        $"Cannot move {key} desired generation backward from {state.DesiredGeneration} to {generation}.");
                if (generation == state.DesiredGeneration) return;
                _nodes[key] = state.WithDesiredGeneration(generation);
                return;
            }
            _nodes.Add(key, new SurfaceLodNodeState(generation, 0, SurfaceLodCompletionKind.Incomplete));
        }

        public bool TryPublishCompletion(in SurfaceLodNodeKey key, ulong generation,
                                         SurfaceLodCompletionKind kind)
        {
            if (kind == SurfaceLodCompletionKind.Incomplete)
                throw new ArgumentException("Incomplete is not publishable.", nameof(kind));
            if (!_nodes.TryGetValue(key, out SurfaceLodNodeState state)) return false;
            if (generation != state.DesiredGeneration) return false;
            _nodes[key] = state.WithCompletion(generation, kind);
            return true;
        }

        public bool IsDesiredComplete(in SurfaceLodNodeKey key) =>
            _nodes.TryGetValue(key, out SurfaceLodNodeState state) && state.IsDesiredComplete;

        public bool AreChildrenDesiredComplete(in SurfaceLodNodeKey parent)
        {
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep, out int childStep))
                return false;
            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                if (!IsDesiredComplete(child)) return false;
            }
            return true;
        }

        public void Remove(in SurfaceLodNodeKey key) => _nodes.Remove(key);
        public void Clear() => _nodes.Clear();
    }
}
