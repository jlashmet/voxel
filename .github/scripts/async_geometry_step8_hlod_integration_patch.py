from pathlib import Path


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


workspace_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs')
w = workspace_path.read_text()

w = once(w,
'''        internal readonly NativeArray<byte> SnapshotClassificationFlags;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'''        internal readonly NativeArray<byte> SnapshotClassificationFlags;

        // Step-8 feature-preserving HLOD scratch. These arrays exist only on the outer exact
        // ring; finer Transvoxel workers pay no memory cost for the coarse representation.
        internal readonly NativeArray<SurfaceBlockHlodSummary> HlodSummaries;
        internal readonly NativeArray<byte> HlodMaskScratch;
        internal readonly NativeArray<int> HlodOverflow;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'workspace HLOD fields')

w = once(w,
'''        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, int cellsPerAxis,
                                          int faceSamplesPerAxis)
''',
'''        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, bool usesBlockHlod,
                                          int hlodCoreBrickEdge, int cellsPerAxis,
                                          int faceSamplesPerAxis)
''',
'workspace ctor signature')

w = once(w,
'''            else
            {
                ExactMixedFlags = default;
                ExactMixedBrickIndices = default;
                SnapshotClassificationFlags = default;
            }

            CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(''',
'''            else
            {
                ExactMixedFlags = default;
                ExactMixedBrickIndices = default;
                SnapshotClassificationFlags = default;
            }

            if (usesBlockHlod)
            {
                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(
                    brickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                int subcellEdge = hlodCoreBrickEdge
                                * SurfaceBlockHlodMeshJob.SubcellsPerBrickAxis;
                HlodMaskScratch = new NativeArray<byte>(
                    subcellEdge * subcellEdge, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                HlodOverflow = new NativeArray<int>(1, Allocator.Persistent,
                                                    NativeArrayOptions.ClearMemory);
            }
            else
            {
                HlodSummaries = default;
                HlodMaskScratch = default;
                HlodOverflow = default;
            }

            CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(''',
'workspace HLOD allocation')

w = once(w,
'''            Vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);
            Indices = new NativeList<uint>(49_152, Allocator.Persistent);
''',
'''            // The HLOD worker meshes a 128^3 subcell volume. Keep its output fixed-capacity and
            // comfortably below the shared GPU arena ceiling so Burst can use AddNoResize and
            // report overflow instead of growing native memory on the frame path.
            int finalVertexCapacity = usesBlockHlod ? 262_144 : 32_768;
            int finalIndexCapacity = usesBlockHlod ? 393_216 : 49_152;
            Vertices = new NativeList<SmoothSurfaceVertex>(finalVertexCapacity,
                                                           Allocator.Persistent);
            Indices = new NativeList<uint>(finalIndexCapacity, Allocator.Persistent);
''',
'workspace final HLOD output capacity')

w = once(w,
'''            if (SnapshotClassificationFlags.IsCreated) SnapshotClassificationFlags.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''',
'''            if (SnapshotClassificationFlags.IsCreated) SnapshotClassificationFlags.Dispose();
            if (HlodSummaries.IsCreated) HlodSummaries.Dispose();
            if (HlodMaskScratch.IsCreated) HlodMaskScratch.Dispose();
            if (HlodOverflow.IsCreated) HlodOverflow.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''',
'workspace HLOD dispose')
workspace_path.write_text(w)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''        public readonly bool SamplesFromMips;

        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools''',
'''        public readonly bool SamplesFromMips;
        /// <summary>
        /// Step 8 keeps the exact versioned/COW snapshot boundary but compresses each 8^3 block
        /// into eight spatial 4^3 HLOD subcells before meshing. This replaces the expensive exact
        /// Transvoxel fallback without ever treating Storage's any-solid block projection as
        /// render density.
        /// </summary>
        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;

        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools''',
'cache UsesBlockHlod property')

s = once(s,
'''        private NativeArray<byte> _snapshotClassificationFlags;
        private JobHandle _exactMetadataJobHandle;
