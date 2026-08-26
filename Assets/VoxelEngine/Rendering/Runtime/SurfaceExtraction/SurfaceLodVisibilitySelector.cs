using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Chooses one non-overlapping logical LOD coverage set from the chunks the renderer can
    /// currently fall back to and the chunks whose desired generation is complete.
    ///
    /// A drawable coarse parent remains active while finer coverage is partial. Once every direct
    /// child is complete, the parent is replaced atomically; known-empty children participate in
    /// that proof even though they have no draw entry. Rebuilding from the current scheduler
    /// snapshot also drops nodes that have left the clipmap without retaining stale ownership.
    /// </summary>
    internal sealed class SurfaceLodVisibilitySelector
    {
        private readonly HashSet<SurfaceLodNodeKey> _coverage = new();
        private readonly HashSet<SurfaceLodNodeKey> _currentComplete = new();
        private readonly HashSet<SurfaceLodNodeKey> _active = new();

        public int Count => _active.Count;
        public bool IsActive(in SurfaceLodNodeKey key) => _active.Contains(key);

        public void Rebuild(IReadOnlyList<SurfaceLodNodeKey> drawableNodes,
                            IReadOnlyList<SurfaceLodNodeKey> currentCompleteNodes)
        {
            if (drawableNodes == null) throw new ArgumentNullException(nameof(drawableNodes));
            if (currentCompleteNodes == null)
                throw new ArgumentNullException(nameof(currentCompleteNodes));

            _coverage.Clear();
            _currentComplete.Clear();
            _active.Clear();

            for (int i = 0; i < drawableNodes.Count; i++)
                _coverage.Add(drawableNodes[i]);
            for (int i = 0; i < currentCompleteNodes.Count; i++)
            {
                SurfaceLodNodeKey node = currentCompleteNodes[i];
                _currentComplete.Add(node);
                _coverage.Add(node);
            }

            foreach (SurfaceLodNodeKey node in _coverage)
            {
                if (!HasCoverageAncestor(node)) Expand(node);
            }
        }

        private bool HasCoverageAncestor(in SurfaceLodNodeKey node)
        {
            SurfaceLodNodeKey cursor = node;
            while (SurfaceLodHierarchy.TryGetParentSourceStep(
                       cursor.SourceStep, out int parentStep))
            {
                cursor = new SurfaceLodNodeKey(
                    parentStep, SurfaceLodHierarchy.ParentCoordinate(cursor.Coordinate));
                if (_coverage.Contains(cursor)) return true;
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
    }
}
