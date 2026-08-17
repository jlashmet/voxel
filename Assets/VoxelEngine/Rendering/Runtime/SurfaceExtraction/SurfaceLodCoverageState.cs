using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Completion proof for one render-node generation. Known-empty is as complete as ready
    /// geometry for hierarchy switching: it proves that the node contributes no drawable
    /// surface for that authoritative source generation.
    /// </summary>
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
            unchecked
            {
                return ((int)math.hash(Coordinate) * 397) ^ SourceStep;
            }
        }

        public override string ToString() => $"step {SourceStep} @ {Coordinate}";
    }

    /// <summary>
    /// Generation state for one render node.
    ///
    /// DesiredGeneration is the authoritative generation the renderer is trying to present.
    /// DrawableGeneration/DrawableKind are deliberately retained across invalidation so old
    /// geometry can remain visible while a replacement is built. Only IsDesiredComplete grants
    /// permission to use this node as part of an atomic parent/child hierarchy transition.
    /// </summary>
    public readonly struct SurfaceLodNodeState
    {
        public readonly ulong DesiredGeneration;
        public readonly ulong DrawableGeneration;
        public readonly SurfaceLodCompletionKind DrawableKind;

        public bool HasDrawableProof => DrawableKind != SurfaceLodCompletionKind.Incomplete;
        public bool IsDrawableGeometry => DrawableKind == SurfaceLodCompletionKind.Ready;
        public bool IsKnownEmpty => DrawableKind == SurfaceLodCompletionKind.KnownEmpty;
        public bool IsDesiredComplete =>
            HasDrawableProof && DrawableGeneration == DesiredGeneration;

        internal SurfaceLodNodeState(ulong desiredGeneration,
                                     ulong drawableGeneration,
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
    /// Scheduler-owned logical coverage state. This contains no GPU/CPU mesh ownership; it only
    /// records generation proofs used to decide when a complete parent may atomically hand off to
    /// all eight complete children (or vice versa).
    ///
    /// Only active/requested hierarchy nodes belong here. Discovery of voxel space does not imply
    /// creation of a coverage-state record.
    /// </summary>
    public sealed class SurfaceLodCoverageState
    {
        private readonly Dictionary<SurfaceLodNodeKey, SurfaceLodNodeState> _nodes = new();

        public int Count => _nodes.Count;

        public SurfaceLodNodeState GetOrDefault(in SurfaceLodNodeKey key) =>
            _nodes.TryGetValue(key, out SurfaceLodNodeState state) ? state : default;

        public bool TryGet(in SurfaceLodNodeKey key, out SurfaceLodNodeState state) =>
            _nodes.TryGetValue(key, out state);

        /// <summary>
        /// Mirrors the source cache's complete observation for one node. This is the integration
        /// path used by the scheduler: an older Ready proof may coexist with a newer desired
        /// generation, while an observed Incomplete state explicitly clears a proof that was
        /// evicted or retired from the source cache.
        /// </summary>
        public void Observe(in SurfaceLodNodeKey key, ulong desiredGeneration,
                            ulong drawableGeneration, SurfaceLodCompletionKind drawableKind)
        {
            if (_nodes.TryGetValue(key, out SurfaceLodNodeState previous)
                && desiredGeneration < previous.DesiredGeneration)
                throw new InvalidOperationException(
                    $"Cannot move {key} desired generation backward from " +
                    $"{previous.DesiredGeneration} to {desiredGeneration}.");
            if (drawableKind == SurfaceLodCompletionKind.Incomplete)
                drawableGeneration = 0;
            else if (drawableGeneration > desiredGeneration)
                throw new InvalidOperationException(
                    $"{key} drawable generation {drawableGeneration} cannot be newer than " +
                    $"desired generation {desiredGeneration}.");

            _nodes[key] = new SurfaceLodNodeState(
                desiredGeneration, drawableGeneration, drawableKind);
        }

        /// <summary>
        /// Advances the authoritative target generation while preserving any older drawable
        /// proof. Generations are monotonic per node; accepting an older target would permit
        /// stale geometry to become authoritative again.
        /// </summary>
        public void SetDesiredGeneration(in SurfaceLodNodeKey key, ulong generation)
        {
            if (_nodes.TryGetValue(key, out SurfaceLodNodeState state))
            {
                if (generation < state.DesiredGeneration)
                    throw new InvalidOperationException(
                        $"Cannot move {key} desired generation backward from " +
                        $"{state.DesiredGeneration} to {generation}.");
                if (generation == state.DesiredGeneration) return;
                _nodes[key] = state.WithDesiredGeneration(generation);
                return;
            }

            _nodes.Add(key, new SurfaceLodNodeState(
                generation, 0, SurfaceLodCompletionKind.Incomplete));
        }

        /// <summary>
        /// Publishes a ready or known-empty proof only when it matches the node's current desired
        /// generation. A late asynchronous completion for an older generation is rejected and
        /// cannot trigger a hierarchy transition.
        /// </summary>
        public bool TryPublishCompletion(in SurfaceLodNodeKey key,
                                         ulong generation,
                                         SurfaceLodCompletionKind kind)
        {
            if (kind == SurfaceLodCompletionKind.Incomplete)
                throw new ArgumentException(
                    "Incomplete is not a publishable completion proof.", nameof(kind));
            if (!_nodes.TryGetValue(key, out SurfaceLodNodeState state)) return false;
            if (generation != state.DesiredGeneration) return false;

            _nodes[key] = state.WithCompletion(generation, kind);
            return true;
        }

        public bool IsDesiredComplete(in SurfaceLodNodeKey key) =>
            _nodes.TryGetValue(key, out SurfaceLodNodeState state) && state.IsDesiredComplete;

        /// <summary>
        /// True only when all eight finer children have a completion proof for each child's
        /// current desired generation. Ready geometry and known-empty proofs both count.
        /// </summary>
        public bool AreChildrenDesiredComplete(in SurfaceLodNodeKey parent)
        {
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep, out int childStep))
                return false;

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                if (!IsDesiredComplete(child)) return false;
            }
            return true;
        }

        public void Remove(in SurfaceLodNodeKey key) => _nodes.Remove(key);

        public void Clear() => _nodes.Clear();
    }
}