''',
'''        private NativeArray<byte> _snapshotClassificationFlags;
        private NativeArray<SurfaceBlockHlodSummary> _hlodSummaries;
        private NativeArray<byte> _hlodMaskScratch;
        private NativeArray<int> _hlodOverflow;
        private JobHandle _hlodJobHandle;
        private bool _hlodJobScheduled;
        private JobHandle _exactMetadataJobHandle;
''',
'cache HLOD fields')

s = once(s,
'''            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips,
                CellsPerAxis, FaceSamplesPerAxis);
''',
'''            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,
                BricksPerAxis, CellsPerAxis, FaceSamplesPerAxis);
''',
'cache workspace ctor')

s = once(s,
'''            _snapshotClassificationFlags = _workspace.SnapshotClassificationFlags;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;
''',
'''            _snapshotClassificationFlags = _workspace.SnapshotClassificationFlags;
            _hlodSummaries = _workspace.HlodSummaries;
            _hlodMaskScratch = _workspace.HlodMaskScratch;
            _hlodOverflow = _workspace.HlodOverflow;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;
''',
'cache borrow HLOD scratch')

s = once(s,
'''        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled
                                   || _densityJobScheduled || _topologyJobScheduled
                                   || _facetedMaskJobScheduled || _transitionJobScheduled
                                    ? 1 : 0;
''',
'''        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled
                                   || _hlodJobScheduled || _densityJobScheduled
                                   || _topologyJobScheduled || _facetedMaskJobScheduled
                                   || _transitionJobScheduled
                                    ? 1 : 0;
''',
'cache running HLOD job metric')

s = once(s,
'''                    // Border invalidation intentionally discovers halo chunks. If the immutable
                    // snapshot proves this chunk owns no solid cells, publish a complete empty
                    // result without scanning/merging all 64^3 cells. Profile blocks still run
                    // because their authored geometry may overlap an otherwise empty core.
                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
''',
'''                    // Step 8 already scheduled its summary -> greedy HLOD dependency chain as
                    // part of the immutable exact snapshot. It bypasses Transvoxel density,
                    // faceted and transition phases and rejoins the normal profile/publication path
                    // only after the HLOD job is ready and its Storage pins are released.
                    if (UsesBlockHlod)
                    {
                        _build.Phase = 7;
                        continue;
                    }

                    // Border invalidation intentionally discovers halo chunks. If the immutable
                    // snapshot proves this chunk owns no solid cells, publish a complete empty
                    // result without scanning/merging all 64^3 cells. Profile blocks still run
                    // because their authored geometry may overlap an otherwise empty core.
                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
''',
'cache phase0 HLOD dispatch')

s = once(s,
'''                if (_build.Phase == 6)
                {
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    _build.Phase = 5;
                    continue;
                }
''',
'''                if (_build.Phase == 7)
                {
                    if (_hlodJobScheduled)
                    {
                        if (!_hlodJobHandle.IsCompleted) break;
                        if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                                _hlodJobHandle, ref _framePathBlockingCompletionViolations))
                            break;
                        _hlodJobScheduled = false;
                        _build.HasOwnedSolid = _indices.Length > 0;
                    }
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    if (_hlodOverflow[0] != 0)
                        throw new InvalidOperationException(
                            $"Feature-preserving HLOD output overflow in chunk {_build.Coordinate}; "
                          + "refusing to allocate or publish partial coarse geometry.");
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 6)
                {
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    _build.Phase = 5;
                    continue;
                }
''',
'cache HLOD phase7 completion')

s = once(s,
'''                    _profileEmitTiming.Add(ElapsedMs(profileStart));
                    if (!profilesDone) continue;

                    _build.Phase = 4;
''',
'''                    _profileEmitTiming.Add(ElapsedMs(profileStart));
                    if (!profilesDone) continue;

                    // The step-8 HLOD grid and the step-4 inner ring both resolve geometry on a
                    // four-voxel lattice. Do not feed faceted HLOD through Transvoxel transition
                    // cells; finish directly and let the visual LOD regression police the aligned
                    // boundary. If that test exposes a seam, add a dedicated HLOD boundary pass.
                    if (UsesBlockHlod)
                    {
                        FinishBuild(frame);
                        if (_pendingUpload) break;
                        continue;
                    }

                    _build.Phase = 4;
