using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Logical active-leaf set for voxel LOD coverage. Active nodes form an antichain: a complete
    /// parent is replaced by all eight complete children atomically, never by a partial child set.
    /// Known-empty children remain logical leaves so they participate in complete coverage proof.
    /// </summary>
    public sealed class SurfaceLodActiveCoverage
    {
        private readonly HashSet<SurfaceLodNodeKey> _active = new();
        private readonly List<SurfaceLodNodeKey> _mutationScratch = new(16);

        public int Count => _active.Count;
        public bool IsActive(in SurfaceLodNodeKey key) => _active.Contains(key);

        public bool TryFindActiveAncestorOrSelf(in SurfaceLodNodeKey key,
                                                out SurfaceLodNodeKey active)
        {
            SurfaceLodNodeKey cursor = key;
            while (true)
            {
                if (_active.Contains(cursor))
                {
                    active = cursor;
                    return true;
                }
                if (!SurfaceLodHierarchy.TryGetParentSourceStep(cursor.SourceStep, out int parentStep))
                    break;
                cursor = new SurfaceLodNodeKey(
                    parentStep, SurfaceLodHierarchy.ParentCoordinate(cursor.Coordinate));
            }
            active = default;
            return false;
        }

        public bool HasActiveDescendant(in SurfaceLodNodeKey ancestor)
        {
            foreach (SurfaceLodNodeKey node in _active)
                if (IsStrictDescendantOf(node, ancestor)) return true;
            return false;
        }

        internal bool RemoveRetiredLeaf(in SurfaceLodNodeKey key) => _active.Remove(key);

        public bool TryActivateCompleteNode(in SurfaceLodNodeKey key, SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.IsDesiredComplete(key) || OverlapsActiveCoverage(key)) return false;
            return _active.Add(key);
        }

        public bool TryRefine(in SurfaceLodNodeKey parent, SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!_active.Contains(parent)) return false;
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep, out int childStep))
                return false;
            if (!state.AreChildrenDesiredComplete(parent)) return false;

            _active.Remove(parent);
            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                _active.Add(new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex)));
            }
            return true;
        }

        public bool TryMerge(in SurfaceLodNodeKey parent, SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.IsDesiredComplete(parent) || _active.Contains(parent)) return false;
            _mutationScratch.Clear();
            foreach (SurfaceLodNodeKey active in _active)
                if (IsStrictDescendantOf(active, parent)) _mutationScratch.Add(active);
            if (_mutationScratch.Count == 0) return false;
            for (int i = 0; i < _mutationScratch.Count; i++) _active.Remove(_mutationScratch[i]);
            _active.Add(parent);
            _mutationScratch.Clear();
            return true;
        }

        public int CopyActiveTo(List<SurfaceLodNodeKey> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int before = destination.Count;
            foreach (SurfaceLodNodeKey node in _active) destination.Add(node);
            return destination.Count - before;
        }

        public void Clear()
        {
            _active.Clear();
            _mutationScratch.Clear();
        }

        private bool OverlapsActiveCoverage(in SurfaceLodNodeKey candidate)
        {
            foreach (SurfaceLodNodeKey active in _active)
            {
                if (active.Equals(candidate)
                    || IsStrictDescendantOf(candidate, active)
                    || IsStrictDescendantOf(active, candidate))
                    return true;
            }
            return false;
        }

        private static bool IsStrictDescendantOf(in SurfaceLodNodeKey candidate,
                                                 in SurfaceLodNodeKey ancestor)
        {
            if (candidate.SourceStep >= ancestor.SourceStep) return false;
            int sourceStep = candidate.SourceStep;
            var coordinate = candidate.Coordinate;
            while (sourceStep < ancestor.SourceStep)
            {
                if (!SurfaceLodHierarchy.TryGetParentSourceStep(sourceStep, out int parentStep))
                    return false;
                coordinate = SurfaceLodHierarchy.ParentCoordinate(coordinate);
                sourceStep = parentStep;
            }
            return sourceStep == ancestor.SourceStep && coordinate.Equals(ancestor.Coordinate);
        }
    }
}
