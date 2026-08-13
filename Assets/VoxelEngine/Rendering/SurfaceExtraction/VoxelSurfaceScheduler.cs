using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    public readonly struct VoxelSurfaceMetrics
    {
        public readonly int ChangeRecords;
        public readonly int DiscoveredSurfaceBricks;
        public readonly int SolidKnownChunks;
        public readonly int SolidResidentChunks;
        public readonly int SolidDirtyChunks;
        public readonly int WaterResidentChunks;
        public readonly int WaterDirtyChunks;
        public readonly int VisibleSolidChunks;
        public readonly int MissingVisibleSolidChunks;
        public readonly int VisibleDetailSolidChunks;
        public readonly int VisibleWaterChunks;
        public readonly ulong CompletedSolidBuilds;
        public readonly ulong RejectedStaleSolidBuilds;
        public readonly ulong CompletedWaterBuilds;
        public readonly ulong RejectedStaleWaterBuilds;
        public readonly long ResidentGeometryBytes;
        public readonly ulong UploadedGeometryBytes;
        public readonly ulong SolidDecorationClumps;

        internal VoxelSurfaceMetrics(CpuTransvoxelChunkCache solids,
                                     CpuWaterSurfaceChunkCache water,
                                     int changeRecords, int discoveredSurfaceBricks)
        {
            ChangeRecords = changeRecords;
            DiscoveredSurfaceBricks = discoveredSurfaceBricks;
            SolidKnownChunks = solids.KnownCount;
            SolidResidentChunks = solids.ResidentCount;
            SolidDirtyChunks = solids.DirtyCount;
            WaterResidentChunks = water.ResidentCount;
            WaterDirtyChunks = water.DirtyCount;
            VisibleSolidChunks = solids.Visible.Count;
            MissingVisibleSolidChunks = 0;
            VisibleDetailSolidChunks = 0;
            VisibleWaterChunks = water.Visible.Count;
            CompletedSolidBuilds = solids.CompletedBuildCount;
            RejectedStaleSolidBuilds = solids.StaleBuildCount;
            CompletedWaterBuilds = water.CompletedBuildCount;
            RejectedStaleWaterBuilds = water.StaleBuildCount;
            ResidentGeometryBytes = solids.ResidentGpuBytes + water.ResidentGpuBytes;
            UploadedGeometryBytes = solids.UploadedGeometryBytes + water.UploadedGeometryBytes;
            SolidDecorationClumps = solids.CompletedDecorationClumps;
        }

        internal VoxelSurfaceMetrics(GpuSurfaceChunkCache solids, int changeRecords,
                                     int discoveredSurfaceBricks)
            : this(solids, null, changeRecords, discoveredSurfaceBricks)
        {
        }

        internal VoxelSurfaceMetrics(GpuSurfaceChunkCache coverage,
                                     GpuSurfaceChunkCache detail,
                                     int changeRecords, int discoveredSurfaceBricks)
        {
            ChangeRecords = changeRecords;
            DiscoveredSurfaceBricks = discoveredSurfaceBricks;
            SolidKnownChunks = coverage.KnownCount + (detail?.KnownCount ?? 0);
            SolidResidentChunks = coverage.ResidentCount + (detail?.ResidentCount ?? 0);
            SolidDirtyChunks = coverage.DirtyCount + (detail?.DirtyCount ?? 0);
            WaterResidentChunks = 0;
            WaterDirtyChunks = 0;
            VisibleSolidChunks = coverage.Visible.Count + (detail?.Visible.Count ?? 0);
            MissingVisibleSolidChunks = coverage.MissingVisibleCount;
            VisibleDetailSolidChunks = detail?.Visible.Count ?? 0;
            VisibleWaterChunks = 0;
            CompletedSolidBuilds = (ulong)Math.Max(0,
                coverage.ResidentCount + (detail?.ResidentCount ?? 0)
              - coverage.DirtyCount - (detail?.DirtyCount ?? 0));
            RejectedStaleSolidBuilds = 0;
            CompletedWaterBuilds = 0;
            RejectedStaleWaterBuilds = 0;
            ResidentGeometryBytes = 0;
            UploadedGeometryBytes = 0;
            SolidDecorationClumps = 0;
        }
    }

    /// <summary>
    /// Common invalidation, residency, build-budget, and handoff owner for derived voxel surfaces.
    /// Render passes consume its ready entries and never interpret voxel semantics themselves.
    /// </summary>
    public sealed class VoxelSurfaceScheduler : IDisposable
    {
        private readonly CpuTransvoxelChunkCache _solids = new();
        private readonly CpuWaterSurfaceChunkCache _water = new();
        private readonly List<VoxelChangeRecord> _changeScratch = new(256);
        private readonly HashSet<int3> _changedSolidRegions = new();
        private readonly HashSet<int3> _changedWaterRegions = new();
        private readonly HashSet<int3> _changedBrickSet = new();
        private readonly List<int3> _changedBricks = new(64);
        private readonly HashSet<int3> _changedWaterBrickSet = new();
        private readonly List<int3> _changedWaterBricks = new(64);
        private readonly HashSet<int3> _surfaceDiscoveryRegions = new();
        private readonly List<int3> _discoveredSurfaceBricks = new(512);
        private ulong _changeCursor;
        private VoxelChangeJournal _journal;
        private int _lastChangeRecords;

        public double SolidBuildBudgetMs { get; set; } = 0.20;
        public double WaterBuildBudgetMs { get; set; } = 0.15;

        public IReadOnlyList<CpuTransvoxelChunkCache.Entry> VisibleSolids => _solids.Visible;
        public IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> VisibleWater => _water.Visible;
        public VoxelSurfaceMetrics Metrics => new(
            _solids, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count);

        public void Prepare(ref RegionTable table, ref BrickPool pool, in MaterialPalette palette,
                            in SurfaceCatalogue surfaceCatalogue,
                            in CoatingCatalogue coatingCatalogue,
                            ProfileBlockStore profileBlocks,
                            VoxelChangeJournal journal, Camera camera, float voxelSize, int frame)
        {
            _changedSolidRegions.Clear();
            _changedWaterRegions.Clear();
            _changedBrickSet.Clear();
            _changedBricks.Clear();
            _changedWaterBrickSet.Clear();
            _changedWaterBricks.Clear();
            _surfaceDiscoveryRegions.Clear();
            _discoveredSurfaceBricks.Clear();
            if (journal != null)
            {
                if (!ReferenceEquals(journal, _journal))
                {
                    _journal = journal;
                    _changeCursor = 0;
                }
                bool complete = journal.ReadSince(ref _changeCursor, _changeScratch);
                _lastChangeRecords = _changeScratch.Count;
                if (!complete)
                {
                    using var resident = table.GetResidentCoords(Allocator.Temp);
                    for (int i = 0; i < resident.Length; i++)
                    {
                        _changedSolidRegions.Add(resident[i]);
                        _changedWaterRegions.Add(resident[i]);
                        _surfaceDiscoveryRegions.Add(resident[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < _changeScratch.Count; i++)
                    {
                        VoxelChangeRecord change = _changeScratch[i];
                        bool affectsSolids = (change.Kind & (VoxelChangeKind.Occupancy
                            | VoxelChangeKind.BaseMaterial | VoxelChangeKind.SurfaceStyle
                            | VoxelChangeKind.Coating | VoxelChangeKind.Residency)) != 0;
                        bool affectsWater = (change.Kind & (VoxelChangeKind.Occupancy
                            | VoxelChangeKind.BaseMaterial | VoxelChangeKind.Water
                            | VoxelChangeKind.Residency)) != 0;
                        int3 extent = change.MaxVoxelExclusive - change.MinVoxel;
                        if (math.any(extent >= VoxelDimensions.RegionVoxelEdge))
                        {
                            if (affectsSolids) _changedSolidRegions.Add(change.Region);
                            if (affectsWater) _changedWaterRegions.Add(change.Region);
                            _surfaceDiscoveryRegions.Add(change.Region);
                            continue;
                        }

                        int3 minBrick = change.MinVoxel >> VoxelDimensions.BrickEdgeLog2;
                        int3 maxBrick = (change.MaxVoxelExclusive - 1)
                                      >> VoxelDimensions.BrickEdgeLog2;
                        for (int z = minBrick.z; z <= maxBrick.z; z++)
                        for (int y = minBrick.y; y <= maxBrick.y; y++)
                        for (int x = minBrick.x; x <= maxBrick.x; x++)
                        {
                            int3 brick = new(x, y, z);
                            if (affectsSolids && _changedBrickSet.Add(brick))
                                _changedBricks.Add(brick);
                            if (affectsWater && _changedWaterBrickSet.Add(brick))
                                _changedWaterBricks.Add(brick);
                        }
                    }
                }
            }
            else
            {
                _journal = null;
                _changeCursor = 0;
                _lastChangeRecords = 0;
            }

            _solids.InvalidateDirtyRegions(_changedSolidRegions);
            _water.InvalidateDirtyRegions(_changedWaterRegions);
            _solids.InvalidateSurfaceBricks(_changedBricks);
            _water.InvalidateSurfaceBricks(ref table, in pool, _changedWaterBricks);
            DiscoverSurfaceBricks(ref table, in pool, _surfaceDiscoveryRegions,
                                  _discoveredSurfaceBricks);
            _solids.InvalidateSurfaceBricks(_discoveredSurfaceBricks);
            _solids.Prepare(ref table, in pool, in palette, in surfaceCatalogue,
                            in coatingCatalogue,
                            profileBlocks,
                            camera, voxelSize, frame,
                            SolidBuildBudgetMs);
            _solids.CollectVisible(camera, voxelSize, frame);

            _water.InvalidateSurfaceBricks(ref table, in pool, _discoveredSurfaceBricks);
            _water.Prepare(ref table, in pool, camera, voxelSize, WaterBuildBudgetMs);
            _water.CollectVisible(camera, voxelSize);
        }

        private static void DiscoverSurfaceBricks(ref RegionTable table, in BrickPool pool,
                                                  HashSet<int3> regions,
                                                  List<int3> destination)
        {
            foreach (int3 regionCoord in regions)
            {
                if (!table.TryGetRegion(regionCoord, out Region region)) continue;
                NativeArray<BrickRef> refs = region.BrickRefs;
                int edge = VoxelDimensions.RegionEdge;
                int yStride = edge;
                int zStride = edge * edge;
                int3 origin = regionCoord * edge;
                for (int i = 0; i < refs.Length; i++)
                {
                    BrickRef brick = refs[i];
                    if (brick.IsEmpty) continue;
                    int bx = i & VoxelDimensions.RegionEdgeMask;
                    int by = (i >> VoxelDimensions.RegionEdgeLog2)
                           & VoxelDimensions.RegionEdgeMask;
                    int bz = i >> (VoxelDimensions.RegionEdgeLog2 * 2);
                    bool boundary = !IsFullySolid(brick, in pool)
                        || bx == 0 || by == 0 || bz == 0
                        || bx + 1 == edge || by + 1 == edge || bz + 1 == edge
                        || !IsFullySolid(refs[i - 1], in pool)
                        || !IsFullySolid(refs[i + 1], in pool)
                        || !IsFullySolid(refs[i - yStride], in pool)
                        || !IsFullySolid(refs[i + yStride], in pool)
                        || !IsFullySolid(refs[i - zStride], in pool)
                        || !IsFullySolid(refs[i + zStride], in pool);
                    if (boundary) destination.Add(origin + new int3(bx, by, bz));
                }
            }
        }

        private static bool IsFullySolid(BrickRef brick, in BrickPool pool)
        {
            if (brick.IsEmpty) return false;
            if (brick.IsUniform) return true;
            int offset = pool.OccupancyOffset(brick.PoolIndex);
            for (int i = 0; i < VoxelDimensions.OccupancyWordsPerBrick; i++)
                if (pool.Occupancy[offset + i] != ulong.MaxValue) return false;
            return true;
        }

        public void Dispose()
        {
            _water.Dispose();
            _solids.Dispose();
        }
    }
}
