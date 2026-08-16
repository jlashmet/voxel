from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = path.read_text()

s = once(s,
'''            public int Phase;   // 0 density, 1 continuous cells/decorations, 2 faceted planes
            public int Cursor;''',
'''            public int Phase;   // 0 snapshot, 1 jobs, 2 faceted job, 3 profiles, 4 seams, 5 result append
            public int Cursor;''', 'phase documentation')

s = once(s,
'''        private NativeList<uint> _facetedIndices;

        private readonly float[] _cellDensity''',
'''        private NativeList<uint> _facetedIndices;
        private const int AppendElementsPerDeadlineCheck = 512;
        private int _resultAppendStage;
        private int _topologyAppendVertexCursor;
        private int _topologyAppendIndexCursor;
        private uint _topologyAppendVertexBase;
        private int _facetedAppendVertexCursor;
        private int _facetedAppendIndexCursor;
        private uint _facetedAppendVertexBase;
        private bool _transitionResultPending;
        private int _transitionAppendVertexCursor;
        private int _transitionAppendIndexCursor;
        private uint _transitionAppendVertexBase;

        private readonly float[] _cellDensity''', 'result append state')

old_phase1 = '''                if (_build.Phase == 1)
                {
                    if (!_topologyCompactJobHandle.IsCompleted
                        || !_facetedMergeJobHandle.IsCompleted) break;
                    _topologyCompactJobHandle.Complete();
                    _facetedMergeJobHandle.Complete();
                    _densityTurnaroundTiming.Add(ElapsedMs(_build.DensityScheduledSeconds));
                    _topologyTurnaroundTiming.Add(ElapsedMs(_build.TopologyScheduledSeconds));
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _densityJobScheduled = false;
                    _topologyJobScheduled = false;
                    _topologyCompactJobScheduled = false;
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    CompactTopology(voxelSize);
                    AppendFacetedTopology();
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 2)
                {
                    if (!_facetedMaskJobScheduled) ScheduleFacetedMaskJob();
                    if (!_facetedMergeJobScheduled) ScheduleFacetedMergeJob(voxelSize);
                    if (!_facetedMergeJobHandle.IsCompleted) break;
                    _facetedMergeJobHandle.Complete();
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    AppendFacetedTopology();
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 3)'''
new_phase1 = '''                if (_build.Phase == 1)
                {
                    if (!_topologyCompactJobHandle.IsCompleted
                        || !_facetedMergeJobHandle.IsCompleted) break;
                    _topologyCompactJobHandle.Complete();
                    _facetedMergeJobHandle.Complete();
                    _densityTurnaroundTiming.Add(ElapsedMs(_build.DensityScheduledSeconds));
                    _topologyTurnaroundTiming.Add(ElapsedMs(_build.TopologyScheduledSeconds));
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _densityJobScheduled = false;
                    _topologyJobScheduled = false;
                    _topologyCompactJobScheduled = false;
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    BeginCompletedResultAppend(includeTopology: true);
                    _build.Phase = 5;
                    continue;
                }

                if (_build.Phase == 2)
                {
                    if (!_facetedMaskJobScheduled) ScheduleFacetedMaskJob();
                    if (!_facetedMergeJobScheduled) ScheduleFacetedMergeJob(voxelSize);
                    if (!_facetedMergeJobHandle.IsCompleted) break;
                    _facetedMergeJobHandle.Complete();
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    BeginCompletedResultAppend(includeTopology: false);
                    _build.Phase = 5;
                    continue;
                }

                if (_build.Phase == 5)
                {
                    if (!StepCompletedResultAppend(deadline)) break;
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 3)'''
s = once(s, old_phase1, new_phase1, 'budget result append phases')

# Replace whole-result topology copier with sliced append helpers.
start = s.index('        private void CompactTopology(float voxelSize)')
end = s.index('        private void ScheduleFacetedMaskJob(', start)
helpers = r'''        private void BeginCompletedResultAppend(bool includeTopology)
        {
            _resultAppendStage = includeTopology ? 0 : 1;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _topologyAppendVertexBase = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _facetedAppendVertexBase = 0;
        }

        private bool StepCompletedResultAppend(double deadlineSeconds)
        {
            if (_resultAppendStage == 0)
            {
                double start = Time.realtimeSinceStartupAsDouble;
                using var scope = s_CompactMarker.Auto();
                int overflowCell = _topologyOverflowCell[0];
                if (overflowCell >= 0)
                    throw new InvalidOperationException(
                        $"Continuous topology output overflow in chunk {_build.Coordinate}, "
                      + $"cell {overflowCell}; refusing to publish partial geometry.");

                if (!StepAppendNativeGeometry(_compactedTopologyVertices.AsArray(),
                                              _compactedTopologyIndices.AsArray(),
                                              ref _topologyAppendVertexCursor,
                                              ref _topologyAppendIndexCursor,
                                              ref _topologyAppendVertexBase,
                                              deadlineSeconds))
                {
                    LastTopologyCompactMs = ElapsedMs(start);
                    _topologyCompactTiming.Add(LastTopologyCompactMs);
                    return false;
                }

                if (_topologyOutput.IsCreated) _topologyOutput.Dispose();
                _topologyOutput = default;
                LastTopologyCompactMs = ElapsedMs(start);
                _topologyCompactTiming.Add(LastTopologyCompactMs);
                _resultAppendStage = 1;
            }

            if (_resultAppendStage == 1)
            {
                double start = Time.realtimeSinceStartupAsDouble;
                using var scope = s_FacetedMergeMarker.Auto();
                if (!StepAppendNativeGeometry(_facetedVertices.AsArray(),
                                              _facetedIndices.AsArray(),
                                              ref _facetedAppendVertexCursor,
                                              ref _facetedAppendIndexCursor,
                                              ref _facetedAppendVertexBase,
                                              deadlineSeconds))
                {
                    _facetedMergeTiming.Add(ElapsedMs(start));
                    return false;
                }
                _facetedMergeTiming.Add(ElapsedMs(start));
                _resultAppendStage = 2;
            }
            return true;
        }

        private bool StepAppendNativeGeometry(NativeArray<SmoothSurfaceVertex> sourceVertices,
                                              NativeArray<uint> sourceIndices,
                                              ref int vertexCursor, ref int indexCursor,
                                              ref uint vertexBase,
                                              double deadlineSeconds)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) return false;
            if (vertexCursor == 0 && indexCursor == 0)
                vertexBase = (uint)_vertices.Length;

            while (vertexCursor < sourceVertices.Length)
            {
                int end = math.min(sourceVertices.Length,
                                   vertexCursor + AppendElementsPerDeadlineCheck);
                for (; vertexCursor < end; vertexCursor++)
                    _vertices.Add(sourceVertices[vertexCursor]);
                if (vertexCursor < sourceVertices.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }

            while (indexCursor < sourceIndices.Length)
            {
                int end = math.min(sourceIndices.Length,
                                   indexCursor + AppendElementsPerDeadlineCheck);
                for (; indexCursor < end; indexCursor++)
                    _indices.Add(vertexBase + sourceIndices[indexCursor]);
                if (indexCursor < sourceIndices.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }
            return true;
        }

'''
s = s[:start] + helpers + s[end:]

