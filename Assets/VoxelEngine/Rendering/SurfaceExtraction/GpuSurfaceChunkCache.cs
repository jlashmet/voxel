using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Incremental resident cache for GPU-authored continuous surface chunks.
    ///
    /// The mid-field cache owns 32^3 authoritative voxel cells and samples the filtered density
    /// every two voxels with one negative sample halo. Its 18^3 lattice owns exactly the sampled
    /// edges [base, base + 30]. The next chunk begins at base + 32, so vertices agree across the
    /// seam while triangles are never emitted twice. A denser near-field level is layered later.
    /// </summary>
    public sealed class GpuSurfaceChunkCache : IDisposable
    {
        public const int DefaultBricksPerAxis = 4;
        public const int DefaultSourceStep = 2;
        public const int DefaultMaxIndicesPerChunk = 24576;

        public sealed class Entry : IDisposable
        {
            private readonly int _voxelsPerAxis;

            internal Entry(int3 coordinate, int voxelsPerAxis, int sourceStep,
                           GpuSurfaceArena arena, int slot)
            {
                Coordinate = coordinate;
                _voxelsPerAxis = voxelsPerAxis;
                SampleOrigin = coordinate * voxelsPerAxis - sourceStep;
                Extractor = arena != null
                    ? new GpuSurfaceExtractor(arena, slot)
                    : new GpuSurfaceExtractor();
            }

            public int3 Coordinate { get; }
            public GpuSurfaceExtractor Extractor { get; }
            public bool Ready { get; internal set; }
            public int LastUsedFrame { get; internal set; }

            public int3 SampleOrigin { get; }

            public Bounds WorldBounds(float voxelSize)
            {
                Vector3 centre = new Vector3(Coordinate.x + 0.5f, Coordinate.y + 0.5f,
                                             Coordinate.z + 0.5f)
                               * (_voxelsPerAxis * voxelSize);
                // Slight inflation prevents interpolation at a chunk face being culled due to
                // floating-point disagreement with the camera plane.
                float size = _voxelsPerAxis * voxelSize + voxelSize * 2f;
                return new Bounds(centre, Vector3.one * size);
            }

            public void Dispose() => Extractor.Dispose();
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _known = new();
        private readonly HashSet<int3> _dirty = new();
        private readonly List<Entry> _scheduled = new();
        private readonly List<Entry> _visible = new();
        private readonly HashSet<int3> _visibleCoordinates = new();
        private readonly HashSet<int3> _retiredToFiner = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        public GpuSurfaceChunkCache(int bricksPerAxis = DefaultBricksPerAxis,
                                    int sourceStep = DefaultSourceStep)
        {
            if (bricksPerAxis < 1) throw new ArgumentOutOfRangeException(nameof(bricksPerAxis));
            if (sourceStep < 1) throw new ArgumentOutOfRangeException(nameof(sourceStep));
            int voxels = checked(bricksPerAxis * 8);
            if (voxels % sourceStep != 0)
                throw new ArgumentException("Chunk voxels must divide evenly by the source step.");
            BricksPerAxis = bricksPerAxis;
            VoxelsPerAxis = voxels;
            SourceStep = sourceStep;
            GridSamplesPerAxis = voxels / sourceStep + 2;
        }

        public int BricksPerAxis { get; }
        public int VoxelsPerAxis { get; }
        public int SourceStep { get; }
        public int GridSamplesPerAxis { get; }

        /// <summary>
        /// A finer level whose chunks subdivide this one. Where the finer level has every child
        /// of a chunk resident, this level stops drawing it.
        ///
        /// Without this the two levels overlap: they are selected by distance to chunk centre, so
        /// around the changeover a coarse chunk and the fine chunks inside it both pass their
        /// range test and both draw, at mismatched resolutions, z-fighting along every shared
        /// face. Chunk sizes are arranged to divide evenly (16 voxels into 32), so a coarse chunk
        /// is covered by exactly <c>ratio^3</c> finer chunks and coverage is an exact test rather
        /// than a distance heuristic.
        /// </summary>
        public GpuSurfaceChunkCache Finer { get; set; }

        /// <summary>
        /// The coarser level that owns the choice between it and this one. Set on the finer cache
        /// so the decision is made exactly once, per coarse chunk, rather than independently at
        /// both levels by distance — which left the transition band drawing both, at mismatched
        /// resolutions, z-fighting along every shared face.
        /// </summary>
        public GpuSurfaceChunkCache Coarser { get; set; }

        /// <summary>
        /// Preallocated home for this level's geometry. Residency is capped by the arena's slot
        /// count when one is attached, so the memory cost of this cache is a number chosen once
        /// rather than an emergent property of how far the camera has flown.
        /// </summary>
        public GpuSurfaceArena Arena { get; set; }

        public int MaxResidentChunks { get; set; } = 512;
        public int MaxBuildsPerFrame { get; set; } = 12;
        public int MaxIndicesPerChunk { get; set; } = DefaultMaxIndicesPerChunk;
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; } = float.PositiveInfinity;
        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        public int DirtyCount => _dirty.Count;
        public IReadOnlyList<Entry> Scheduled => _scheduled;
        public IReadOnlyList<Entry> Visible => _visible;

        /// <summary>
        /// Invalidates from the exact density jobs uploaded by <see cref="VoxelGpuBuffers"/>.
        /// Those jobs already include the 3x3x3 density-filter halo, so a boundary edit dirties
        /// both continuous chunks that consume the changed samples.
        /// </summary>
        public void InvalidateDensityBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;
            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 chunk = new(FloorDiv(brick.x, BricksPerAxis),
                                 FloorDiv(brick.y, BricksPerAxis),
                                 FloorDiv(brick.z, BricksPerAxis));
                _known.Add(chunk);
                _dirty.Add(chunk);
            }
        }

        /// <summary>Selects a bounded nearest-first extraction batch for this frame.</summary>
        public IReadOnlyList<Entry> Prepare(Vector3 cameraWorldPosition, float voxelSize,
                                            int frameIndex)
            => PrepareInternal(cameraWorldPosition, null, voxelSize, frameIndex);

        /// <summary>
        /// Camera-aware variant used by the showcase. Screen-centre chunks are more important
        /// than equally distant geometry behind the player; distance-only residency was filling
        /// the cache with ground around a hero camera while the castle remained on the blocky
        /// fallback path.
        /// </summary>
        public IReadOnlyList<Entry> Prepare(Camera camera, float voxelSize, int frameIndex)
            => PrepareInternal(camera != null ? camera.transform.position : Vector3.zero,
                               camera, voxelSize, frameIndex);

        private IReadOnlyList<Entry> PrepareInternal(Vector3 cameraWorldPosition, Camera camera,
                                                     float voxelSize, int frameIndex)
        {
            _scheduled.Clear();
            if (!(voxelSize > 0f) || MaxBuildsPerFrame <= 0 || MaxResidentChunks <= 0)
                return _scheduled;

            Vector3 cameraChunk = cameraWorldPosition / (VoxelsPerAxis * voxelSize);
            for (int build = 0; build < MaxBuildsPerFrame; build++)
            {
                Vector2 targetViewport = TargetViewport(frameIndex, build);
                bool found = TryFindNearest(_dirty, cameraChunk, camera, voxelSize,
                                            targetViewport, requireMissing: false,
                                            out int3 coordinate);
                if (!found)
                    found = TryFindNearest(_known, cameraChunk, camera, voxelSize,
                                           targetViewport, requireMissing: true, out coordinate);
                if (!found) break;

                if (!_entries.TryGetValue(coordinate, out Entry entry))
                {
                    if (!TryMakeRoom(coordinate, cameraChunk, camera, voxelSize)) break;
                    int slot = -1;
                    // Eviction returns a slot, so this only fails when every slot is held by a
                    // chunk that TryMakeRoom judged more valuable than the incoming one.
                    if (Arena != null && !Arena.TryAcquire(out slot)) break;
                    entry = new Entry(coordinate, VoxelsPerAxis, SourceStep, Arena, slot);
                    _entries.Add(coordinate, entry);
                }

                _dirty.Remove(coordinate);
                entry.Ready = true; // extraction precedes its draw in the recorded GPU stream
                entry.LastUsedFrame = frameIndex;
                _scheduled.Add(entry);
            }

            return _scheduled;
        }

        public void RecordScheduled(CommandBuffer commandBuffer, ComputeShader shader,
                                    VoxelGpuBuffers source, float voxelSize)
        {
            var grid = new int3(GridSamplesPerAxis);
            for (int i = 0; i < _scheduled.Count; i++)
            {
                Entry entry = _scheduled[i];
                entry.Extractor.RecordExtractSparse(commandBuffer, shader, source, grid,
                    entry.SampleOrigin, voxelSize, maxIndexCount: MaxIndicesPerChunk,
                    sourceStep: SourceStep);
            }
        }

        /// <summary>Builds a stable frustum-culled draw list before recording the raster pass.</summary>
        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frameIndex)
        {
            _visible.Clear();
            _visibleCoordinates.Clear();
            _retiredToFiner.Clear();
            if (camera == null) return _visible;
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (Entry entry in _entries.Values)
            {
                float distance = Vector3.Distance(camera.transform.position,
                                                  entry.WorldBounds(voxelSize).center);
                // Exactly one level owns each patch of ground. A coarse chunk retires only when
                // it is inside the finer level's range and every one of its children is ready to
                // take over; the finer level draws only where its parent actually retired.
                if (RetireToFiner(entry, distance))
                {
                    _retiredToFiner.Add(entry.Coordinate);
                    continue;
                }
                if (Coarser != null && !Coarser.IsRetiredToFiner(ParentCoordinate(entry.Coordinate)))
                    continue;
                if (!entry.Ready || !GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, entry.WorldBounds(voxelSize))
                    || distance < MinDistance || distance > MaxDistance)
                    continue;
                entry.LastUsedFrame = frameIndex;
                _visible.Add(entry);
                _visibleCoordinates.Add(entry.Coordinate);
            }
            return _visible;
        }

        /// <summary>True when this coordinate holds extracted geometry ready to draw.</summary>
        public bool IsReady(int3 coordinate)
            => _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        /// <summary>True when this coordinate was selected for drawing by the last collection.</summary>
        public bool IsVisible(int3 coordinate) => _visibleCoordinates.Contains(coordinate);

        /// <summary>True when this coarse coordinate handed its ground to the finer level.</summary>
        public bool IsRetiredToFiner(int3 coordinate) => _retiredToFiner.Contains(coordinate);

        private static readonly int3[] FaceNeighbours =
        {
            new int3(1, 0, 0), new int3(-1, 0, 0),
            new int3(0, 1, 0), new int3(0, -1, 0),
            new int3(0, 0, 1), new int3(0, 0, -1),
        };

        private bool NeighbourPending(int3 coordinate)
        {
            for (int i = 0; i < FaceNeighbours.Length; i++)
            {
                int3 neighbour = coordinate + FaceNeighbours[i];
                // Unknown neighbours are air or beyond residency; both render fine through the
                // raymarch and the far-field continuation, so only a known-but-unextracted
                // neighbour can leave a visible cross-section.
                if (_known.Contains(neighbour) && !IsReady(neighbour))
                    return true;
            }
            return false;
        }

        private int3 ParentCoordinate(int3 coordinate)
        {
            if (Coarser == null) return coordinate;
            int ratio = Coarser.VoxelsPerAxis / VoxelsPerAxis;
            if (ratio <= 1) return coordinate;
            return new int3(FloorDiv(coordinate.x, ratio), FloorDiv(coordinate.y, ratio),
                            FloorDiv(coordinate.z, ratio));
        }

        private bool RetireToFiner(Entry entry, float distance)
        {
            if (Finer == null || Finer.VoxelsPerAxis >= VoxelsPerAxis) return false;
            // Outside the finer level's own draw range it has nothing to hand over to.
            if (distance > Finer.MaxDistance || distance < Finer.MinDistance) return false;
            return CoveredByFiner(entry.Coordinate);
        }

        private bool CoveredByFiner(int3 coordinate)
        {
            if (Finer == null || Finer.VoxelsPerAxis >= VoxelsPerAxis) return false;
            int ratio = VoxelsPerAxis / Finer.VoxelsPerAxis;
            // Residency is not the test — visibility is. The finer level stops drawing beyond its
            // own MaxDistance while its chunks stay resident, so a residency test retired the
            // coarse chunk over ground nobody was drawing any more and punched a hole through the
            // world. That only shows up once the camera moves away from a region, which is why
            // the fixed views never caught it and the motion sequence did immediately.
            //
            // This requires the finer level to have been collected first; VoxelRenderPass orders
            // the two calls accordingly.
            int3 origin = coordinate * ratio;
            for (int z = 0; z < ratio; z++)
                for (int y = 0; y < ratio; y++)
                    for (int x = 0; x < ratio; x++)
                        if (!Finer.IsReady(origin + new int3(x, y, z)))
                            return false;
            return true;
        }

        private bool TryFindNearest(HashSet<int3> candidates, Vector3 cameraChunk, Camera camera,
                                    float voxelSize, Vector2 targetViewport, bool requireMissing,
                                    out int3 nearest)
        {
            nearest = default;
            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            foreach (int3 candidate in candidates)
            {
                if (requireMissing && _entries.ContainsKey(candidate)) continue;
                if (camera != null && !InDistanceRange(candidate, cameraChunk, voxelSize))
                    continue;
                float distance = PriorityScore(candidate, cameraChunk, camera, voxelSize,
                                               VoxelsPerAxis,
                                               targetViewport);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
                found = true;
            }
            return found;
        }

        private int ResidencyCeiling =>
            Arena != null ? Mathf.Min(MaxResidentChunks, Arena.SlotCount) : MaxResidentChunks;

        private bool TryMakeRoom(int3 incomingCoordinate, Vector3 cameraChunk, Camera camera,
                                 float voxelSize)
        {
            if (_entries.Count < ResidencyCeiling) return true;

            Entry victim = null;
            float farthest = float.NegativeInfinity;
            foreach (Entry candidate in _entries.Values)
            {
                if (_scheduled.Contains(candidate)) continue;
                float score = RetentionScore(candidate.Coordinate, cameraChunk, camera, voxelSize,
                                             VoxelsPerAxis)
                            + (Time.frameCount - candidate.LastUsedFrame) * 0.01f;
                if (score <= farthest) continue;
                farthest = score;
                victim = candidate;
            }

            if (victim == null) return false;
            // Once the nearest resident set is full, distant known chunks stay pending instead
            // of replacing a closer chunk and being replaced back on the next frame.
            if (RetentionScore(incomingCoordinate, cameraChunk, camera, voxelSize,
                               VoxelsPerAxis) >= farthest)
                return false;
            _entries.Remove(victim.Coordinate);
            victim.Dispose();
            return true;
        }

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

        private bool InDistanceRange(int3 coordinate, Vector3 cameraChunk, float voxelSize)
        {
            Vector3 chunkCentre = new Vector3(coordinate.x + 0.5f, coordinate.y + 0.5f,
                                              coordinate.z + 0.5f);
            float distance = (chunkCentre - cameraChunk).magnitude * VoxelsPerAxis * voxelSize;
            return distance >= MinDistance && distance <= MaxDistance;
        }

        private static float PriorityScore(int3 coordinate, Vector3 cameraChunk, Camera camera,
                                           float voxelSize, int voxelsPerAxis,
                                           Vector2 targetViewport)
        {
            Vector3 chunkCentre = new Vector3(coordinate.x + 0.5f, coordinate.y + 0.5f,
                                              coordinate.z + 0.5f);
            if (camera == null) return (chunkCentre - cameraChunk).sqrMagnitude;

            Vector3 worldCentre = chunkCentre * (voxelsPerAxis * voxelSize);
            Vector3 viewport = camera.WorldToViewportPoint(worldCentre);
            float dx = viewport.x - targetViewport.x;
            float dy = viewport.y - targetViewport.y;
            float radial = dx * dx + dy * dy;
            float offscreen = viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f
                           || viewport.y < -0.1f || viewport.y > 1.1f ? 1000000f : 0f;
            // Fill one screen tile at a time, front-to-back. A centre-only score built a narrow
            // tunnel of chunks through the castle while most visible pixels stayed on fallback.
            return offscreen + radial * 10000f + math.max(viewport.z, 0f) * 0.35f;
        }

        private static float RetentionScore(int3 coordinate, Vector3 cameraChunk, Camera camera,
                                            float voxelSize, int voxelsPerAxis)
        {
            Vector3 chunkCentre = new Vector3(coordinate.x + 0.5f, coordinate.y + 0.5f,
                                              coordinate.z + 0.5f);
            if (camera == null) return (chunkCentre - cameraChunk).sqrMagnitude;
            Vector3 viewport = camera.WorldToViewportPoint(
                chunkCentre * (voxelsPerAxis * voxelSize));
            bool visible = viewport.z > 0f && viewport.x >= -0.1f && viewport.x <= 1.1f
                        && viewport.y >= -0.1f && viewport.y <= 1.1f;
            return (visible ? 0f : 1000000f) + math.max(viewport.z, 0f);
        }

        private static Vector2 TargetViewport(int frameIndex, int buildIndex)
        {
            const int tilesX = 12;
            const int tilesY = 7;
            int tile = (frameIndex * 12 + buildIndex) % (tilesX * tilesY);
            return new Vector2(((tile % tilesX) + 0.5f) / tilesX,
                               ((tile / tilesX) + 0.5f) / tilesY);
        }

        public void Dispose()
        {
            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _known.Clear();
            _dirty.Clear();
            _scheduled.Clear();
            _visible.Clear();
            _visibleCoordinates.Clear();
            _retiredToFiner.Clear();
        }
    }
}
