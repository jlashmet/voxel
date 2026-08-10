using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Exact mesh generator for authored hard geometry.
    ///
    /// This is not a second renderer. It produces the same SmoothSurfaceVertex/index buffers as
    /// the smooth extractor and is drawn by the same material/depth pass. The only difference is
    /// the polygonizer: authored structure chunks use exact greedy voxel faces while natural
    /// chunks continue through the smooth extractor until Transvoxel replaces Surface Nets.
    ///
    /// Replacement meshes are built into scratch and swapped only when complete. A newly tagged
    /// hard chunk therefore keeps its old smooth representation until the exact replacement is
    /// ready; dirty hard chunks keep their previous exact mesh until the rebuild finishes. This is
    /// the same no-hole handoff invariant used by mature voxel LOD systems.
    /// </summary>
    public sealed class CpuHardSurfaceChunkCache : IDisposable
    {
        private const int MaterialCount = 18;
        private const int E = VoxelDimensions.BrickEdge;
        private const int BricksPerSlice = 512;
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

            internal Entry(int3 coordinate) => Coordinate = coordinate;

            internal void Upload(List<SmoothSurfaceVertex> vertices, List<uint> indices)
            {
                ComputeBuffer nextVertices = null;
                ComputeBuffer nextIndices = null;
                ComputeBuffer nextArgs = null;

                try
                {
                    int vertexCount = math.max(1, vertices.Count);
                    int indexCount = math.max(1, indices.Count);
                    nextVertices = new ComputeBuffer(vertexCount, SmoothSurfaceVertex.Stride,
                                                     ComputeBufferType.Structured);
                    nextIndices = new ComputeBuffer(indexCount, sizeof(uint),
                                                    ComputeBufferType.Structured);
                    nextArgs = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

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

            public Bounds WorldBounds(float voxelSize, int voxelsPerAxis)
            {
                float size = voxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f), Vector3.one * size);
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
            public int Cursor;
        }

        private readonly int _bricksPerAxis;
        private readonly int _voxelsPerAxis;
        private readonly int _chunkShift;
        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _knownHardChunks = new();
        private readonly HashSet<int3> _pending = new();
        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly List<SmoothSurfaceVertex> _vertices = new(16_384);
        private readonly List<uint> _indices = new(24_576);
        private readonly byte[] _brickMaterials = new byte[VoxelDimensions.VoxelsPerBrick];
        private readonly byte[] _mask = new byte[E * E];
        private BuildState _build;

        private static readonly int[] s_Strides = { 1, E, E * E };
        private static readonly int3[] s_BrickNeighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        public CpuHardSurfaceChunkCache(int bricksPerAxis)
        {
            if (bricksPerAxis <= 0 || (bricksPerAxis & (bricksPerAxis - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(bricksPerAxis),
                    "Hard-surface chunk size must be a power of two in bricks.");

            _bricksPerAxis = bricksPerAxis;
            _voxelsPerAxis = bricksPerAxis * VoxelDimensions.BrickEdge;
            _chunkShift = 0;
            for (int n = bricksPerAxis; n > 1; n >>= 1) _chunkShift++;
        }

        public IReadOnlyList<Entry> Visible => _visible;
        public int ResidentCount => _entries.Count;
        public int PendingCount => _pending.Count + (_build.Active ? 1 : 0);

        /// <summary>
        /// True only after an exact replacement mesh is complete. Until then the smooth chunk is
        /// deliberately left visible, preventing a representation swap from creating a hole.
        /// </summary>
        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        public void Sync(ref RegionTable table, in BrickPool pool,
                         HashSet<int3> dirtyRegions, Vector3 cameraWorldPosition,
                         float voxelSize, double budgetMs = 1.25)
        {
            DiscoverHardChunks(ref table);
            QueueDirtyRegions(dirtyRegions);
            DropNoLongerResident(ref table);

            double deadline = Time.realtimeSinceStartupAsDouble + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active && !BeginNearestBuild(cameraWorldPosition, voxelSize)) break;
                if (StepBuild(ref table, in pool)) FinishBuild();
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }

        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize)
        {
            _visible.Clear();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            foreach (Entry entry in _entries.Values)
            {
                if (!entry.Ready || entry.IndexCount == 0) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes,
                        entry.WorldBounds(voxelSize, _voxelsPerAxis)))
                    continue;
                _visible.Add(entry);
            }
            return _visible;
        }

        private void DiscoverHardChunks(ref RegionTable table)
        {
            var resident = table.GetResidentCoords(Allocator.Temp);
            for (int i = 0; i < resident.Length; i++)
            {
                int3 regionCoord = resident[i];
                if (!table.TryGetRegion(regionCoord, out Region region) || !region.HasHardSurfaceBricks())
                    continue;

                for (int wordIndex = 0; wordIndex < region.HardSurfaceWords.Length; wordIndex++)
                {
                    ulong word = region.HardSurfaceWords[wordIndex];
                    while (word != 0UL)
                    {
                        int bit = TrailingZeroCount(word);
                        int brickIndex = (wordIndex << 6) + bit;
                        int bx = brickIndex & VoxelDimensions.RegionEdgeMask;
                        int by = (brickIndex >> VoxelDimensions.RegionEdgeLog2)
                               & VoxelDimensions.RegionEdgeMask;
                        int bz = brickIndex >> (VoxelDimensions.RegionEdgeLog2 * 2);
                        int3 worldBrick = regionCoord * VoxelDimensions.RegionEdge
                                        + new int3(bx, by, bz);
                        int3 chunk = new(worldBrick.x >> _chunkShift,
                                         worldBrick.y >> _chunkShift,
                                         worldBrick.z >> _chunkShift);
                        if (_knownHardChunks.Add(chunk)) _pending.Add(chunk);
                        word &= word - 1UL;
                    }
                }
            }
            resident.Dispose();
        }

        private void QueueDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions == null || dirtyRegions.Count == 0 || _knownHardChunks.Count == 0)
                return;

            int chunksPerRegion = VoxelDimensions.RegionEdge / _bricksPerAxis;
            int chunkRegionShift = 0;
            for (int n = chunksPerRegion; n > 1; n >>= 1) chunkRegionShift++;

            foreach (int3 chunk in _knownHardChunks)
            {
                int3 ownerRegion = new(chunk.x >> chunkRegionShift,
                                       chunk.y >> chunkRegionShift,
                                       chunk.z >> chunkRegionShift);
                foreach (int3 dirtyRegion in dirtyRegions)
                {
                    int3 delta = math.abs(ownerRegion - dirtyRegion);
                    if (math.max(delta.x, math.max(delta.y, delta.z)) > 1) continue;
                    _pending.Add(chunk);
                    break;
                }
            }
        }

        private void DropNoLongerResident(ref RegionTable table)
        {
            if (_knownHardChunks.Count == 0) return;
            int chunksPerRegion = VoxelDimensions.RegionEdge / _bricksPerAxis;
            int regionShift = 0;
            for (int n = chunksPerRegion; n > 1; n >>= 1) regionShift++;

            List<int3> gone = null;
            foreach (int3 chunk in _knownHardChunks)
            {
                int3 region = new(chunk.x >> regionShift, chunk.y >> regionShift, chunk.z >> regionShift);
                if (table.IsResident(region)) continue;
                (gone ??= new List<int3>()).Add(chunk);
            }

            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++)
            {
                int3 chunk = gone[i];
                _knownHardChunks.Remove(chunk);
                _pending.Remove(chunk);
                if (_entries.TryGetValue(chunk, out Entry entry))
                {
                    entry.Dispose();
                    _entries.Remove(chunk);
                }
                if (_build.Active && _build.Coordinate.Equals(chunk))
                {
                    _build = default;
                    _vertices.Clear();
                    _indices.Clear();
                }
            }
        }

        private bool BeginNearestBuild(Vector3 cameraWorldPosition, float voxelSize)
        {
            if (_pending.Count == 0) return false;

            int3 best = default;
            float bestDistance = float.PositiveInfinity;
            float chunkMetres = _voxelsPerAxis * voxelSize;
            foreach (int3 candidate in _pending)
            {
                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }

            _pending.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _build = new BuildState { Active = true, Coordinate = best, Cursor = 0 };
            return true;
        }

        private bool StepBuild(ref RegionTable table, in BrickPool pool)
        {
            int total = _bricksPerAxis * _bricksPerAxis * _bricksPerAxis;
            int end = math.min(total, _build.Cursor + BricksPerSlice);
            int3 chunkBrickOrigin = _build.Coordinate * _bricksPerAxis;

            for (int i = _build.Cursor; i < end; i++)
            {
                int bx = i % _bricksPerAxis;
                int by = (i / _bricksPerAxis) % _bricksPerAxis;
                int bz = i / (_bricksPerAxis * _bricksPerAxis);
                int3 worldBrick = chunkBrickOrigin + new int3(bx, by, bz);

                if (!TryGetBrick(ref table, worldBrick, out BrickRef brick) || brick.IsEmpty)
                    continue;
                if (brick.IsUniform && !IsRenderableSolid(brick.UniformMaterial))
                    continue;
                if (brick.IsUniform && NeighboursAllRenderableSolid(ref table, worldBrick))
                    continue;

                LoadBrickMaterials(in pool, brick);
                EmitBrick(ref table, in pool, worldBrick * E);
            }

            _build.Cursor = end;
            return end >= total;
        }

        private void FinishBuild()
        {
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            entry.Upload(_vertices, _indices);
            _build = default;
            _vertices.Clear();
            _indices.Clear();
        }

        private static bool TryGetBrick(ref RegionTable table, int3 worldBrick, out BrickRef brick)
        {
            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region))
            {
                brick = BrickRef.Empty;
                return false;
            }

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            brick = region.GetBrick(bx, by, bz);
            return true;
        }

        private static bool NeighboursAllRenderableSolid(ref RegionTable table, int3 worldBrick)
        {
            for (int i = 0; i < s_BrickNeighbours.Length; i++)
            {
                if (!TryGetBrick(ref table, worldBrick + s_BrickNeighbours[i], out BrickRef neighbour))
                    return false;
                if (!neighbour.IsUniform || !IsRenderableSolid(neighbour.UniformMaterial))
                    return false;
            }
            return true;
        }

        private void LoadBrickMaterials(in BrickPool pool, BrickRef brick)
        {
            if (brick.IsUniform)
            {
                byte material = brick.UniformMaterial;
                for (int i = 0; i < _brickMaterials.Length; i++) _brickMaterials[i] = material;
                return;
            }

            int offset = pool.VoxelOffset(brick.PoolIndex);
            for (int i = 0; i < _brickMaterials.Length; i++)
                _brickMaterials[i] = pool.Voxels[offset + i];
        }

        private void EmitBrick(ref RegionTable table, in BrickPool pool, int3 brickBaseVoxel)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;

                for (int sign = -1; sign <= 1; sign += 2)
                for (int layer = 0; layer < E; layer++)
                {
                    BuildMask(ref table, in pool, brickBaseVoxel,
                              axis, axisA, axisB, sign, layer);
                    MergeMask(brickBaseVoxel, axis, axisA, axisB, sign, layer);
                }
            }
        }

        private void BuildMask(ref RegionTable table, in BrickPool pool, int3 brickBaseVoxel,
                               int axis, int axisA, int axisB, int sign, int layer)
        {
            int strideAxis = s_Strides[axis];
            int strideA = s_Strides[axisA];
            int strideB = s_Strides[axisB];
            int neighbourLayer = layer + sign;
            bool crossesBrick = (uint)neighbourLayer >= E;
            int layerBase = layer * strideAxis;

            for (int b = 0; b < E; b++)
            for (int a = 0; a < E; a++)
            {
                int index = layerBase + b * strideB + a * strideA;
                byte material = _brickMaterials[index];
                if (!IsRenderableSolid(material))
                {
                    _mask[a + b * E] = 0;
                    continue;
                }

                bool neighbourSolid;
                if (!crossesBrick)
                {
                    neighbourSolid = IsRenderableSolid(
                        _brickMaterials[index + sign * strideAxis]);
                }
                else
                {
                    int3 local = int3.zero;
                    local[axis] = neighbourLayer;
                    local[axisA] = a;
                    local[axisB] = b;
                    byte neighbourMaterial = VoxelAccess.GetVoxel(ref table, in pool,
                                                                   brickBaseVoxel + local);
                    neighbourSolid = IsRenderableSolid(neighbourMaterial);
                }

                _mask[a + b * E] = neighbourSolid ? (byte)0 : ClampMaterial(material);
            }
        }

        private void MergeMask(int3 brickBaseVoxel, int axis, int axisA, int axisB,
                               int sign, int layer)
        {
            for (int b = 0; b < E; b++)
            for (int a = 0; a < E; a++)
            {
                byte material = _mask[a + b * E];
                if (material == 0) continue;

                int width = 1;
                while (a + width < E && _mask[a + width + b * E] == material) width++;

                int height = 1;
                bool extend = true;
                while (b + height < E && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (_mask[a + k + (b + height) * E] == material) continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }

                for (int hb = 0; hb < height; hb++)
                for (int ha = 0; ha < width; ha++)
                    _mask[a + ha + (b + hb) * E] = 0;

                EmitQuad(material, brickBaseVoxel, axis, axisA, axisB, sign, layer,
                         a, b, width, height);
            }
        }

        private void EmitQuad(byte material, int3 brickBaseVoxel,
                              int axis, int axisA, int axisB, int sign, int layer,
                              int a, int b, int width, int height)
        {
            int planeVoxel = brickBaseVoxel[axis] + layer + (sign > 0 ? 1 : 0);
            int a0 = brickBaseVoxel[axisA] + a;
            int b0 = brickBaseVoxel[axisB] + b;

            Vector3 p0 = Corner(axis, axisA, axisB, planeVoxel, a0, b0);
            Vector3 p1 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0);
            Vector3 p2 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0 + height);
            Vector3 p3 = Corner(axis, axisA, axisB, planeVoxel, a0, b0 + height);
            Vector3 normal = Vector3.zero;
            normal[axis] = sign;

            uint m = material;
            uint baseIndex = (uint)_vertices.Count;
            _vertices.Add(new SmoothSurfaceVertex { Position = p0, Normal = normal, Material = m, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = p1, Normal = normal, Material = m, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = p2, Normal = normal, Material = m, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = p3, Normal = normal, Material = m, Active = FullyLitOcclusion });

            bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                _indices.Add(baseIndex); _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 1);
                _indices.Add(baseIndex); _indices.Add(baseIndex + 3); _indices.Add(baseIndex + 2);
            }
            else
            {
                _indices.Add(baseIndex); _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
                _indices.Add(baseIndex); _indices.Add(baseIndex + 2); _indices.Add(baseIndex + 3);
            }
        }

        private static Vector3 Corner(int axis, int axisA, int axisB, int plane, int a, int b)
        {
            Vector3 v = Vector3.zero;
            v[axis] = plane * 0.1f;
            v[axisA] = a * 0.1f;
            v[axisB] = b * 0.1f;
            return v;
        }

        private static bool IsRenderableSolid(byte material) =>
            material != VoxelDimensions.MaterialEmpty && material != 11 && material != 16;

        private static byte ClampMaterial(byte material) => material < MaterialCount ? material : (byte)1;

        private static int TrailingZeroCount(ulong value)
        {
            int count = 0;
            if ((value & 0xFFFFFFFFUL) == 0UL) { count += 32; value >>= 32; }
            if ((value & 0xFFFFUL) == 0UL) { count += 16; value >>= 16; }
            if ((value & 0xFFUL) == 0UL) { count += 8; value >>= 8; }
            if ((value & 0xFUL) == 0UL) { count += 4; value >>= 4; }
            if ((value & 0x3UL) == 0UL) { count += 2; value >>= 2; }
            if ((value & 0x1UL) == 0UL) count++;
            return count;
        }

        public void Dispose()
        {
            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _knownHardChunks.Clear();
            _pending.Clear();
            _visible.Clear();
            _vertices.Clear();
            _indices.Clear();
            _build = default;
        }
    }
}
