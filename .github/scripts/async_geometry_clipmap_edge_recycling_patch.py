from pathlib import Path


def once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''        private bool _clipmapWindowValid;
        private int3 _clipmapCenter;
        private int _clipmapRadius;
        // Known-chunk liveness is maintained incrementally.''',
'''        private bool _clipmapWindowValid;
        private int3 _clipmapCenter;
        private int _clipmapRadius;
        // Camera motion retires only the slabs that left the previous clipmap window. The
        // traversal is resumable, so even a teleport never turns residency cleanup into a scan
        // of every known chunk or a full old-window walk in one frame.
        private const int ClipmapEdgeCandidatesPerPrepare = 32;
        private bool _clipmapEdgeRetirementPending;
        private int3 _clipmapRetirementFromCenter;
        private int3 _clipmapRetirementToCenter;
        private int _clipmapRetirementRadius;
        private int _clipmapRetirementAxis;
        private int _clipmapRetirementDepth;
        private int _clipmapRetirementPlaneCursor;
        // Known-chunk liveness is maintained incrementally.''',
'clipmap edge retirement fields')

s = once(s,
'''            sectionStart = Time.realtimeSinceStartupAsDouble;
            StepResidencyPrune(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));''',
'''            sectionStart = Time.realtimeSinceStartupAsDouble;
            StepClipmapEdgeRetirement();
            StepResidencyPrune(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));''',
'prepare clipmap edge retirement')

old_window = '''        public void SetClipmapWindow(int3 centre, int radius)
        {
            _clipmapCenter = centre;
            _clipmapRadius = math.max(0, radius);
            _clipmapWindowValid = true;
        }
'''
new_window = r'''        public void SetClipmapWindow(int3 centre, int radius)
        {
            int nextRadius = math.max(0, radius);
            if (_clipmapWindowValid && _clipmapRadius == nextRadius
                && math.any(_clipmapCenter != centre))
                ScheduleClipmapEdgeRetirement(_clipmapCenter, centre, nextRadius);

            _clipmapCenter = centre;
            _clipmapRadius = nextRadius;
            _clipmapWindowValid = true;
        }

        private void ScheduleClipmapEdgeRetirement(int3 fromCenter, int3 toCenter, int radius)
        {
            if (math.all(fromCenter == toCenter)) return;

            if (_clipmapEdgeRetirementPending && _clipmapRetirementRadius == radius)
            {
                int3 activeDelta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;
                int3 extendedDelta = toCenter - _clipmapRetirementFromCenter;
                bool sameDirection = true;
                for (int axis = 0; axis < 3; axis++)
                {
                    int active = activeDelta[axis];
                    int extended = extendedDelta[axis];
                    if (active != 0 && extended != 0
                        && math.sign(active) != math.sign(extended))
                    {
                        sameDirection = false;
                        break;
                    }
                }

                // Continuous movement in the same direction simply extends the outgoing slab.
                // Keep the existing cursor so already-checked edge coordinates are not revisited.
                if (sameDirection)
                {
                    _clipmapRetirementToCenter = toCenter;
                    return;
                }
            }

            _clipmapRetirementFromCenter = fromCenter;
            _clipmapRetirementToCenter = toCenter;
            _clipmapRetirementRadius = radius;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
            _clipmapEdgeRetirementPending = true;
        }

        private void StepClipmapEdgeRetirement()
        {
            if (!_clipmapEdgeRetirementPending) return;

            int remaining = ClipmapEdgeCandidatesPerPrepare;
            int edge = _clipmapRetirementRadius * 2 + 1;
            int planeCount = edge * edge;
            int3 delta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;

            while (remaining > 0 && _clipmapRetirementAxis < 3)
            {
                int axis = _clipmapRetirementAxis;
                int shift = delta[axis];
                int depthCount = math.min(math.abs(shift), edge);
                if (depthCount == 0 || _clipmapRetirementDepth >= depthCount)
                {
                    _clipmapRetirementAxis++;
                    _clipmapRetirementDepth = 0;
                    _clipmapRetirementPlaneCursor = 0;
                    continue;
                }

                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                while (remaining > 0 && _clipmapRetirementPlaneCursor < planeCount)
                {
                    int linear = _clipmapRetirementPlaneCursor++;
                    int a = linear % edge;
                    int b = linear / edge;
                    int3 coordinate = _clipmapRetirementFromCenter;
                    coordinate[axisA] += a - _clipmapRetirementRadius;
                    coordinate[axisB] += b - _clipmapRetirementRadius;
                    coordinate[axis] += shift > 0
                        ? -_clipmapRetirementRadius + _clipmapRetirementDepth
                        : _clipmapRetirementRadius - _clipmapRetirementDepth;
                    remaining--;

                    // Diagonal movement makes edge planes overlap. Current-window ownership and
                    // _known membership make those duplicates free without another hash set.
                    if (WithinClipmapWindow(coordinate) || !OwnsShard(coordinate)
                        || !_known.Contains(coordinate))
                        continue;
                    if (!TryRemoveChunk(coordinate)) RequeueResidency(coordinate);
                }

                if (_clipmapRetirementPlaneCursor < planeCount) return;
                _clipmapRetirementPlaneCursor = 0;
                _clipmapRetirementDepth++;
            }

            if (_clipmapRetirementAxis < 3) return;
            _clipmapEdgeRetirementPending = false;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
        }
'''
s = once(s, old_window, new_window, 'clipmap window implementation')
cache_path.write_text(s)


test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'ClipmapMovementRetiresOnlyOutgoingEdgesIncrementally' in t:
    raise SystemExit('clipmap edge recycling architecture test already exists')
insert = r'''

        [Test]
        public void ClipmapMovementRetiresOnlyOutgoingEdgesIncrementally()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("ClipmapEdgeCandidatesPerPrepare", cache);
            StringAssert.Contains("ScheduleClipmapEdgeRetirement", cache);
            StringAssert.Contains("StepClipmapEdgeRetirement();", cache);

            int step = cache.IndexOf("private void StepClipmapEdgeRetirement",
                                     StringComparison.Ordinal);
            int stepEnd = cache.IndexOf("private bool WithinClipmapWindow", step,
                                        StringComparison.Ordinal);
            Assert.GreaterOrEqual(step, 0);
            Assert.Greater(stepEnd, step);
            string retirement = cache.Substring(step, stepEnd - step);
            StringAssert.Contains("remaining = ClipmapEdgeCandidatesPerPrepare", retirement);
            StringAssert.Contains("WithinClipmapWindow(coordinate)", retirement);
            StringAssert.Contains("TryRemoveChunk(coordinate)", retirement);
            StringAssert.DoesNotContain("foreach (int3 chunk in _known)", retirement);
            StringAssert.DoesNotContain("foreach (var pair in _entries)", retirement);
        }
'''
marker = '\n\n        [Test]\n        public void BrickPoolSupportsGenerationStampedCowReaders()'
pos = t.find(marker)
if pos < 0:
    raise SystemExit('architecture test insertion marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

cache = cache_path.read_text()
assert 'StepClipmapEdgeRetirement();' in cache
assert 'ClipmapEdgeCandidatesPerPrepare' in cache
step = cache[cache.index('private void StepClipmapEdgeRetirement'):cache.index('private bool WithinClipmapWindow')]
assert 'foreach (int3 chunk in _known)' not in step
assert 'foreach (var pair in _entries)' not in step
