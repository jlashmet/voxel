using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Chooses one non-overlapping logical LOD coverage set from drawable fallback chunks and
    /// chunks whose desired generation is complete.
    ///
    /// A drawable coarse parent remains active while finer coverage is partial. Once every direct
    /// child is complete, the parent is replaced atomically; known-empty children participate in
    /// that proof even though they have no draw entry. A known-empty node never becomes a root by
    /// itself, so logical air cannot suppress finer drawable geometry when there is no coarse
    /// fallback to show.
    /// </summary>
    internal sealed class SurfaceLodVisibilitySelector
    {
        private readonly HashSet<SurfaceLodNodeKey> _drawable = new();
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

            _drawable.Clear();
            _currentComplete.Clear();
            _active.Clear();

            for (int i = 0; i < drawableNodes.Count; i++)
                _drawable.Add(drawableNodes[i]);
            for (int i = 0; i < currentCompleteNodes.Count; i++)
                _currentComplete.Add(currentCompleteNodes[i]);

            foreach (SurfaceLodNodeKey node in _drawable)
            {
                if (!HasDrawableAncestor(node)) Expand(node);
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
    }
}
