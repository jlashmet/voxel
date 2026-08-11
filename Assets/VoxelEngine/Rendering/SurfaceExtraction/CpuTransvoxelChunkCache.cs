using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.SurfaceExtraction.Transvoxel;
using VoxelEngine.Rendering.Vegetation;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// CPU-authored regular Transvoxel mesh cache for smooth volumetric geometry.
    ///
    /// This first milestone intentionally runs one resolution everywhere: 32 regular cells at a
    /// four-voxel (40 cm) sample step, giving the same 12.8 m physical chunk grid as the recovery
    /// Surface Nets cache but twice its linear sample density. Keeping one level means there is no
    /// LOD boundary yet; transition cells are the next milestone after this regular-cell path has
    /// been runtime-validated.
    ///
    /// Hard semantic bricks are treated as empty by this field. That allows a crisp wall and
    /// smooth terrain to coexist inside the same render chunk without double-rendering the wall.
    /// Both layers still use the same SmoothSurfaceVertex/indirect draw contract and raster pass.
    /// </summary>
    public sealed class CpuTransvoxelChunkCache : IDisposable
    {
        public const int CellsPerAxis = 32;
        public const int SourceStep = 4;
        public const int VoxelsPerAxis = CellsPerAxis * SourceStep;
        public const int BricksPerAxis = VoxelsPerAxis / VoxelDimensions.BrickEdge;

        private const int Padding = 1;
        private const int GridSize = CellsPerAxis + 3;
        private const int GridSampleCount = GridSize * GridSize * GridSize;
        private const int CellsPerSlice = 512;
        private const int BrickCachePadding = 1;
        private const int BrickCacheEdge = BricksPerAxis + BrickCachePadding * 2;
        private const int BrickCacheCount = BrickCacheEdge * BrickCacheEdge * BrickCacheEdge;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");

        public sealed class Entry : IDisposable
        {
            public readonly int3 Coordinate;
            public ComputeBuffer Vertices;
            public ComputeBuffer Indices;
            public ComputeBuffer Args;
            public bool Ready;
            public int IndexCount;
            public int LastUsedFrame;

            internal Entry(int3 coordinate) => Coordinate = coordinate;

            internal void Upload(List<SmoothSurfaceVertex> vertices, List<uint> indices)
            {
                ComputeBuffer nextVertices = null;
                ComputeBuffer nextIndices = null;
                ComputeBuffer nextArgs = null;

                try
                {
                    nextVertices = new ComputeBuffer(math.max(1, vertices.Count),
                                                     SmoothSurfaceVertex.Stride,
                                                     ComputeBufferType.Structured);
                    nextIndices = new ComputeBuffer(math.max(1, indices.Count), sizeof(uint),
                                                    ComputeBufferType.Structured);
                    nextArgs = new ComputeBuffer(4, sizeof(uint),
                                                 ComputeBufferType.IndirectArguments);
                    if (vertices.Count > 0) nextVertices.SetData(vertices);
                    if (indices.Count > 0) nextIndices.SetData(indices);
                    nextArgs.SetData(new uint[] { (uint)indices.Count, 1u, 0u, 0u });
                }
                catch
                {
                    nextVertices?.Release();
                    nextIndices?.Release();
                    nextArgs?.Release();
                    throw;
                }

                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = nextVertices;
                Indices = nextIndices;
                Args = nextArgs;
                IndexCount = indices.Count;
                Ready = true;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f),
                                  Vector3.one * (size + SourceStep * voxelSize * 2f));
            }

            public void Draw(CommandBuffer commandBuffer, Material material,
                             MaterialPropertyBlock properties)
            {
                if (!Ready || IndexCount == 0 || Vertices == null || Indices == null || Args == null)
                    return;

                properties.SetBuffer(s_SurfaceVertices, Vertices);
                properties.SetBuffer(s_SurfaceIndices, Indices);
                properties.SetInt(s_SurfaceIndexBase, 0);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, Args, 0, properties);
            }

            public void Dispose()
            {
                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = null;
                Indices = null;
                Args = null;
                Ready = false;
                IndexCount = 0;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Phase;   // 0 density job, 1 regular cells
            public int Cursor;
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _known = new();
        private readonly HashSet<int3> _dirty = new();
        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private readonly NativeArray<float> _density;
        private readonly NativeArray<byte> _materials;
        private NativeArray<TransvoxelDensityBrick> _densityBricks;
        private NativeList<byte> _densityMixedVoxels;
        private JobHandle _densityJobHandle;
        private bool _densityJobScheduled;

        private readonly float[] _cellDensity = new float[8];
        private readonly byte[] _cellMaterial = new byte[8];
        private readonly List<SmoothSurfaceVertex> _vertices = new(16_384);
        private readonly List<uint> _indices = new(24_576);
        private BuildState _build;
        private int _treeRegistryVersion = int.MinValue;

        public CpuTransvoxelChunkCache()
        {
            _density = new NativeArray<float>(GridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            _materials = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                               NativeArrayOptions.UninitializedMemory);
            _densityBricks = new NativeArray<TransvoxelDensityBrick>(
                BrickCacheCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _densityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
        }

        public int MaxResidentChunks { get; set; } = 768;
        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public IReadOnlyList<Entry> Visible => _visible;

        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        /// <summary>
        /// Discovers/invalidates chunks from the same surface-brick stream used by the previous
        /// GPU cache. The one-sample Transvoxel padding can consume a neighbouring chunk's edge,
        /// so face/edge/corner neighbours are dirtied only when the brick lies on a chunk border.
        /// </summary>
        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),
                                     FloorDiv(brick.y, BricksPerAxis),
                                     FloorDiv(brick.z, BricksPerAxis));
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
                    int3 chunk = baseChunk + new int3(x, y, z);
                    _known.Add(chunk);
                    _dirty.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Called before the GPU uploader consumes the shared dirty-region set. Existing known
        /// chunks in or adjacent to an edited region are rebuilt; the old ready mesh stays alive
        /// until the replacement is uploaded.
        /// </summary>
        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions == null || dirtyRegions.Count == 0 || _known.Count == 0) return;

            List<int3> affected = null;
            foreach (int3 chunk in _known)
            {
                int3 ownerRegion = ChunkRegion(chunk);
                foreach (int3 dirtyRegion in dirtyRegions)
                {
                    int3 delta = math.abs(ownerRegion - dirtyRegion);
                    if (math.max(delta.x, math.max(delta.y, delta.z)) > 1) continue;
                    (affected ??= new List<int3>()).Add(chunk);
                    break;
                }
            }

            if (affected == null) return;
            for (int i = 0; i < affected.Count; i++) _dirty.Add(affected[i]);
        }

        public void Prepare(ref RegionTable table, in BrickPool pool, Camera camera,
                            float voxelSize, int frame, double budgetMs = 0.20)
        {
            DropNoLongerResident(ref table);
            EnforceCapacity(camera, voxelSize);

            // Tree semantics can arrive a few frames after the first terrain chunks because the
            // legacy showcase migration waits for authored planting voxels. Rebuild known smooth
            // chunks once when that semantic snapshot changes so an already-cached old crown
            // cannot survive underneath the procedural tree. Damage does not change Version, so
            // this is not part of the contact hot path.
            int treeRegistryVersion = ProceduralTreeRegistry.Version;
            if (_treeRegistryVersion != treeRegistryVersion)
            {
                _treeRegistryVersion = treeRegistryVersion;
                foreach (int3 chunk in _known) _dirty.Add(chunk);
            }

            if (camera == null || _dirty.Count == 0 && !_build.Active) return;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active && !BeginNearestBuild(camera.transform.position, voxelSize)) break;

                if (_build.Phase == 0)
                {
                    if (!_densityJobScheduled)
                        ScheduleDensityJob(ref table, in pool);

                    // Never wait for worker threads on the render thread. A later frame will see
                    // completion and continue directly into polygonization.
                    if (!_densityJobHandle.IsCompleted) break;

                    _densityJobHandle.Complete();
                    _densityJobScheduled = false;
                    _build.Phase = 1;
                    _build.Cursor = 0;
                    continue;
                }

                if (StepCells(voxelSize)) FinishBuild(frame);
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }

        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)
        {
            _visible.Clear();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            foreach (Entry entry in _entries.Values)
            {
                if (!entry.Ready || entry.IndexCount == 0) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, entry.WorldBounds(voxelSize)))
                    continue;
                entry.LastUsedFrame = frame;
                _visible.Add(entry);
            }
            return _visible;
        }

        private bool BeginNearestBuild(Vector3 cameraWorldPosition, float voxelSize)
        {
            if (_dirty.Count == 0) return false;

            int3 best = default;
            float bestDistance = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            foreach (int3 candidate in _dirty)
            {
                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }

            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _build = new BuildState { Active = true, Coordinate = best, Phase = 0, Cursor = 0 };
            return true;
        }

        /// <summary>
        /// Resolves the 18^3 brick neighbourhood around the chunk once and copies only mixed
        /// voxel payloads. This snapshot is immutable until the Burst density job completes, so
        /// gameplay may continue editing/evicting authoritative storage without racing the job.
        /// </summary>
        private void ScheduleDensityJob(ref RegionTable table, in BrickPool pool)
        {
            _densityMixedVoxels.Clear();

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int3 chunkBrickOrigin = new(chunkOriginVoxel.x >> VoxelDimensions.BrickEdgeLog2,
                                        chunkOriginVoxel.y >> VoxelDimensions.BrickEdgeLog2,
                                        chunkOriginVoxel.z >> VoxelDimensions.BrickEdgeLog2);
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;

            for (int z = 0; z < BrickCacheEdge; z++)
            for (int y = 0; y < BrickCacheEdge; y++)
            for (int x = 0; x < BrickCacheEdge; x++)
            {
                int cacheIndex = x + BrickCacheEdge * (y + BrickCacheEdge * z);
                int3 worldBrick = cacheOrigin + new int3(x, y, z);
                _densityBricks[cacheIndex] = SnapshotBrick(ref table, in pool, worldBrick);
            }

            var job = new TransvoxelDensityJob
            {
                Bricks = _densityBricks,
                MixedVoxels = _densityMixedVoxels.AsArray(),
                Density = _density,
                Materials = _materials,
                ChunkOriginVoxel = chunkOriginVoxel,
                BrickCacheOrigin = cacheOrigin,
                BrickCacheEdge = BrickCacheEdge,
                GridSize = GridSize,
                Padding = Padding,
                SourceStep = SourceStep
            };

            _densityJobHandle = job.Schedule(GridSampleCount, 64);
            _densityJobScheduled = true;
        }

        private TransvoxelDensityBrick SnapshotBrick(ref RegionTable table, in BrickPool pool,
                                                      int3 worldBrick)
        {
            // Legacy showcase crowns are gameplay proxies only. They use grass/moss materials that
            // otherwise belong to terrain, so material alone cannot distinguish them from the
            // ground. Semantic crown ownership is the authoritative presentation exclusion.
            if (ProceduralTreeRegistry.IsLegacyHiddenSmoothBrick(worldBrick)) return default;

            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region)) return default;

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            int brickIndex = Region.BrickIndex(bx, by, bz);
            if (region.IsHardSurfaceBrick(brickIndex)) return default;

            BrickRef brick = region.BrickRefs[brickIndex];
            if (brick.IsUniform)
            {
                byte material = brick.UniformMaterial;
                if (material == VoxelDimensions.MaterialEmpty) return default;
                return new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = material,
                    MixedOffset = 0
                };
            }

            int mixedOffset = _densityMixedVoxels.Length;
            int nextLength = mixedOffset + VoxelDimensions.VoxelsPerBrick;
            _densityMixedVoxels.ResizeUninitialized(nextLength);
            NativeArray<byte> packed = _densityMixedVoxels.AsArray();
            int sourceOffset = pool.VoxelOffset(brick.PoolIndex);
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
                packed[mixedOffset + i] = pool.Voxels[sourceOffset + i];

            return new TransvoxelDensityBrick
            {
                Kind = 2,
                UniformMaterial = 0,
                MixedOffset = mixedOffset
            };
        }

        private bool StepCells(float voxelSize)
        {
            int cellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            int end = math.min(cellCount, _build.Cursor + CellsPerSlice);
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;

            for (int cellIndex = _build.Cursor; cellIndex < end; cellIndex++)
            {
                int x = cellIndex % CellsPerAxis;
                int y = (cellIndex / CellsPerAxis) % CellsPerAxis;
                int z = cellIndex / (CellsPerAxis * CellsPerAxis);
                PolygoniseCell(chunkOrigin, new int3(x, y, z), voxelSize);
            }

            _build.Cursor = end;
            return end >= cellCount;
        }

        private void PolygoniseCell(int3 chunkOrigin, int3 cell, float voxelSize)
        {
            int caseCode = 0;
            for (int i = 0; i < 8; i++)
            {
                int3 grid = cell + Padding + TransvoxelRegularTables.CornerOffsets[i];
                int sampleIndex = GridIndex(grid.x, grid.y, grid.z);
                float density = _density[sampleIndex];
                _cellDensity[i] = density;
                _cellMaterial[i] = _materials[sampleIndex];
                if (density < 0f) caseCode |= 1 << i;
            }

            if (caseCode == 0 || caseCode == 255) return;

            RegularCellData cellData =
                TransvoxelRegularTables.CellData[TransvoxelRegularTables.CellClass[caseCode]];
            ushort[] edgeCodes = TransvoxelRegularTables.VertexData[caseCode];
            int vertexCount = cellData.VertexCount;
            uint baseVertex = (uint)_vertices.Count;

            for (int i = 0; i < vertexCount; i++)
            {
                ushort edgeCode = edgeCodes[i];
                int corner0 = (edgeCode >> 4) & 0x0F;
                int corner1 = edgeCode & 0x0F;
                float d0 = _cellDensity[corner0];
                float d1 = _cellDensity[corner1];

                int3 o0 = TransvoxelRegularTables.CornerOffsets[corner0];
                int3 o1 = TransvoxelRegularTables.CornerOffsets[corner1];
                float t0 = math.abs(d1 - d0) > 1e-7f ? d1 / (d1 - d0) : 0.5f;
                float t1 = 1f - t0;
                float3 localVoxel = ((float3)(cell + o0) * t0
                                   + (float3)(cell + o1) * t1) * SourceStep;
                float3 worldVoxel = chunkOrigin + localVoxel + 0.5f;

                int3 g0 = cell + Padding + o0;
                int3 g1 = cell + Padding + o1;
                float3 n0 = DensityNormal(g0);
                float3 n1 = DensityNormal(g1);
                float3 normal = math.normalizesafe(n0 * t0 + n1 * t1,
                                                    new float3(0f, 1f, 0f));

                byte material = d0 < d1 ? _cellMaterial[corner0] : _cellMaterial[corner1];
                if (!IsSmoothFieldMaterial(material)) material = 1;

                _vertices.Add(new SmoothSurfaceVertex
                {
                    Position = (Vector3)(worldVoxel * voxelSize),
                    Normal = (Vector3)normal,
                    Material = material,
                    Active = FullyLitOcclusion
                });
            }

            int indexCount = cellData.TriangleCount * 3;
            byte[] localIndices = cellData.VertexIndices;
            for (int i = 0; i < indexCount; i++)
                _indices.Add(baseVertex + localIndices[i]);
        }

        private float3 DensityNormal(int3 grid)
        {
            float x = DensityAtGrid(grid.x - 1, grid.y, grid.z)
                    - DensityAtGrid(grid.x + 1, grid.y, grid.z);
            float y = DensityAtGrid(grid.x, grid.y - 1, grid.z)
                    - DensityAtGrid(grid.x, grid.y + 1, grid.z);
            float z = DensityAtGrid(grid.x, grid.y, grid.z - 1)
                    - DensityAtGrid(grid.x, grid.y, grid.z + 1);
            return math.normalizesafe(new float3(x, y, z), new float3(0f, 1f, 0f));
        }

        private float DensityAtGrid(int x, int y, int z)
        {
            x = math.clamp(x, 0, GridSize - 1);
            y = math.clamp(y, 0, GridSize - 1);
            z = math.clamp(z, 0, GridSize - 1);
            return _density[GridIndex(x, y, z)];
        }

        private static bool IsSmoothFieldMaterial(byte material) =>
            material == 1 || material == 3 || material == 5 || material == 6
            || material == 10 || material == 13 || material == 14;

        private void FinishBuild(int frame)
        {
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            entry.Upload(_vertices, _indices);
            entry.LastUsedFrame = frame;
            _build = default;
            _vertices.Clear();
            _indices.Clear();
        }

        private void DropNoLongerResident(ref RegionTable table)
        {
            if (_known.Count == 0) return;
            List<int3> gone = null;
            foreach (int3 chunk in _known)
            {
                if (table.IsResident(ChunkRegion(chunk))) continue;
                (gone ??= new List<int3>()).Add(chunk);
            }

            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++) RemoveChunk(gone[i]);
        }

        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            while (_entries.Count >= MaxResidentChunks && _dirty.Count > 0)
            {
                int3 victim = default;
                float farthest = -1f;
                Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
                float chunkMetres = VoxelsPerAxis * voxelSize;

                foreach (var pair in _entries)
                {
                    Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                    + Vector3.one * 0.5f) * chunkMetres;
                    float distance = (centre - cameraPosition).sqrMagnitude;
                    if (distance <= farthest) continue;
                    farthest = distance;
                    victim = pair.Key;
                }

                if (farthest < 0f) break;
                if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();
                _entries.Remove(victim);
                // Keep it known. If it becomes relevant again, discovery or an edit will dirty it.
            }
        }

        private void RemoveChunk(int3 chunk)
        {
            _known.Remove(chunk);
            _dirty.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                entry.Dispose();
                _entries.Remove(chunk);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                CompleteDensityJob();
                _build = default;
                _vertices.Clear();
                _indices.Clear();
            }
        }

        private void CompleteDensityJob()
        {
            if (!_densityJobScheduled) return;
            _densityJobHandle.Complete();
            _densityJobScheduled = false;
        }

        private static int3 ChunkRegion(int3 chunk)
        {
            // Four 12.8 m chunks fit one 51.2 m region along each axis.
            return new int3(chunk.x >> 2, chunk.y >> 2, chunk.z >> 2);
        }

        private static int GridIndex(int x, int y, int z) =>
            x + GridSize * (y + GridSize * z);

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        public void Dispose()
        {
            CompleteDensityJob();
            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _known.Clear();
            _dirty.Clear();
            _visible.Clear();
            _vertices.Clear();
            _indices.Clear();
            if (_density.IsCreated) _density.Dispose();
            if (_materials.IsCreated) _materials.Dispose();
            if (_densityBricks.IsCreated) _densityBricks.Dispose();
            if (_densityMixedVoxels.IsCreated) _densityMixedVoxels.Dispose();
            _build = default;
        }
    }
}
