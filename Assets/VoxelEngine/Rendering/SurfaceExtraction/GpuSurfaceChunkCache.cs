using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
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
        public GpuSurfaceChunkCache Finer { get; set; }
        public GpuSurfaceChunkCache Coarser { get; set; }
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

        public void InvalidateDensityBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;
            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 chunk = BrickToChunk(brick);
                _known.Add(chunk);
                _dirty.Add(chunk);
            }
        }

        /// <summary>
        /// Adds chunks discovered from the resident brick-pointer grid, independently of density
        /// jobs. A surface brick can affect the neighbouring chunk when it lies on a chunk face,
        /// edge, or corner because extraction samples a one-sample halo. Add exactly those
        /// neighbours rather than relying on density-job side effects to discover geometry.
        /// </summary>
        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 chunk = BrickToChunk(brick);
                int rx = FloorMod(brick.x, BricksPerAxis);
                int ry = FloorMod(brick.y, BricksPerAxis);
                int rz = FloorMod(brick.z, BricksPerAxis);

                int minX = rx == 0 ? -1 : 0;
                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;
                int minY = ry == 0 ? -1 : 0;
                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;
                int minZ = rz == 0 ? -1 : 0;
                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int3 affected = chunk + new int3(x, y, z);
                    _known.Add(affected);
                    _dirty.Add(affected);
                }
            }
        }

        private int3 BrickToChunk(int3 brick) =>
            new(FloorDiv(brick.x, BricksPerAxis),
                FloorDiv(brick.y, BricksPerAxis),
                FloorDiv(brick.z, BricksPerAxis));

        public IReadOnlyList<Entry> Prepare(Vector3 cameraWorldPosition, float voxelSize,
                                            int frameIndex)
            => PrepareInternal(cameraWorldPosition, null, voxelSize, frameIndex);

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
                    if (Arena != null && !Arena.TryAcquire(out slot)) break;
                    entry = new Entry(coordinate, VoxelsPerAxis, SourceStep, Arena, slot);
                    _entries.Add(coordinate, entry);
                }

                _dirty.Remove(coordinate);
                entry.Ready = true;
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

        public bool IsReady(int3 coordinate)
            => _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        public bool IsVisible(int3 coordinate) => _visibleCoordinates.Contains(coordinate);
        public bool IsRetiredToFiner(int3 coordinate) => _retiredToFiner.Contains(coordinate);

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
            if (distance > Finer.MaxDistance || distance < Finer.MinDistance) return false;
            return CoveredByFiner(entry.Coordinate);
        }

        private bool CoveredByFiner(int3 coordinate)
        {
            if (Finer == null || Finer.VoxelsPerAxis >= VoxelsPerAxis) return false;
            int ratio = VoxelsPerAxis / Finer.VoxelsPerAxis;
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
                                               VoxelsPerAxis, targetViewport);
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
            if (RetentionScore(incomingCoordinate, cameraChunk, camera, voxelSize,
                               VoxelsPerAxis) >= farthest)
                return false;
            _entries.Remove(victim.Coordinate);
            victim.Dispose();
            return true;
        }

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

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
