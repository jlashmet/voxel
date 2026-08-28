using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Chooses one non-overlapping logical LOD coverage set from drawable fallback chunks and
    /// chunks whose desired generation is complete.
    ///
    /// Logical completion is allowed to replace a coarse node only when that expanded subtree has
    /// an actual drawable representative. If every logically active descendant is proof-only
    /// (for example current-known-empty metadata while finer geometry is still converging), the
    /// coarsest drawable fallback remains in the production draw set instead of opening a hole.
    /// </summary>
    internal sealed class SurfaceLodVisibilitySelector
    {
        private readonly HashSet<SurfaceLodNodeKey> _drawable = new();
        private readonly HashSet<SurfaceLodNodeKey> _drawableAncestors = new();
        private readonly HashSet<SurfaceLodNodeKey> _currentComplete = new();
        private readonly HashSet<SurfaceLodNodeKey> _active = new();
        private readonly HashSet<SurfaceLodNodeKey> _drawActive = new();

        public int Count => _active.Count;
        internal int DrawCount => _drawActive.Count;

        /// <summary>
        /// Production draw eligibility. The scheduler calls this only for entries that a worker
        /// has already proven in-band, in-frustum and physically drawable.
        /// </summary>
        public bool IsActive(in SurfaceLodNodeKey key) => _drawActive.Contains(key);

        /// <summary>Logical ownership before physical-fallback preservation.</summary>
        internal bool IsLogicallyActive(in SurfaceLodNodeKey key) => _active.Contains(key);

        /// <summary>
        /// A direct child can satisfy the current camera's handoff only if the finer ring owns the
        /// coordinate. Once owned, off-frustum coverage is irrelevant to this frame; in-frustum
        /// coverage must be current-ready or current-known-empty before the coarse fallback leaves.
        /// </summary>
        internal static bool IsCurrentViewComplete(bool inBand, bool inFrustum,
                                                   bool currentReady, bool currentEmpty) =>
            inBand && (!inFrustum || currentReady || currentEmpty);

        public void Rebuild(IReadOnlyList<SurfaceLodNodeKey> drawableNodes,
                            IReadOnlyList<SurfaceLodNodeKey> currentCompleteNodes)
        {
            if (drawableNodes == null) throw new ArgumentNullException(nameof(drawableNodes));
            if (currentCompleteNodes == null)
                throw new ArgumentNullException(nameof(currentCompleteNodes));

            _drawable.Clear();
            _drawableAncestors.Clear();
            _currentComplete.Clear();
            _active.Clear();
            _drawActive.Clear();

            for (int i = 0; i < drawableNodes.Count; i++)
            {
                SurfaceLodNodeKey node = drawableNodes[i];
                _drawable.Add(node);
                SurfaceLodNodeKey cursor = node;
                while (SurfaceLodHierarchy.TryGetParentSourceStep(
                           cursor.SourceStep, out int parentStep))
                {
                    cursor = new SurfaceLodNodeKey(
                        parentStep, SurfaceLodHierarchy.ParentCoordinate(cursor.Coordinate));
                    _drawableAncestors.Add(cursor);
                }
            }
            for (int i = 0; i < currentCompleteNodes.Count; i++)
                _currentComplete.Add(currentCompleteNodes[i]);

            foreach (SurfaceLodNodeKey node in _drawable)
            {
                if (!HasDrawableAncestor(node)) Expand(node);
            }

            foreach (SurfaceLodNodeKey node in _drawable)
            {
                if (!HasDrawableAncestor(node)) SelectDrawableCoverage(node);
            }
        }

        private bool HasDrawableAncestor(in SurfaceLodNodeKey node)
        {
            SurfaceLodNodeKey cursor = node;
            while (SurfaceLodHierarchy.TryGetParentSourceStep(
                       cursor.SourceStep, out int parentStep))
            {
                cursor = new SurfaceLodNodeKey(
                    parentStep, SurfaceLodHierarchy.ParentCoordinate(cursor.Coordinate));
                if (_drawable.Contains(cursor)) return true;
            }
            return false;
        }

        private void Expand(in SurfaceLodNodeKey node)
        {
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(
                    node.SourceStep, out int childStep))
            {
                _active.Add(node);
                return;
            }

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(node.Coordinate, childIndex));
                if (!_currentComplete.Contains(child))
                {
                    _active.Add(node);
                    return;
                }
            }

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(node.Coordinate, childIndex));
                Expand(child);
            }
        }

        /// <summary>
        /// Mirrors the logical expansion into a physical draw set. A logical proof-only leaf does
        /// not count as replacement geometry. If an expanded drawable node has no physical draw in
        /// any descendant path, retain that node as the fallback for the subtree.
        /// </summary>
        private bool SelectDrawableCoverage(in SurfaceLodNodeKey node)
        {
            bool drawable = _drawable.Contains(node);
            if (_active.Contains(node))
            {
                if (!drawable) return false;
                _drawActive.Add(node);
                return true;
            }

            if (!drawable && !_drawableAncestors.Contains(node)) return false;

            if (!SurfaceLodHierarchy.TryGetChildSourceStep(
                    node.SourceStep, out int childStep))
            {
                if (!drawable) return false;
                _drawActive.Add(node);
                return true;
            }

            bool hasDrawableReplacement = false;
            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(node.Coordinate, childIndex));
                hasDrawableReplacement |= SelectDrawableCoverage(child);
            }

            if (hasDrawableReplacement || !drawable) return hasDrawableReplacement;

            _drawActive.Add(node);
            return true;
        }
    }
}