''',
'cache HLOD finish after profiles')

# Schedule HLOD immediately after exact metadata/pins are validated and region metadata is released,
# before the expensive exact classification/density pipeline.
s = once(s,
'''            ReleasePinnedRegionMetadataImmediate();

            if (!_exactClassificationJobScheduled)
''',
'''            ReleasePinnedRegionMetadataImmediate();

            if (UsesBlockHlod)
            {
                _hlodOverflow[0] = 0;
                JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob
                {
                    Bricks = _densityBricks,
                    MixedVoxels = PinnedMixedVoxelsOrFallback(),
                    Summaries = _hlodSummaries,
                }.Schedule(BrickCacheCount, 256);
                _hlodJobHandle = new SurfaceBlockHlodMeshJob
                {
                    Summaries = _hlodSummaries,
                    SummaryGridEdge = BrickCacheEdge,
                    PaddingBricks = BrickCachePadding,
                    CoreBrickEdge = BricksPerAxis,
                    CoreOriginVoxel = chunkOriginVoxel,
                    VoxelSize = voxelSize,
                    MaskScratch = _hlodMaskScratch,
                    Vertices = _vertices,
                    Indices = _indices,
                    Overflow = _hlodOverflow,
                }.Schedule(summaryHandle);
                _hlodJobScheduled = true;
                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion
                _build.RequiresContinuousTopology = false;
                _build.SnapshotTaken = true;
                _exactMetadataReady = false;
                _exactMixedPinCursor = 0;
                AccumulateSnapshotSlice(sliceStart, completed: true);
                return true;
            }

            if (!_exactClassificationJobScheduled)
''',
'cache HLOD scheduling')

s = once(s,
'''            if (_exactClassificationJobScheduled && !_exactClassificationJobHandle.IsCompleted)
                return false;
            if (_densityJobScheduled && !_densityJobHandle.IsCompleted) return false;
''',
'''            if (_exactClassificationJobScheduled && !_exactClassificationJobHandle.IsCompleted)
                return false;
            if (_hlodJobScheduled && !_hlodJobHandle.IsCompleted) return false;
            if (_densityJobScheduled && !_densityJobHandle.IsCompleted) return false;
''',
'cache ScheduledJobsComplete HLOD')

s = once(s,
'''            if (_exactClassificationJobScheduled)
            {
                _exactClassificationJobHandle.Complete(); // teardown may synchronize
                _exactClassificationJobScheduled = false;
            }
            if (_densityJobScheduled)
''',
'''            if (_exactClassificationJobScheduled)
            {
                _exactClassificationJobHandle.Complete(); // teardown may synchronize
                _exactClassificationJobScheduled = false;
            }
            if (_hlodJobScheduled)
            {
                _hlodJobHandle.Complete(); // teardown may synchronize
                _hlodJobScheduled = false;
            }
            if (_densityJobScheduled)
''',
'cache CompleteJobs HLOD')

s = once(s,
'''            if (_pinnedReadBlocks.Length != 0 || _pinnedRegionCount != 0
                || _exactMetadataJobScheduled || _exactClassificationJobScheduled)
''',
'''            if (_pinnedReadBlocks.Length != 0 || _pinnedRegionCount != 0
                || _exactMetadataJobScheduled || _exactClassificationJobScheduled
                || _hlodJobScheduled)
