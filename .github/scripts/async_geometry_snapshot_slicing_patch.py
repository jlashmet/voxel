from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = path.read_text()

s = once(s,
'''            public bool SnapshotTaken;
            public bool HasOwnedSolid;
            public bool RequiresContinuousTopology;
            public double BuildStartSeconds;''',
'''            public bool SnapshotTaken;
            public bool SnapshotInitialised;
            public int SnapshotCursor;
            public double SnapshotCpuMs;
            public bool HasOwnedSolid;
            public bool RequiresContinuousTopology;
            public double BuildStartSeconds;''', 'snapshot build state')

s = once(s,
'''            if (_build.Active && _build.SnapshotTaken
                && _build.MaterialPaletteVersion != palette.Version
                && (!_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    || desired <= _build.SourceVersion))
                Invalidate(_build.Coordinate);

            double deadline = Time.realtimeSinceStartupAsDouble''',
'''            if (_build.Active && _build.SnapshotTaken
                && _build.MaterialPaletteVersion != palette.Version
                && (!_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    || desired <= _build.SourceVersion))
                Invalidate(_build.Coordinate);

            // A sliced snapshot can become stale between frames. Unlike a scheduled Burst job it
            // owns no worker dependency yet, so abandon it immediately and let the newer dirty
            // generation restart rather than spending more budget on data we will reject anyway.
            if (_build.Active && !_build.SnapshotTaken
                && _desiredVersions.TryGetValue(_build.Coordinate, out ulong slicedDesired)
                && slicedDesired > _build.SourceVersion)
            {
                StaleBuildCount++;
                ResetCompletedBuild();
            }

            double deadline = Time.realtimeSinceStartupAsDouble''', 'abort stale sliced snapshot')

s = once(s,
'''                if (_build.Phase == 0)
                {
                    if (!_densityJobScheduled)
                        ScheduleDensityJob(source, in palette, voxelSize);

                    // Border invalidation intentionally discovers halo chunks.''',
'''                if (_build.Phase == 0)
                {
                    if (!_build.SnapshotTaken
                        && !StepDensitySnapshot(source, in palette, voxelSize, deadline))
                        break;

                    // Border invalidation intentionally discovers halo chunks.''',
'phase zero sliced snapshot')

