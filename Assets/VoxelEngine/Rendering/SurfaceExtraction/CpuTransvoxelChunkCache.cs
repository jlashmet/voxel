using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// CPU-authored feature-aware mesh cache for all solid voxel geometry.
    ///
    /// The near-field extractor samples every authoritative voxel. Curvature is reconstructed
    /// from local style rules, while collision and destruction continue to use discrete cells.
    ///
    /// Every non-liquid solid participates in this field. Surface semantics, rather than a
    /// brick-wide renderer classifier, control local reconstruction.
    /// </summary>
    public sealed class CpuTransvoxelChunkCache : IDisposable
    {
        public const int CellsPerAxis = 32;
        public const int SourceStep = 1;
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
            public long GpuBytes { get; private set; }
            public ulong SourceVersion { get; internal set; }
            public uint MaterialPaletteVersion { get; internal set; }
            public uint SurfaceCatalogueVersion { get; internal set; }
            public ulong SurfaceCatalogueHash { get; internal set; }
            public uint CoatingCatalogueVersion { get; internal set; }
            public ulong CoatingCatalogueHash { get; internal set; }

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
                GpuBytes = (long)vertices.Count * SmoothSurfaceVertex.Stride
                         + (long)indices.Count * sizeof(uint) + 4L * sizeof(uint);
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
                GpuBytes = 0;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Phase;   // 0 density, 1 continuous cells/decorations, 2 faceted planes
            public int Cursor;
            public ulong SourceVersion;
            public uint MaterialPaletteVersion;
            public uint SurfaceCatalogueVersion;
            public ulong SurfaceCatalogueHash;
            public uint CoatingCatalogueVersion;
            public ulong CoatingCatalogueHash;
            public bool SnapshotTaken;
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _known = new();
        private readonly HashSet<int3> _dirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        private ulong _versionCounter;
        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private readonly NativeArray<float> _density;
        private readonly NativeArray<byte> _materials;
        private readonly NativeArray<uint> _surfaceSemantics;
        private readonly NativeArray<byte> _boundarySamples;
        private NativeArray<TransvoxelDensityBrick> _densityBricks;
        private NativeList<byte> _densityMixedVoxels;
        private NativeList<ushort> _densityMixedSurfaceSemantics;
        private NativeList<byte> _densityMixedBoundarySamples;
        private JobHandle _densityJobHandle;
        private bool _densityJobScheduled;

        private readonly float[] _cellDensity = new float[8];
        private readonly byte[] _cellMaterial = new byte[8];
        private readonly uint[] _cellSurface = new uint[8];
        private readonly byte[] _cellBoundary = new byte[8];
        private readonly SmoothSurfaceVertex[] _cellVertices = new SmoothSurfaceVertex[16];
        private readonly uint[] _faceMask = new uint[CellsPerAxis * CellsPerAxis];
        private readonly List<SmoothSurfaceVertex> _vertices = new(16_384);
        private readonly List<uint> _indices = new(24_576);
        private BuildState _build;
        private SurfaceCatalogue _surfaceCatalogue;
        private SurfaceCatalogue _buildSurfaceCatalogue;
        private CoatingCatalogue _coatingCatalogue;
        private CoatingCatalogue _buildCoatingCatalogue;
        private MaterialPalette _buildPalette;
        private uint _materialPaletteVersion;
        private ProfileBlock[] _profileBlocks = Array.Empty<ProfileBlock>();
        private ProfileBlockStore _profileBlockStore;
        private uint _profileBlockVersion;

        public CpuTransvoxelChunkCache()
        {
            _surfaceCatalogue = SurfaceCatalogue.CreateBuiltIns();
            _coatingCatalogue = CoatingCatalogue.CreateBuiltIns();
            _density = new NativeArray<float>(GridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            _materials = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                               NativeArrayOptions.UninitializedMemory);
            _surfaceSemantics = new NativeArray<uint>(GridSampleCount, Allocator.Persistent,
                                                      NativeArrayOptions.UninitializedMemory);
            _boundarySamples = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            _densityBricks = new NativeArray<TransvoxelDensityBrick>(
                BrickCacheCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _densityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            _densityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024, Allocator.Persistent);
            _densityMixedBoundarySamples = new NativeList<byte>(64 * 1024, Allocator.Persistent);
        }

        public int MaxResidentChunks { get; set; } = 4096;
        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        public ulong UploadedGeometryBytes { get; private set; }
        public ulong CompletedDecorationClumps { get; private set; }
        public long ResidentGpuBytes
        {
            get
            {
                long total = 0;
                foreach (Entry entry in _entries.Values) total += entry.GpuBytes;
                return total;
            }
        }
        public IReadOnlyList<Entry> Visible => _visible;

        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        /// <summary>
        /// Discovers/invalidates chunks from the scheduler's authoritative surface-brick stream.
        /// The one-sample Transvoxel padding can consume a neighbouring chunk's edge,
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
                    Invalidate(chunk);
                }
            }
        }

        /// <summary>
        /// Consumes scheduler-local regions derived from the versioned change journal. Existing
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
            for (int i = 0; i < affected.Count; i++) Invalidate(affected[i]);
        }

        public void Prepare(ref RegionTable table, in BrickPool pool, in MaterialPalette palette,
                            in SurfaceCatalogue surfaceCatalogue,
                            in CoatingCatalogue coatingCatalogue,
                            ProfileBlockStore profileBlocks,
                            Camera camera,
                            float voxelSize, int frame, double budgetMs = 0.20)
        {
            SetSurfaceCatalogue(in surfaceCatalogue);
            SetCoatingCatalogue(in coatingCatalogue);
            SetMaterialPaletteVersion(palette.Version);
            SetProfileBlocks(profileBlocks);
            DropNoLongerResident(ref table);
            EnforceCapacity(camera, voxelSize);

            if (camera == null || _dirty.Count == 0 && !_build.Active) return;

            if (_build.Active && _build.SnapshotTaken
                && _build.MaterialPaletteVersion != palette.Version
                && (!_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    || desired <= _build.SourceVersion))
                Invalidate(_build.Coordinate);

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active && !BeginNearestBuild(camera.transform.position, voxelSize)) break;

                if (_build.Phase == 0)
                {
                    if (!_densityJobScheduled)
                        ScheduleDensityJob(ref table, in pool, in palette);

                    // Never wait for worker threads on the render thread. A later frame will see
                    // completion and continue directly into polygonization.
                    if (!_densityJobHandle.IsCompleted) break;

                    _densityJobHandle.Complete();
                    _densityJobScheduled = false;
                    _build.Phase = 1;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 1)
                {
                    if (StepCells(voxelSize))
                    {
                        _build.Phase = 2;
                        _build.Cursor = 0;
                    }
                    continue;
                }

                if (_build.Phase == 2)
                {
                    if (StepFacetedPlanes(voxelSize))
                    {
                        _build.Phase = 3;
                        _build.Cursor = 0;
                    }
                    continue;
                }

                if (StepProfileBlocks(voxelSize)) FinishBuild(frame);
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
            _build = new BuildState
            {
                Active = true, Coordinate = best, Phase = 0, Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,
                SurfaceCatalogueHash = _surfaceCatalogue.CatalogueHash,
                CoatingCatalogueVersion = _coatingCatalogue.Version,
                CoatingCatalogueHash = _coatingCatalogue.CatalogueHash
            };
            return true;
        }

        private void Invalidate(int3 chunk)
        {
            _desiredVersions[chunk] = ++_versionCounter;
            _dirty.Add(chunk);
        }

        private void SetSurfaceCatalogue(in SurfaceCatalogue catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_surfaceCatalogue.Version == catalogue.Version
                && _surfaceCatalogue.CatalogueHash == hash) return;

            _surfaceCatalogue = catalogue;
            if (_surfaceCatalogue.CatalogueHash == 0)
                _surfaceCatalogue.Seal(_surfaceCatalogue.Version, hash);

            // Catalogue data participates in geometry. Existing meshes may remain visible while
            // every known chunk queues a replacement built from the new immutable snapshot.
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetMaterialPaletteVersion(uint version)
        {
            if (_materialPaletteVersion == version) return;
            _materialPaletteVersion = version;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetCoatingCatalogue(in CoatingCatalogue catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_coatingCatalogue.Version == catalogue.Version
                && _coatingCatalogue.CatalogueHash == hash) return;

            _coatingCatalogue = catalogue;
            if (_coatingCatalogue.CatalogueHash == 0)
                _coatingCatalogue.Seal(_coatingCatalogue.Version, hash);
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetProfileBlocks(ProfileBlockStore store)
        {
            uint version = store?.Version ?? 0;
            if (ReferenceEquals(_profileBlockStore, store) && _profileBlockVersion == version)
                return;
            _profileBlockStore = store;
            _profileBlockVersion = version;
            _profileBlocks = store?.Snapshot() ?? Array.Empty<ProfileBlock>();
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        /// <summary>
        /// Resolves the padded brick neighbourhood around the chunk once and copies only mixed
        /// voxel payloads. This snapshot is immutable until the Burst density job completes, so
        /// gameplay may continue editing/evicting authoritative storage without racing the job.
        /// </summary>
        private void ScheduleDensityJob(ref RegionTable table, in BrickPool pool,
                                        in MaterialPalette palette)
        {
            _densityMixedVoxels.Clear();
            _densityMixedSurfaceSemantics.Clear();
            _densityMixedBoundarySamples.Clear();

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

            _buildSurfaceCatalogue = _surfaceCatalogue;
            _buildCoatingCatalogue = _coatingCatalogue;
            var job = new TransvoxelDensityJob
            {
                Bricks = _densityBricks,
                MixedVoxels = _densityMixedVoxels.AsArray(),
                MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),
                Palette = palette,
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

            _build.MaterialPaletteVersion = palette.Version;
            _build.SnapshotTaken = true;
            _buildPalette = palette;

            _densityJobHandle = job.Schedule(GridSampleCount, 64);
            _densityJobScheduled = true;
        }

        private TransvoxelDensityBrick SnapshotBrick(ref RegionTable table, in BrickPool pool,
                                                      int3 worldBrick)
        {
            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region)) return default;

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            int brickIndex = Region.BrickIndex(bx, by, bz);
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
            _densityMixedSurfaceSemantics.ResizeUninitialized(nextLength);
            _densityMixedBoundarySamples.ResizeUninitialized(nextLength);
            NativeArray<byte> packed = _densityMixedVoxels.AsArray();
            NativeArray<ushort> packedSurfaces = _densityMixedSurfaceSemantics.AsArray();
            NativeArray<byte> packedBoundaries = _densityMixedBoundarySamples.AsArray();
            int sourceOffset = pool.VoxelOffset(brick.PoolIndex);
            for (int i = 0; i < VoxelDimensions.VoxelsPerBrick; i++)
            {
                packed[mixedOffset + i] = pool.Voxels[sourceOffset + i];
                packedSurfaces[mixedOffset + i] = pool.SurfaceSemantics[sourceOffset + i];
                packedBoundaries[mixedOffset + i] = pool.BoundarySamples[sourceOffset + i];
            }

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
            TryEmitSurfaceDecoration(chunkOrigin + cell, voxelSize);
            int caseCode = 0;
            for (int i = 0; i < 8; i++)
            {
                int3 grid = cell + Padding + TransvoxelRegularTables.CornerOffsets[i];
                int sampleIndex = GridIndex(grid.x, grid.y, grid.z);
                float density = _density[sampleIndex];
                _cellDensity[i] = density;
                _cellMaterial[i] = _materials[sampleIndex];
                _cellSurface[i] = _surfaceSemantics[sampleIndex];
                _cellBoundary[i] = _boundarySamples[sampleIndex];
                if (density < 0f) caseCode |= 1 << i;
            }

            if (caseCode == 0 || caseCode == 255) return;

            bool hasContinuousSurface = false;
            bool hasPlanarSurface = false;
            bool hasRoundedSurface = false;
            bool hasAuthoredBoundary = false;
            bool hasDisplacedCoating = false;
            for (int i = 0; i < 8; i++)
            {
                hasAuthoredBoundary |= _cellBoundary[i] != 0;
                byte coating = (byte)(_cellSurface[i] >> 16);
                hasDisplacedCoating |= _buildCoatingCatalogue.Get(coating).Displacement != 0;
            }
            for (int i = 0; i < 8; i++)
            {
                if (!IsSolidSurfaceMaterial(_cellMaterial[i])) continue;
                SurfaceStyleDefinition definition = _buildSurfaceCatalogue.Get(
                    (ushort)_cellSurface[i]);
                if (definition.Reconstruction == SurfaceReconstruction.Smooth
                    || definition.Reconstruction == SurfaceReconstruction.Rounded
                    || definition.Reconstruction == SurfaceReconstruction.Planar
                       && (hasAuthoredBoundary || hasDisplacedCoating))
                {
                    hasContinuousSurface = true;
                    if (definition.Reconstruction == SurfaceReconstruction.Planar)
                        hasPlanarSurface = true;
                    else
                        hasRoundedSurface = true;
                }
            }
            // Planar cells need continuous topology only when authoring retained a sub-voxel
            // boundary. Ordinary planar occupancy has exact axis-aligned faces and is emitted in
            // phase 2, avoiding diagonal marching triangles across walls and piers.
            if (!hasContinuousSurface) return;

            RegularCellData cellData =
                TransvoxelRegularTables.CellData[TransvoxelRegularTables.CellClass[caseCode]];
            ushort[] edgeCodes = TransvoxelRegularTables.VertexData[caseCode];
            int vertexCount = cellData.VertexCount;
            for (int i = 0; i < vertexCount; i++)
            {
                ushort edgeCode = edgeCodes[i];
                int corner0 = (edgeCode >> 4) & 0x0F;
                int corner1 = edgeCode & 0x0F;
                float d0 = _cellDensity[corner0];
                float d1 = _cellDensity[corner1];

                int3 o0 = TransvoxelRegularTables.CornerOffsets[corner0];
                int3 o1 = TransvoxelRegularTables.CornerOffsets[corner1];
                int3 edgeDelta = math.abs(o1 - o0);
                int edgeAxis = edgeDelta.x != 0 ? 0 : edgeDelta.y != 0 ? 1 : 2;
                var boundary0 = new VoxelBoundarySample { Packed = _cellBoundary[corner0] };
                var boundary1 = new VoxelBoundarySample { Packed = _cellBoundary[corner1] };
                if ((boundary0.IsAuthored && !boundary0.AppliesAlong(edgeAxis))
                    || (boundary1.IsAuthored && !boundary1.AppliesAlong(edgeAxis)))
                {
                    d0 = IsSolidSurfaceMaterial(_cellMaterial[corner0]) ? 0.5f : -0.5f;
                    d1 = IsSolidSurfaceMaterial(_cellMaterial[corner1]) ? 0.5f : -0.5f;
                }
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

                // Positive density is the solid side. Carry both its material and independent
                // surface semantics; if that endpoint is empty, fall back to another solid corner.
                int selectedCorner = d0 > d1 ? corner0 : corner1;
                byte material = _cellMaterial[selectedCorner];
                uint selectedSurface = _cellSurface[selectedCorner];
                if (!IsSolidSurfaceMaterial(material))
                {
                    for (int corner = 0; corner < 8; corner++)
                    {
                        if (!IsSolidSurfaceMaterial(_cellMaterial[corner])) continue;
                        material = _cellMaterial[corner];
                        selectedSurface = _cellSurface[corner];
                        break;
                    }
                }

                _cellVertices[i] = new SmoothSurfaceVertex
                {
                    Position = (Vector3)(worldVoxel * voxelSize),
                    Normal = (Vector3)normal,
                    Material = PackSurfaceAttributes(material, selectedSurface),
                    Active = FullyLitOcclusion
                };
            }

            int indexCount = cellData.TriangleCount * 3;
            byte[] localIndices = cellData.VertexIndices;
            if (hasPlanarSurface && !hasRoundedSurface)
            {
                // Continuous topology does not imply smooth shading. Duplicate the three
                // vertices of each planar triangle and assign its geometric normal, preserving
                // cut-stone facets while keeping the occupancy-constrained isosurface positions.
                for (int i = 0; i < indexCount; i += 3)
                {
                    SmoothSurfaceVertex a = _cellVertices[localIndices[i]];
                    SmoothSurfaceVertex b = _cellVertices[localIndices[i + 1]];
                    SmoothSurfaceVertex c = _cellVertices[localIndices[i + 2]];
                    Vector3 face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position)
                                          .normalized;
                    Vector3 expected = a.Normal + b.Normal + c.Normal;
                    if (Vector3.Dot(face, expected) < 0f) face = -face;
                    a.Normal = face;
                    b.Normal = face;
                    c.Normal = face;
                    uint triangleBase = (uint)_vertices.Count;
                    _vertices.Add(a);
                    _vertices.Add(b);
                    _vertices.Add(c);
                    _indices.Add(triangleBase);
                    _indices.Add(triangleBase + 1);
                    _indices.Add(triangleBase + 2);
                }
            }
            else
            {
                uint baseVertex = (uint)_vertices.Count;
                for (int i = 0; i < vertexCount; i++) _vertices.Add(_cellVertices[i]);
                for (int i = 0; i < indexCount; i++)
                    _indices.Add(baseVertex + localIndices[i]);
            }
        }

        private bool StepFacetedPlanes(float voxelSize)
        {
            const int planesPerStep = 8;
            int planeCount = 3 * 2 * CellsPerAxis;
            int end = math.min(planeCount, _build.Cursor + planesPerStep);
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;
            for (int planeIndex = _build.Cursor; planeIndex < end; planeIndex++)
            {
                int layer = planeIndex % CellsPerAxis;
                int face = planeIndex / CellsPerAxis;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                BuildFacetedMask(chunkOrigin, axis, sign, layer);
                MergeFacetedMask(chunkOrigin, axis, sign, layer, voxelSize);
            }
            _build.Cursor = end;
            return end >= planeCount;
        }

        private bool StepProfileBlocks(float voxelSize)
        {
            const int blocksPerStep = 8;
            int end = math.min(_profileBlocks.Length, _build.Cursor + blocksPerStep);
            for (int i = _build.Cursor; i < end; i++)
                EmitProfileBlock(in _profileBlocks[i], voxelSize);
            _build.Cursor = end;
            return end >= _profileBlocks.Length;
        }

        private void EmitProfileBlock(in ProfileBlock block, float voxelSize)
        {
            block.Bounds(out int3 blockMin, out int3 blockMax);
            int3 chunkMin = _build.Coordinate * VoxelsPerAxis;
            int3 chunkMax = chunkMin + VoxelsPerAxis;
            if (math.any(blockMin >= chunkMax) || math.any(blockMax < chunkMin)) return;

            int axisA = (block.Axis + 1) % 3;
            int axisB = (block.Axis + 2) % 3;
            float start = math.atan2(block.StartDirection.y, block.StartDirection.x);
            float finish = math.atan2(block.EndDirection.y, block.EndDirection.x);
            if (finish <= start) finish += math.PI * 2f;
            float inner = block.InnerRadiusQ4 * (1f / 16f);
            float outer = block.OuterRadiusQ4 * (1f / 16f);
            float midRadius = (inner + outer) * 0.5f;
            float jointAngle = block.JointHalfWidthQ4 * (1f / 16f)
                             / math.max(1f, midRadius);
            start += jointAngle;
            finish -= jointAngle;
            if (finish <= start) return;

            float bevel = math.min(block.BevelQ4 * (1f / 16f), (outer - inner) * 0.22f);
            float front = block.FrontQ4 * (1f / 16f);
            float shoulder = math.min(front + bevel, block.BackQ4 * (1f / 16f));
            float back = block.BackQ4 * (1f / 16f);
            float frontInner = inner + bevel;
            float frontOuter = outer - bevel;
            // Radial beds need a much narrower face bevel than the intrados/extrados. Applying
            // the full radial bevel at both sides doubles the authored mortar gap and turns the
            // ring into a black-outlined diagram rather than cut masonry.
            float angularBevel = bevel * 0.32f / math.max(1f, midRadius);
            float faceStart = math.min(start + angularBevel, (start + finish) * 0.5f);
            float faceFinish = math.max(finish - angularBevel, (start + finish) * 0.5f);
            int segments = math.clamp((int)math.ceil((finish - start) * outer / 0.55f), 2, 24);
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = math.lerp(start, finish, segment / (float)segments);
                float a1 = math.lerp(start, finish, (segment + 1f) / segments);
                float faceA0 = math.lerp(faceStart, faceFinish, segment / (float)segments);
                float faceA1 = math.lerp(faceStart, faceFinish,
                                         (segment + 1f) / segments);
                float3 f00 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontInner, faceA0, front);
                float3 f01 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontOuter, faceA0, front);
                float3 f11 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontOuter, faceA1, front);
                float3 f10 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontInner, faceA1, front);
                if (!TryReadProfileBacking((f00 + f01 + f11 + f10) * 0.25f,
                                           block.Axis, back, out uint backingSurface))
                    continue;
                VoxelSurfaceSemantics current = VoxelSurfaceSemantics.FromPacked(backingSurface);
                uint attributes = PackSurfaceAttributes(block.Material,
                    new VoxelSurfaceSemantics
                    {
                        StyleId = block.SurfaceStyle,
                        CoatingId = current.CoatingId != Coatings.None
                            ? current.CoatingId : block.Coating,
                        Flags = VoxelSurfaceFlags.PreserveFeature,
                        Detail = block.SurfaceDetail,
                    }.Packed);

                float3 frontNormal = float3.zero;
                frontNormal[block.Axis] = -1f;
                EmitProfileQuad(f00, f01, f11, f10, frontNormal, attributes, voxelSize);

                float3 outer0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             outer, a0, shoulder);
                float3 outer1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             outer, a1, shoulder);
                float3 outerNormal = float3.zero;
                float middle = (a0 + a1) * 0.5f;
                outerNormal[axisA] = math.cos(middle);
                outerNormal[axisB] = math.sin(middle);
                EmitProfileQuad(f01, outer0, outer1, f11,
                                math.normalizesafe(outerNormal - frontNormal * 0.55f),
                                attributes, voxelSize);

                float3 inner0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             inner, a0, shoulder);
                float3 inner1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             inner, a1, shoulder);
                float3 innerNormal = -outerNormal;
                EmitProfileQuad(inner0, f00, f10, inner1,
                                math.normalizesafe(innerNormal - frontNormal * 0.55f),
                                attributes, voxelSize);

                float3 outerBack0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 outer, a0, back);
                float3 outerBack1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 outer, a1, back);
                EmitProfileQuad(outer0, outerBack0, outerBack1, outer1,
                                outerNormal, attributes, voxelSize);
                float3 innerBack0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 inner, a0, back);
                float3 innerBack1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 inner, a1, back);
                EmitProfileQuad(innerBack0, inner0, inner1, innerBack1,
                                innerNormal, attributes, voxelSize);
            }

            uint sideAttributes = PackSurfaceAttributes(block.Material,
                new VoxelSurfaceSemantics
                {
                    StyleId = block.SurfaceStyle,
                    CoatingId = block.Coating,
                    Flags = VoxelSurfaceFlags.PreserveFeature,
                    Detail = block.SurfaceDetail,
                }.Packed);
            EmitProfileRadialSide(in block, start, inner, outer, frontInner, frontOuter,
                                  faceStart, front, shoulder, back, axisA, axisB, -1f,
                                  sideAttributes, voxelSize);
            EmitProfileRadialSide(in block, finish, inner, outer, frontInner, frontOuter,
                                  faceFinish, front, shoulder, back, axisA, axisB, 1f,
                                  sideAttributes, voxelSize);
        }

        private void EmitProfileRadialSide(in ProfileBlock block, float angle,
                                           float inner, float outer,
                                           float frontInner, float frontOuter,
                                           float faceAngle, float front,
                                           float shoulder, float back,
                                           int axisA, int axisB, float sign,
                                           uint attributes, float voxelSize)
        {
            float3 f0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     frontInner, faceAngle, front);
            float3 f1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     frontOuter, faceAngle, front);
            float3 s0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     inner, angle, shoulder);
            float3 s1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     outer, angle, shoulder);
            float3 b0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     inner, angle, back);
            float3 b1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     outer, angle, back);
            if (!TryReadProfileBacking((f0 + f1 + b0 + b1) * 0.25f,
                                       block.Axis, back, out _)) return;
            float3 normal = float3.zero;
            normal[axisA] = -math.sin(angle) * sign;
            normal[axisB] = math.cos(angle) * sign;
            float3 frontNormal = float3.zero;
            frontNormal[block.Axis] = -1f;
            EmitProfileQuad(f0, s0, s1, f1,
                            math.normalizesafe(normal - frontNormal * 0.55f),
                            attributes, voxelSize);
            EmitProfileQuad(s0, b0, b1, s1, normal, attributes, voxelSize);
        }

        private bool TryReadProfileBacking(float3 position, int axis, float back,
                                           out uint surface)
        {
            int3 voxel = (int3)math.round(position);
            voxel[axis] = (int)math.round(back);
            ReadSnapshotCell(voxel, out byte material, out surface, out _);
            return IsSolidSurfaceMaterial(material);
        }

        private void EmitProfileQuad(float3 p0, float3 p1, float3 p2, float3 p3,
                                     float3 normal, uint attributes, float voxelSize)
        {
            float3 centroid = (p0 + p1 + p2 + p3) * 0.25f;
            int3 owner = new(FloorDiv((int)math.floor(centroid.x), VoxelsPerAxis),
                             FloorDiv((int)math.floor(centroid.y), VoxelsPerAxis),
                             FloorDiv((int)math.floor(centroid.z), VoxelsPerAxis));
            if (!owner.Equals(_build.Coordinate)) return;
            p0 *= voxelSize; p1 *= voxelSize; p2 *= voxelSize; p3 *= voxelSize;
            uint baseVertex = (uint)_vertices.Count;
            var n = (Vector3)math.normalizesafe(normal, new float3(0f, 1f, 0f));
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p0, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p1, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p2, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p3, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 1);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 3); _indices.Add(baseVertex + 2);
            }
            else
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 1); _indices.Add(baseVertex + 2);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 3);
            }
        }

        private static float3 ProfilePoint(int3 centre, int axisA, int axisB, int axis,
                                           float radius, float angle, float depth)
        {
            float3 point = centre;
            point[axisA] += math.cos(angle) * radius;
            point[axisB] += math.sin(angle) * radius;
            point[axis] = depth;
            return point;
        }

        private void TryEmitSurfaceDecoration(int3 voxel, float voxelSize)
        {
            ReadSnapshotCell(voxel, out byte material, out uint surface, out _);
            if (!IsSolidSurfaceMaterial(material)) return;
            byte coating = (byte)(surface >> 16);
            CoatingDefinition definition = _buildCoatingCatalogue.Get(coating);
            if (definition.DecorationShape == SurfaceDecorationShape.None
                || definition.DecorationDensity == 0) return;

            for (int face = 0; face < 6; face++)
            {
                if ((definition.DecorationFaceMask & (1 << face)) == 0) continue;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                int3 neighbour = voxel;
                neighbour[axis] += sign;
                ReadSnapshotCell(neighbour, out byte adjacent, out _, out _);
                if (IsSolidSurfaceMaterial(adjacent)) continue;
                uint faceHash = DecorationHash(voxel, (byte)(coating + face * 17));
                if ((faceHash & 0xFFu) >= definition.DecorationDensity) continue;
                if (!IsDecorationAnchor(voxel, coating, face,
                                        definition.DecorationSeparation)) continue;

                EmitDecorationClump(voxel, material, surface, definition,
                                    axis, sign, faceHash, voxelSize);
                CompletedDecorationClumps++;
            }
        }

        private static bool IsDecorationAnchor(int3 voxel, byte coating, int face, int separation)
        {
            if (separation <= 0) return true;
            int stride = separation + 1;
            int axis = face >> 1;
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            uint rowHash = DecorationHash(voxel, (byte)(coating + face * 17));
            int offsetA = (int)(rowHash % (uint)stride);
            int offsetB = (int)((rowHash >> 8) % (uint)stride);
            return FloorMod(voxel[axisA] + offsetA, stride) == 0
                && FloorMod(voxel[axisB] + offsetB, stride) == 0;
        }

        private void EmitDecorationClump(int3 voxel, byte material, uint surface,
                                         CoatingDefinition definition, int axis, int sign,
                                         uint hash, float voxelSize)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            float jitterA = (((hash >> 8) & 0xFFu) * (1f / 255f) - 0.5f) * 0.42f;
            float jitterB = (((hash >> 16) & 0xFFu) * (1f / 255f) - 0.5f) * 0.42f;
            float radiusA = definition.DecorationRadiusQ4 * (1f / 16f)
                          * math.lerp(0.85f, 1.18f, ((hash >> 5) & 0xFFu) / 255f);
            float radiusB = definition.DecorationRadiusQ4 * (1f / 16f)
                          * math.lerp(0.62f, 1.05f, ((hash >> 13) & 0xFFu) / 255f);
            float height = definition.DecorationHeightQ4 * (1f / 16f)
                         * math.lerp(0.78f, 1.22f, ((hash >> 24) & 0xFFu) * (1f / 255f));
            float3 centre = (float3)voxel + new float3(0.5f);
            centre[axis] = voxel[axis] + (sign > 0 ? 1f : 0f);
            centre[axisA] += jitterA;
            centre[axisB] += jitterB;
            uint attributes = PackSurfaceAttributes(material, surface);
            uint baseVertex = (uint)_vertices.Count;
            const int sides = 6;
            for (int ring = 0; ring < 2; ring++)
            {
                float scale = ring == 0 ? 1f : 0.94f;
                float normalOffset = ring == 0 ? 0.025f : height;
                for (int side = 0; side < sides; side++)
                {
                    float angle = (side + ((hash >> (side + 3)) & 7u) * 0.018f)
                                * (math.PI * 2f / sides);
                    AddMoundVertex(centre, axis, axisA, axisB, sign,
                                   math.cos(angle) * radiusA * scale,
                                   math.sin(angle) * radiusB * scale,
                                   normalOffset, attributes, voxelSize);
                }
            }

            for (uint side = 0; side < sides; side++)
            {
                uint next = (side + 1u) % sides;
                _indices.Add(baseVertex + side);
                _indices.Add(baseVertex + sides + side);
                _indices.Add(baseVertex + next);
                _indices.Add(baseVertex + next);
                _indices.Add(baseVertex + sides + side);
                _indices.Add(baseVertex + sides + next);
            }
            for (uint side = 1; side < sides - 1; side++)
            {
                _indices.Add(baseVertex + sides);
                _indices.Add(baseVertex + sides + side + 1u);
                _indices.Add(baseVertex + sides + side);
            }

            if (axis == 1 && sign > 0 && definition.DecorationDropQ4 > 0)
                EmitDecorationFringes(voxel, centre, axisA, axisB, radiusA, radiusB,
                                      definition.DecorationDropQ4 * (1f / 16f), hash,
                                      attributes, voxelSize);
        }

        private void EmitDecorationFringes(int3 voxel, float3 centre, int axisA, int axisB,
                                           float radiusA, float radiusB, float maxDrop,
                                           uint hash, uint attributes, float voxelSize)
        {
            for (int face = 0; face < 6; face++)
            {
                int faceAxis = face >> 1;
                if (faceAxis == 1) continue;
                int faceSign = (face & 1) == 0 ? -1 : 1;
                int3 neighbour = voxel;
                neighbour[faceAxis] += faceSign;
                ReadSnapshotCell(neighbour, out byte adjacent, out _, out _);
                if (IsSolidSurfaceMaterial(adjacent)) continue;

                int direction = faceAxis == axisB
                    ? (faceSign > 0 ? 0 : 1)
                    : (faceSign > 0 ? 2 : 3);
                EmitDecorationFringe(centre, axisA, axisB, radiusA, radiusB,
                                     maxDrop, hash ^ (uint)(face * 0x9E37), direction,
                                     attributes, voxelSize);
            }
        }

        private void EmitDecorationFringe(float3 centre, int axisA, int axisB,
                                          float radiusA, float radiusB, float maxDrop,
                                          uint hash, int direction,
                                          uint attributes, float voxelSize)
        {
            // A shallow skirt hangs only over a genuinely exposed ledge. Its extent varies
            // deterministically, while world occupancy decides direction.
            float3 tangent = float3.zero;
            float3 outward = float3.zero;
            if (direction < 2)
            {
                tangent[axisA] = 1f;
                outward[axisB] = direction == 0 ? 1f : -1f;
            }
            else
            {
                tangent[axisB] = 1f;
                outward[axisA] = direction == 2 ? 1f : -1f;
            }
            float halfWidth = math.lerp(0.20f, 0.42f, ((hash >> 3) & 0xFFu) / 255f)
                            * (direction < 2 ? radiusA : radiusB);
            float drop = maxDrop * math.lerp(0.38f, 1f, ((hash >> 11) & 0xFFu) / 255f);
            float outwardRadius = direction < 2 ? radiusB : radiusA;
            float3 lip = centre + outward * outwardRadius;
            float3 p0 = lip - tangent * halfWidth;
            float3 p1 = lip + tangent * halfWidth;
            float3 p2 = p1 - new float3(0f, drop, 0f) - outward * 0.07f;
            float3 p3 = p0 - new float3(0f, drop * 0.72f, 0f) - outward * 0.05f;
            EmitProfileQuad(p0, p1, p2, p3, outward, attributes, voxelSize);
        }

        private void AddMoundVertex(float3 centre, int axis, int axisA, int axisB, int sign,
                                    float a, float b, float height,
                                    uint attributes, float voxelSize)
        {
            float3 position = centre;
            position[axisA] += a;
            position[axisB] += b;
            position[axis] += height * sign;
            float3 normal = float3.zero;
            normal[axis] = sign;
            _vertices.Add(new SmoothSurfaceVertex
            {
                Position = (Vector3)(position * voxelSize), Normal = (Vector3)normal,
                Material = attributes, Active = FullyLitOcclusion
            });
        }

        private static uint DecorationHash(int3 voxel, byte coating)
        {
            uint h = (uint)voxel.x * 0x9E3779B9u ^ (uint)voxel.y * 0x85EBCA6Bu
                   ^ (uint)voxel.z * 0xC2B2AE35u ^ (uint)coating * 0x27D4EB2Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h;
        }

        private void BuildFacetedMask(int3 chunkOrigin, int axis, int sign, int layer)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            for (int b = 0; b < CellsPerAxis; b++)
            for (int a = 0; a < CellsPerAxis; a++)
            {
                int3 local = int3.zero;
                local[axis] = layer;
                local[axisA] = a;
                local[axisB] = b;
                int3 voxel = chunkOrigin + local;
                ReadSnapshotCell(voxel, out byte material, out uint surface,
                                 out byte boundary);
                SurfaceStyleDefinition style = _buildSurfaceCatalogue.Get((ushort)surface);
                byte coating = (byte)(surface >> 16);
                bool displacedCoating =
                    _buildCoatingCatalogue.Get(coating).Displacement != 0;
                var authoredBoundary = new VoxelBoundarySample { Packed = boundary };
                bool boundaryAffectsFace = authoredBoundary.AppliesAlong(axis);
                bool faceted = IsSolidSurfaceMaterial(material)
                    && (style.Reconstruction == SurfaceReconstruction.Sharp
                        || style.Reconstruction == SurfaceReconstruction.Cubic
                        || style.Reconstruction == SurfaceReconstruction.Planar
                           && !boundaryAffectsFace && !displacedCoating);
                if (!faceted)
                {
                    _faceMask[a + b * CellsPerAxis] = 0;
                    continue;
                }

                int3 neighbour = voxel;
                neighbour[axis] += sign;
                ReadSnapshotCell(neighbour, out byte neighbourMaterial, out _,
                                 out byte neighbourBoundary);
                var neighbourAuthoredBoundary =
                    new VoxelBoundarySample { Packed = neighbourBoundary };
                _faceMask[a + b * CellsPerAxis] = IsSolidSurfaceMaterial(neighbourMaterial)
                    || neighbourAuthoredBoundary.AppliesAlong(axis)
                    ? 0u : PackSurfaceAttributes(material, surface) + 1u;
            }
        }

        private void MergeFacetedMask(int3 chunkOrigin, int axis, int sign, int layer,
                                      float voxelSize)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            for (int b = 0; b < CellsPerAxis; b++)
            for (int a = 0; a < CellsPerAxis; a++)
            {
                uint encoded = _faceMask[a + b * CellsPerAxis];
                if (encoded == 0) continue;
                int width = 1;
                while (a + width < CellsPerAxis
                       && _faceMask[a + width + b * CellsPerAxis] == encoded) width++;
                int height = 1;
                bool extend = true;
                while (b + height < CellsPerAxis && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (_faceMask[a + k + (b + height) * CellsPerAxis] == encoded) continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }
                for (int db = 0; db < height; db++)
                for (int da = 0; da < width; da++)
                    _faceMask[a + da + (b + db) * CellsPerAxis] = 0;
                EmitFacetedQuad(chunkOrigin, axis, axisA, axisB, sign, layer,
                                a, b, width, height, encoded - 1u, voxelSize);
            }
        }

        private void EmitFacetedQuad(int3 chunkOrigin, int axis, int axisA, int axisB,
                                     int sign, int layer, int a, int b,
                                     int width, int height, uint attributes, float voxelSize)
        {
            float3 p0 = chunkOrigin;
            p0[axis] += layer + (sign > 0 ? 1 : 0);
            p0[axisA] += a;
            p0[axisB] += b;
            float3 p1 = p0;
            float3 p2 = p0;
            float3 p3 = p0;
            p1[axisA] += width;
            p2[axisA] += width;
            p2[axisB] += height;
            p3[axisB] += height;
            p0 *= voxelSize;
            p1 *= voxelSize;
            p2 *= voxelSize;
            p3 *= voxelSize;
            float3 normal = float3.zero;
            normal[axis] = sign;
            uint baseVertex = (uint)_vertices.Count;
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p0, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p1, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p2, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p3, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 1);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 3); _indices.Add(baseVertex + 2);
            }
            else
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 1); _indices.Add(baseVertex + 2);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 3);
            }
        }

        private void ReadSnapshotCell(int3 voxel, out byte material, out uint surface,
                                      out byte boundary)
        {
            int3 chunkBrickOrigin = _build.Coordinate * BricksPerAxis;
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;
            int3 worldBrick = voxel >> VoxelDimensions.BrickEdgeLog2;
            int3 localBrick = worldBrick - cacheOrigin;
            if (math.any(localBrick < 0) || math.any(localBrick >= BrickCacheEdge))
            {
                material = 0;
                surface = 0;
                boundary = 0;
                return;
            }
            int brickIndex = localBrick.x
                           + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            TransvoxelDensityBrick brick = _densityBricks[brickIndex];
            if (brick.Kind == 0)
            {
                material = 0;
                surface = 0;
                boundary = 0;
                return;
            }
            if (brick.Kind == 1)
            {
                material = brick.UniformMaterial;
                surface = _buildPalette.GetDefaultSurfaceStyle(material);
                boundary = 0;
                return;
            }
            int3 local = voxel & VoxelDimensions.BrickEdgeMask;
            int voxelIndex = local.x | (local.y << 3) | (local.z << 6);
            material = _densityMixedVoxels[brick.MixedOffset + voxelIndex];
            surface = VoxelSurfaceSemantics.FromStorage(
                _densityMixedSurfaceSemantics[brick.MixedOffset + voxelIndex]).Packed;
            boundary = _densityMixedBoundarySamples[brick.MixedOffset + voxelIndex];
            if ((ushort)surface == SurfaceStyles.MaterialDefault)
                surface = (surface & 0xFFFF0000u)
                        | _buildPalette.GetDefaultSurfaceStyle(material);
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

        private static bool IsSolidSurfaceMaterial(byte material) =>
            material != 0 && material != 11 && material != 16;

        private static uint PackSurfaceAttributes(byte material, uint surface) =>
            material
            | (((surface >> 16) & 0xFFu) << 8)
            | ((surface & 0xFFu) << 16)
            | (((surface >> 24) & 0xFFu) << 24);

        private void FinishBuild(int frame)
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                // Input changed while the immutable snapshot/job was in flight. Never publish
                // stale geometry; the newer invalidation remains queued.
                StaleBuildCount++;
                _build = default;
                _vertices.Clear();
                _indices.Clear();
                return;
            }

            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            entry.Upload(_vertices, _indices);
            CompletedBuildCount++;
            UploadedGeometryBytes += (ulong)entry.GpuBytes;
            entry.LastUsedFrame = frame;
            entry.SourceVersion = _build.SourceVersion;
            entry.MaterialPaletteVersion = _build.MaterialPaletteVersion;
            entry.SurfaceCatalogueVersion = _build.SurfaceCatalogueVersion;
            entry.SurfaceCatalogueHash = _build.SurfaceCatalogueHash;
            entry.CoatingCatalogueVersion = _build.CoatingCatalogueVersion;
            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            _desiredVersions.Remove(_build.Coordinate);
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
            _desiredVersions.Remove(chunk);
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
            const int chunksPerRegion = VoxelDimensions.RegionVoxelEdge / VoxelsPerAxis;
            int shift = 0;
            for (int n = chunksPerRegion; n > 1; n >>= 1) shift++;
            return new int3(chunk.x >> shift, chunk.y >> shift, chunk.z >> shift);
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
            _desiredVersions.Clear();
            _visible.Clear();
            _vertices.Clear();
            _indices.Clear();
            if (_density.IsCreated) _density.Dispose();
            if (_materials.IsCreated) _materials.Dispose();
            if (_surfaceSemantics.IsCreated) _surfaceSemantics.Dispose();
            if (_boundarySamples.IsCreated) _boundarySamples.Dispose();
            if (_densityBricks.IsCreated) _densityBricks.Dispose();
            if (_densityMixedVoxels.IsCreated) _densityMixedVoxels.Dispose();
            if (_densityMixedSurfaceSemantics.IsCreated) _densityMixedSurfaceSemantics.Dispose();
            if (_densityMixedBoundarySamples.IsCreated) _densityMixedBoundarySamples.Dispose();
            _build = default;
        }
    }
}