''',
'cache reset HLOD guard')
cache_path.write_text(s)


ring_path = Path('Assets/Tests/EditMode/SurfaceRingBandTests.cs')
r = ring_path.read_text()
r = once(r,
'''        public void RenderRingsUseExactStepEightAndMipsBeyondIt()
        {
            // The dividing line is one brick: a stride finer than a brick has no mip level.
''',
'''        public void RenderRingsUseFeaturePreservingStepEightAndMipsBeyondIt()
        {
            // Step 8 keeps exact COW Storage inputs but no longer runs exact Transvoxel. It
            // compresses those inputs into spatial 4^3 HLOD subcells; coarser experimental rings
            // beyond step 8 may still consume the conventional mip pyramid.
''',
'ring test rename')
r = once(r,
'''            using (var coarse = new CpuTransvoxelChunkCache(8))
                Assert.IsFalse(coarse.SamplesFromMips,
                    "Step 8 must stay exact: conservative any-solid block summaries are not render density.");
''',
'''            using (var coarse = new CpuTransvoxelChunkCache(8))
            {
                Assert.IsFalse(coarse.SamplesFromMips,
                    "Step 8 must not use conservative any-solid block summaries as render density.");
                Assert.IsTrue(coarse.UsesBlockHlod,
                    "Step 8 must derive its coarse mesh from feature-preserving exact block inputs.");
            }
''',
'ring test HLOD assertion')
ring_path.write_text(r)


lod_path = Path('Assets/Tests/PlayMode/LodRenderingTests.cs')
l = lod_path.read_text()
l = once(l,
'''        public void StepEightUsesFeaturePreservingVoxelSamples()
        {
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8),
                "Step 8 must not turn an any-solid 8^3 storage block into a render sample.");
            using var cache = new CpuTransvoxelChunkCache(8);
            Assert.False(cache.SamplesFromMips,
                "The castle's 288-420m LOD must preserve voxel features rather than OR-collapsing them.");
        }
''',
'''        public void StepEightUsesFeaturePreservingBlockHlod()
        {
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8),
                "Step 8 must not turn an any-solid 8^3 storage block into a render sample.");
            using var cache = new CpuTransvoxelChunkCache(8);
            Assert.False(cache.SamplesFromMips,
                "The castle's outer LOD must never use OR-collapsed Storage occupancy as density.");
            Assert.True(cache.UsesBlockHlod,
                "Step 8 must mesh spatial 4^3 HLOD subcells derived from exact COW inputs.");
        }
''',
'LOD step8 HLOD assertion')
lod_path.write_text(l)


arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'StepEightHlodRunsAsReadinessGatedBurstJobs' in a:
    raise SystemExit('HLOD integration architecture test already exists')
addition = r'''

        [Test]
        public void StepEightHlodRunsAsReadinessGatedBurstJobs()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string workspace = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "TransvoxelBuildWorkspace.cs"));
            string summary = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "SurfaceBlockHlodSummaryJob.cs"));
            string mesh = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceBlockHlodMeshJob.cs"));

            StringAssert.Contains("public bool UsesBlockHlod", cache);
            StringAssert.Contains("new SurfaceBlockHlodSummaryJob", cache);
            StringAssert.Contains("new SurfaceBlockHlodMeshJob", cache);
            StringAssert.Contains(".Schedule(summaryHandle)", cache);
            StringAssert.Contains("if (!_hlodJobHandle.IsCompleted) break;", cache);
            StringAssert.Contains("GeometryFrameJobCompletionGuard.TryCompleteReady", cache);
            StringAssert.Contains("_hlodOverflow[0]", cache);
            StringAssert.Contains("HlodSummaries", workspace);
            StringAssert.Contains("HlodMaskScratch", workspace);
            StringAssert.Contains("usesBlockHlod ? 262_144 : 32_768", workspace);
            StringAssert.Contains("[BurstCompile]", summary);
            StringAssert.Contains("[BurstCompile]", mesh);
            StringAssert.Contains("AddNoResize", mesh);
            StringAssert.DoesNotContain(".Run();", cache);
        }
'''
marker = '\n\n        [Test]\n        public void CoarseExactSamplingUsesFewerBuildWorkspaces()'
if marker not in a:
    raise SystemExit('architecture insertion marker missing')
a = a.replace(marker, addition + marker, 1)
arch_path.write_text(a)

# Final static guards.
cache = cache_path.read_text()
workspace = workspace_path.read_text()
assert 'UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge' in cache
assert 'new SurfaceBlockHlodSummaryJob' in cache
assert 'new SurfaceBlockHlodMeshJob' in cache
assert 'if (!_hlodJobHandle.IsCompleted) break;' in cache
assert 'usesBlockHlod ? 262_144 : 32_768' in workspace
assert '.Run();' not in cache[cache.index('if (UsesBlockHlod)'):cache.index('private void ScheduleTopologyJob')]
print('step-8 HLOD integration patch applied')