# Replace both synchronous snapshot methods and the synchronous post-snapshot full-cache scan.
start = s.index('        /// <summary>\n        /// Snapshots one mip cell per lattice sample and schedules the coarse-ring density job.')
end = s.index('        private TransvoxelDensityBrick SnapshotBlock(', start)
replacement = r'''        private const int SnapshotBlocksPerDeadlineCheck = 8;
        private const int SnapshotMipSamplesPerDeadlineCheck = 64;

        /// <summary>
        /// Advances the authoritative-to-immutable snapshot boundary without ever walking a full
        /// chunk in one frame. The snapshot lives entirely in this workspace's persistent native
        /// buffers; borrowed Storage views are reacquired inside each slice and never survive the
        /// call. A later journal invalidation rejects the partial generation before publication.
        /// </summary>
        private bool StepDensitySnapshot(IRegionReadSource source,
                                         in MaterialPaletteView palette,
                                         float voxelSize, double deadlineSeconds)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) return false;
            return SamplesFromMips
                ? StepMipDensitySnapshot(source, in palette, voxelSize, deadlineSeconds)
                : StepExactDensitySnapshot(source, in palette, voxelSize, deadlineSeconds);
        }

        private bool StepExactDensitySnapshot(IRegionReadSource source,
                                              in MaterialPaletteView palette,
                                              float voxelSize, double deadlineSeconds)
        {
            double sliceStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();
            if (!_build.SnapshotInitialised)
            {
                _densityMixedVoxels.Clear();
                _densityMixedSurfaceSemantics.Clear();
                _densityMixedBoundarySamples.Clear();
                _buildSurfaceCatalogue = _surfaceCatalogue;
                _buildCoatingCatalogue = _coatingCatalogue;
                _buildPalette = palette;
                _build.MaterialPaletteVersion = palette.Version;
                _buildProfileBlocks = _profileBlocksByChunk.TryGetValue(
                    _build.Coordinate, out ProfileBlock[] blocks)
                    ? blocks : Array.Empty<ProfileBlock>();
                _build.SnapshotCursor = 0;
                _build.SnapshotCpuMs = 0.0;
                _build.HasOwnedSolid = false;
                _build.RequiresContinuousTopology = _buildProfileBlocks.Length > 0;
                _build.SnapshotInitialised = true;
            }

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int3 chunkBrickOrigin = new(chunkOriginVoxel.x >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.y >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.z >> VoxelReadGrid.BlockEdgeLog2);
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;
            RegionSampleCursor cursor = default;

            while (_build.SnapshotCursor < BrickCacheCount)
            {
                int batchEnd = math.min(BrickCacheCount,
                    _build.SnapshotCursor + SnapshotBlocksPerDeadlineCheck);
                for (; _build.SnapshotCursor < batchEnd; _build.SnapshotCursor++)
                {
                    int index = _build.SnapshotCursor;
                    int x = index % BrickCacheEdge;
                    int y = (index / BrickCacheEdge) % BrickCacheEdge;
                    int z = index / (BrickCacheEdge * BrickCacheEdge);
                    int3 worldBrick = cacheOrigin + new int3(x, y, z);
                    TransvoxelDensityBrick brick = SnapshotBlock(source, ref cursor, worldBrick);
                    _densityBricks[index] = brick;

                    bool ownsCore = x >= BrickCachePadding
                                  && y >= BrickCachePadding
                                  && z >= BrickCachePadding
                                  && x < BrickCachePadding + BricksPerAxis
                                  && y < BrickCachePadding + BricksPerAxis
                                  && z < BrickCachePadding + BricksPerAxis;
                    ClassifySnapshotBrick(in brick, ownsCore);
                }

                if (_build.SnapshotCursor < BrickCacheCount
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
            }

            _build.SnapshotTaken = true;
            AccumulateSnapshotSlice(sliceStart, completed: true);
            if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                return true;

            if (_build.RequiresContinuousTopology)
            {
                var job = new TransvoxelDensityJob
                {
                    Bricks = _densityBricks,
                    MixedVoxels = _densityMixedVoxels.AsArray(),
                    MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                    MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),
                    Palette = _buildPalette,
                    Catalogue = _buildSurfaceCatalogue,
                    Coatings = _buildCoatingCatalogue,
                    Density = _density,
                    Materials = _materials,
                    SurfaceSemantics = _surfaceSemantics,
                    BoundarySamples = _boundarySamples,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    BrickCacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    GridSize = GridSize,
                    Padding = Padding,
                    SourceStep = SourceStep
                };
                _build.DensityScheduledSeconds = Time.realtimeSinceStartupAsDouble;
                _densityJobHandle = job.Schedule(GridSampleCount, 64);
                _densityJobScheduled = true;
                ScheduleTopologyJob(voxelSize, _densityJobHandle);
                ScheduleFacetedMaskJob(_densityJobHandle);
                ScheduleFacetedMergeJob(voxelSize);
            }
            return true;
        }

        private void ClassifySnapshotBrick(in TransvoxelDensityBrick brick, bool ownsCore)
        {
            if (brick.Kind == 0) return;
            if (brick.Kind == 1)
            {
                if (!IsSolidSurfaceMaterial(brick.UniformMaterial)) return;
                if (ownsCore) _build.HasOwnedSolid = true;
                if (_build.RequiresContinuousTopology) return;
                SurfaceStyleReadDefinition style = _buildSurfaceCatalogue.Get(
                    _buildPalette.GetDefaultSurfaceStyle(brick.UniformMaterial));
                _build.RequiresContinuousTopology =
                    style.Reconstruction == SurfaceReconstruction.Smooth
                    || style.Reconstruction == SurfaceReconstruction.Rounded;
                return;
            }

            int endVoxel = brick.MixedOffset + VoxelReadGrid.VoxelsPerBlock;
            for (int voxel = brick.MixedOffset; voxel < endVoxel; voxel++)
            {
                byte material = _densityMixedVoxels[voxel];
                if (!IsSolidSurfaceMaterial(material)) continue;
                if (ownsCore) _build.HasOwnedSolid = true;
                if (_build.RequiresContinuousTopology) continue;

                uint surface = VoxelSurfaceSemantics.FromStorage(
                    _densityMixedSurfaceSemantics[voxel]).Packed;
                ushort styleId = (ushort)surface;
                if (styleId == SurfaceStyles.MaterialDefault)
                    styleId = _buildPalette.GetDefaultSurfaceStyle(material);
                SurfaceStyleReadDefinition style = _buildSurfaceCatalogue.Get(styleId);
                byte coating = (byte)(surface >> 16);
                _build.RequiresContinuousTopology = _densityMixedBoundarySamples[voxel] != 0
                    || _buildCoatingCatalogue.Get(coating).Displacement != 0
                    || style.Reconstruction == SurfaceReconstruction.Smooth
                    || style.Reconstruction == SurfaceReconstruction.Rounded;
            }
        }

        private bool StepMipDensitySnapshot(IRegionReadSource source,
                                            in MaterialPaletteView palette,
                                            float voxelSize, double deadlineSeconds)
        {
            double sliceStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();
            if (!_build.SnapshotInitialised)
            {
                _buildSurfaceCatalogue = _surfaceCatalogue;
                _buildCoatingCatalogue = _coatingCatalogue;
                _buildPalette = palette;
                _build.MaterialPaletteVersion = palette.Version;
                _buildProfileBlocks = Array.Empty<ProfileBlock>();
                _build.SnapshotCursor = 0;
                _build.SnapshotCpuMs = 0.0;
                _build.HasOwnedSolid = false;
                _build.RequiresContinuousTopology = false;
                _build.SnapshotInitialised = true;
            }

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int mipLevel = VoxelReadGrid.LevelForStride(SourceStep);
            RegionSampleCursor cursor = default;
            while (_build.SnapshotCursor < GridSampleCount)
            {
                int batchEnd = math.min(GridSampleCount,
                    _build.SnapshotCursor + SnapshotMipSamplesPerDeadlineCheck);
                for (; _build.SnapshotCursor < batchEnd; _build.SnapshotCursor++)
                {
                    int index = _build.SnapshotCursor;
                    int gx = index % GridSize;
                    int gy = (index / GridSize) % GridSize;
                    int gz = index / (GridSize * GridSize);
                    int3 voxel = chunkOriginVoxel
                               + (new int3(gx, gy, gz) - Padding) * SourceStep;

                    bool occupied = false;
                    byte material = VoxelGrid.MaterialEmpty;
                    if (TrySampleWorld(source, ref cursor, voxel, mipLevel,
                                       out bool sampled, out byte sampledMaterial))
                    {
                        occupied = sampled;
                        material = sampledMaterial;
                    }
                    _mipSampleOccupancy[index] = occupied ? (byte)1 : (byte)0;
                    _mipSampleMaterials[index] = material;
                    _build.HasOwnedSolid |= occupied && IsSolidSurfaceMaterial(material);
                }

                if (_build.SnapshotCursor < GridSampleCount
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
            }

            _build.SnapshotTaken = true;
            _build.RequiresContinuousTopology = _build.HasOwnedSolid;
            AccumulateSnapshotSlice(sliceStart, completed: true);
            if (!_build.HasOwnedSolid) return true;

            var job = new MipDensityJob
            {
                SampleOccupancy = _mipSampleOccupancy,
                SampleMaterials = _mipSampleMaterials,
                Palette = _buildPalette,
                Density = _density,
                Materials = _materials,
                SurfaceSemantics = _surfaceSemantics,
                BoundarySamples = _boundarySamples,
                GridSize = GridSize,
            };
            _build.DensityScheduledSeconds = Time.realtimeSinceStartupAsDouble;
            _densityJobHandle = job.Schedule(GridSampleCount, 256);
            _densityJobScheduled = true;
            ScheduleTopologyJob(voxelSize, _densityJobHandle);
            return true;
        }

        private void AccumulateSnapshotSlice(double sliceStart, bool completed)
        {
            _build.SnapshotCpuMs += ElapsedMs(sliceStart);
            if (!completed) return;
            LastSnapshotMs = _build.SnapshotCpuMs;
            _snapshotTiming.Add(LastSnapshotMs);
        }

'''
s = s[:start] + replacement + s[end:]

path.write_text(s)

# Architecture guard: the snapshot boundary must remain resumable and deadline-aware.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'AuthoritativeSnapshotAssemblyIsResumable' in t:
    raise SystemExit('snapshot slicing test already exists')
insert = r'''

        [Test]
        public void AuthoritativeSnapshotAssemblyIsResumable()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("private bool StepDensitySnapshot", cache);
            StringAssert.Contains("SnapshotCursor", cache);
            StringAssert.Contains("SnapshotBlocksPerDeadlineCheck", cache);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadlineSeconds", cache);
            StringAssert.DoesNotContain("private void ScheduleDensityJob", cache);
            StringAssert.DoesNotContain("private void ScheduleMipDensityJob", cache);
            StringAssert.DoesNotContain("private bool SnapshotCoreHasSolid", cache);
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

cache = path.read_text()
assert 'private bool StepDensitySnapshot' in cache
assert 'SnapshotBlocksPerDeadlineCheck' in cache
assert 'private void ScheduleDensityJob' not in cache
assert 'private void ScheduleMipDensityJob' not in cache
assert 'private bool SnapshotCoreHasSolid' not in cache
assert 'StepDensitySnapshot(source, in palette, voxelSize, deadline)' in cache