# Delete the old whole-result faceted append helper.
start = s.index('        private void AppendFacetedTopology()')
end = s.index('        private void MergeAllFacetedMasks(', start)
s = s[:start] + s[end:]

# Transition result append is likewise resumable. Keep the job completed flag distinct from
# result-pending so eviction never waits merely because CPU copies are unfinished.
old_transition_completion = '''            if (_transitionJobScheduled)
            {
                if (!_transitionJobHandle.IsCompleted) return false;

                // Completion is non-blocking because IsCompleted was observed above. It is
                // still required before reading the NativeLists written by the job.
                _transitionJobHandle.Complete();
                _transitionJobScheduled = false;

                uint vertexBase = (uint)_vertices.Length;
                NativeArray<SmoothSurfaceVertex> vertices = _transitionVertices.AsArray();
                for (int i = 0; i < vertices.Length; i++) _vertices.Add(vertices[i]);
                NativeArray<uint> indices = _transitionIndices.AsArray();
                for (int i = 0; i < indices.Length; i++)
                    _indices.Add(vertexBase + indices[i]);

                _build.Cursor = _transitionFace + 1;
                _transitionFace = -1;
                _transitionSampleCursor = 0;
            }

            Vector3 cameraPosition = camera.transform.position;'''
new_transition_completion = '''            if (_transitionJobScheduled)
            {
                if (!_transitionJobHandle.IsCompleted) return false;

                // Completion is non-blocking because IsCompleted was observed above. The result
                // is now CPU-owned, but merging it into final output is itself budgeted.
                _transitionJobHandle.Complete();
                _transitionJobScheduled = false;
                _transitionResultPending = true;
                _transitionAppendVertexCursor = 0;
                _transitionAppendIndexCursor = 0;
                _transitionAppendVertexBase = 0;
            }

            if (_transitionResultPending)
            {
                if (!StepAppendNativeGeometry(_transitionVertices.AsArray(),
                                              _transitionIndices.AsArray(),
                                              ref _transitionAppendVertexCursor,
                                              ref _transitionAppendIndexCursor,
                                              ref _transitionAppendVertexBase,
                                              deadlineSeconds))
                    return false;

                _transitionResultPending = false;
                _build.Cursor = _transitionFace + 1;
                _transitionFace = -1;
                _transitionSampleCursor = 0;
            }

            Vector3 cameraPosition = camera.transform.position;'''
s = once(s, old_transition_completion, new_transition_completion, 'transition result slicing')

# Reset append state when a new build starts. This is the only path that should carry cursors
# across frames; a different generation must never inherit them.
s = once(s,
'''            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _build = new BuildState''',
'''            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _transitionResultPending = false;
            _resultAppendStage = 0;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _transitionAppendVertexCursor = 0;
            _transitionAppendIndexCursor = 0;
            _build = new BuildState''', 'new build append reset')

# ResetCompletedBuild is shared by success/stale paths.
s = once(s,
'''            _transitionFace = -1;
            _transitionSampleCursor = 0;
        }

        private void DropNoLongerResident''',
'''            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _transitionResultPending = false;
            _resultAppendStage = 0;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _transitionAppendVertexCursor = 0;
            _transitionAppendIndexCursor = 0;
        }

        private void DropNoLongerResident''', 'completed build append reset')

path.write_text(s)

# Guard the frame-path invariant at source level.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'CompletedJobResultsAreMergedUnderDeadline' in t:
    raise SystemExit('result slicing test already exists')
insert = r'''

        [Test]
        public void CompletedJobResultsAreMergedUnderDeadline()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("StepCompletedResultAppend(deadline)", cache);
            StringAssert.Contains("private bool StepAppendNativeGeometry", cache);
            StringAssert.Contains("AppendElementsPerDeadlineCheck", cache);
            StringAssert.Contains("_transitionResultPending", cache);
            StringAssert.DoesNotContain("private void CompactTopology", cache);
            StringAssert.DoesNotContain("private void AppendFacetedTopology", cache);
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

cache = path.read_text()
assert 'StepCompletedResultAppend(deadline)' in cache
assert 'private bool StepAppendNativeGeometry' in cache
assert 'private void CompactTopology' not in cache
assert 'private void AppendFacetedTopology' not in cache
assert '_transitionResultPending' in cache
