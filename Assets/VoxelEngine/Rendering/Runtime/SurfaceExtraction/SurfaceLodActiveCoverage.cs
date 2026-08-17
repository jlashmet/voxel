using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Logical active-leaf set for voxel surface LOD coverage.
    ///
    /// Active nodes form an antichain: no active node may be an ancestor or descendant of another
    /// active node. Refinement and merge operations replace a complete region atomically so
    /// presentation never observes parent removed + partial children (or the reverse).
    ///
    /// Known-empty nodes remain in the logical active set. They draw nothing, but their completion
    /// proof is required to establish that the active leaves still cover the entire parent region.
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
                if (!SurfaceLodHierarchy.TryGetParentSourceStep(
                        cursor.SourceStep, out int parentStep))
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

        /// <summary>
        /// Seeds an uncovered region once a current-generation completion proof exists.
        /// This is intended for initial/coarse emergency coverage, not arbitrary overlapping
        /// activation. Returns false when the node is incomplete or overlaps active coverage.
        /// </summary>
        public bool TryActivateCompleteNode(in SurfaceLodNodeKey key,
                                            SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.IsDesiredComplete(key)) return false;
            if (OverlapsActiveCoverage(key)) return false;
            return _active.Add(key);
        }

        /// <summary>
        /// Atomically replaces one active parent with its eight current-generation-complete
        /// children. No mutation occurs until every child has a Ready or KnownEmpty proof.
        /// </summary>
        public bool TryRefine(in SurfaceLodNodeKey parent, SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!_active.Contains(parent)) return false;
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep, out int childStep))
                return false;
            if (!state.AreChildrenDesiredComplete(parent)) return false;

            _active.Remove(parent);
            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                _active.Add(child);
            }
            return true;
        }

        /// <summary>
        /// Atomically replaces all active descendants of a parent region with the parent. The
        /// parent itself must be complete for its current desired generation; stale-but-drawable
        /// parent geometry is not sufficient to retire current descendants.
        /// </summary>
        public bool TryMerge(in SurfaceLodNodeKey parent, SurfaceLodCoverageState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.IsDesiredComplete(parent)) return false;
            if (_active.Contains(parent)) return false;

            _mutationScratch.Clear();
            foreach (SurfaceLodNodeKey active in _active)
            {
                if (IsStrictDescendantOf(active, parent))
                    _mutationScratch.Add(active);
            }
            if (_mutationScratch.Count == 0) return false;

            // The active set is only mutated after all preconditions have passed. Because active
            // leaves are maintained exclusively by seed/refine/merge operations, descendants of
            // this parent represent complete logical coverage of the region, including empty
            // leaves. A future debug validator can assert full tiling if other mutation APIs are
            // introduced.
            for (int i = 0; i < _mutationScratch.Count; i++)
                _active.Remove(_mutationScratch[i]);
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
