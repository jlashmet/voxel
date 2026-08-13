using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Streaming;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// The streamed voxel world behind the showcase scene.
    ///
    /// Regions become resident as the camera approaches and are evicted behind it, using
    /// <see cref="ResidencyManager"/> for the residency set and eviction policy. A region is
    /// 64 bricks on a side — 51.2 m at 10 cm voxels — so flying in a straight line
    /// continuously loads and discards them, which is the thing worth watching in the HUD.
    ///
    /// Nothing here is engine code; it is a caller of the engine, in its own assembly. Edits
    /// go through <see cref="VoxelAccess"/> and shapes come from <see cref="BuildBrushes"/>.
    /// Terrain generation writes brick references directly, which is the only way to fill a
    /// region at a sane cost: a solid brick below the surface becomes a uniform reference and
    /// allocates nothing, and only the bricks the surface actually passes through take a pool
    /// slot. That asymmetry is the whole memory argument, and the HUD makes it visible.
    /// </summary>
    public sealed class ShowcaseWorld : IDisposable
    {
        public const float VoxelSize = 0.1f;
        // -- material indices ----------------------------------------------------

        public const byte MatStone = 1;
        public const byte MatWood = 2;
        public const byte MatSand = 3;
        public const byte MatGlass = 4;
        public const byte MatBedrock = 5;

        /// <summary>Materials the player can build with, in hotkey order (1..4).</summary>
        public static readonly byte[] BuildableMaterials = { MatStone, MatWood, MatSand, MatGlass };

        public static readonly string[] MaterialNames =
        {
            "empty", "stone", "wood", "sand", "glass", "bedrock",
            "darkstone", "slate", "tile", "cloth", "grass", "water", "gold", "dirt", "moss",
            "lit window", "cascade", "crystal"
        };

        // -- geometry constants --------------------------------------------------

        /// <summary>Voxels along a region edge: 512, i.e. 51.2 m.</summary>
        public const int RegionVoxelEdge = 1 << VoxelDimensions.RegionVoxelEdgeLog2;

        /// <summary>Metres along a region edge.</summary>
        public const float RegionMetres = RegionVoxelEdge * 0.1f;

        /// <summary>Base terrain height in voxels. Terrain stays inside the y = 0 region layer.</summary>
        private const int BaseHeight = 220;

        /// <summary>
        /// Exposed for the renderer's implicit far field, which evaluates this same terrain from
        /// the seed for everything beyond the resident set.
        /// </summary>
        public const int BaseHeightVoxels = BaseHeight;

        // -- state ---------------------------------------------------------------

        private RegionTable _table;
        private BrickPool _pool;
        private FeatureCatalogue _catalogue;
        private MaterialPalette _palette;
        private SurfaceCatalogue _surfaceCatalogue;
        private CoatingCatalogue _coatingCatalogue;
        private MaterialAdjacencyCatalogue _materialAdjacencyCatalogue;
        private readonly ProfileBlockStore _profileBlocks = new();
        private uint _editCounter;
        private CastlePlan _castlePlan;
        private bool _hasCastlePlan;
        private bool _castleTrapdoorOpen;
        private bool _castleFrontGateOpen;

        private readonly HashSet<int3> _generated = new();
        private readonly Queue<DetachedVoxelChunk> _detachedChunks = new();

        private static readonly int3[] s_Neighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, 0, 1), new(0, 0, -1), new(0, -1, 0),
        };

        private struct FallingVoxel
        {
            public int3 Position;
            public byte Material;
            public byte Coating;
        }

        public sealed class DetachedVoxelChunk
        {
            public int3[] Voxels;
            public byte[] Materials;
            public byte[] Coatings;
            public int SourceVoxelCount;
            public float3 ImpactMetres;
            public float3 ImpulseDirection;
        }

        private sealed class VisualBucket
        {
            public readonly List<FallingVoxel> Samples = new(GpuDebrisSystem.RenderInstancesPerChunk);
            public int SourceVoxelCount;
            public uint Priority;
        }

        private struct BrickCollapseInfo
        {
            public int OccupiedCount;
            public bool HasStructuralMarker;
        }

        private const int MaxCollapseComponentVoxels = 1_048_576;
        private const int FallingChunkEdge = 8;
        public const int MaxQueuedDetachedChunks = 256;
        private const int MaxVisualChunksPerCollapse = 192;

        private readonly VoxelChangeJournal _changes = new();
        private readonly List<int3> _pendingLoads = new();

        public ref RegionTable Table => ref _table;
        public ref BrickPool Pool => ref _pool;
        public MaterialPalette Palette => _palette;
        public SurfaceCatalogue SurfaceRules => _surfaceCatalogue;
        public CoatingCatalogue CoatingRules => _coatingCatalogue;
        public MaterialAdjacencyCatalogue MaterialAdjacencyRules =>
            _materialAdjacencyCatalogue;

        public VoxelChangeJournal Changes => _changes;

        public uint Seed { get; }

        /// <summary>Regions in the wanted set that have not been generated yet.</summary>
        public int PendingRegionLoads => _pendingLoads.Count;

        public int PendingDetachedChunks => _detachedChunks.Count;

        public int RegionsGenerated { get; private set; }
        public int RegionsEvicted { get; private set; }
        public double LastGenerateMs { get; private set; }
        public int LastEditVoxels { get; private set; }

        /// <summary>
        /// Load radius in regions. Deliberately far smaller than
        /// <see cref="ResidencyManager.LoadRadiusMetres_PC"/> (500 m): the shipping engine
        /// covers that distance with mip-level far-field data, whereas this demo builds full
        /// voxel detail plus a triangle mesh for every resident region. This is a demo budget,
        /// not a tiering parameter — Constitution Principle IV is about device class, and this
        /// number is the same on every device.
        /// </summary>
        public int LoadRadiusRegions { get; }

        public int UnloadRadiusRegions { get; }

        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions, int unloadRadiusRegions)
        {
            Seed = seed;
            LoadRadiusRegions = math.max(1, loadRadiusRegions);
            UnloadRadiusRegions = math.max(LoadRadiusRegions + 1, unloadRadiusRegions);

            _table = new RegionTable(64, Allocator.Persistent);
            _pool = new BrickPool(brickPoolCapacity, Allocator.Persistent);

            _palette = default;
            const uint weatherCoatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                        | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            _palette.Register(MatStone, 200, DestructionClass.Crumble,
                              SurfaceStyles.Smooth, weatherCoatings);
            _palette.Register(MatWood, 90, DestructionClass.Splinter,
                              SurfaceStyles.Planar, weatherCoatings);
            _palette.Register(MatSand, 20, DestructionClass.Powder,
                              SurfaceStyles.Smooth, 1u << Coatings.Wet);
            _palette.Register(MatGlass, 10, DestructionClass.Powder,
                              SurfaceStyles.Sharp, 1u << Coatings.Wet);
            _palette.Register(MatBedrock, 255, DestructionClass.None,
                              SurfaceStyles.Planar, 0u);

            // Castle materials. Weathering and roofing read as different stone, which is most of
            // what stops masonry looking extruded.
            _palette.Register(6, 210, DestructionClass.Crumble,
                              SurfaceStyles.Smooth, weatherCoatings); // dark stone
            _palette.Register(7, 120, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weatherCoatings); // slate
            _palette.Register(8, 110, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weatherCoatings); // tile
            _palette.Register(9, 15, DestructionClass.Splinter,
                              SurfaceStyles.Planar, weatherCoatings); // cloth
            _palette.Register(10, 25, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // grass
            _palette.Register(11, 5, DestructionClass.Spreading,
                              SurfaceStyles.Smooth, 0u); // water
            _palette.Register(12, 180, DestructionClass.Crumble,
                              SurfaceStyles.Sharp, 1u << Coatings.Soot); // gold
            _palette.Register(13, 30, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // dirt
            _palette.Register(14, 40, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // legacy moss material
            _palette.Register(15, 18, DestructionClass.Powder,
                              SurfaceStyles.Sharp, 1u << Coatings.Wet); // leaded window glass
            _palette.Register(Mat.MasonrySmall, 200, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);
            _palette.Register(Mat.MasonryMedium, 210, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);
            _palette.Register(Mat.MasonryLarge, 220, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);

            _surfaceCatalogue = SurfaceCatalogue.CreateBuiltIns();
            _coatingCatalogue = CoatingCatalogue.CreateBuiltIns();
            _materialAdjacencyCatalogue = default;

            _catalogue = ShowcaseCatalogue.Build(seed, Allocator.Persistent);
        }

        // -- streaming -----------------------------------------------------------

        /// <summary>
        /// Advances streaming for up to <paramref name="budgetMs"/> milliseconds.
        ///
        /// Generation is interruptible: a region is built in slices and the work stops at the
        /// budget wherever it happens to be, resuming next frame. Whole regions were the
        /// obvious unit and the wrong one — a region costs tens of milliseconds to fill, which
        /// is several frames' worth, so the unit of work has to be smaller than the unit of
        /// data.
        /// </summary>
        public void StepStreaming(float3 cameraMetres, double budgetMs)
        {
            var centre = ResidencyManager.PositionToRegion(cameraMetres);
            RefreshPending(centre);

            var deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            var start = Time.realtimeSinceStartupAsDouble;
            bool didWork = false;

            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!_gen.Active)
                {
                    if (_pendingLoads.Count == 0) break;

                    BeginRegion(_pendingLoads[0]);
                    _pendingLoads.RemoveAt(0);
                }

                didWork = true;
                if (StepRegion()) FinishRegion();
            }

            if (didWork) LastGenerateMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;

            EvictDistantRegions(centre);
        }

        /// <summary>
        /// Generates one region to completion regardless of budget. Used once at spawn: the
        /// character needs ground under it before physics can run at all.
        /// </summary>
        public void GenerateRegionBlocking(int3 regionCoord)
        {
            if (_generated.Contains(regionCoord)) return;
            if (_gen.Active) FinishRegionForced();

            BeginRegion(regionCoord);
            while (!StepRegion()) { }
            FinishRegion();
        }

        public bool IsGenerated(int3 regionCoord) => _generated.Contains(regionCoord);

        /// <summary>Region containing a world position in metres.</summary>
        public static int3 RegionAt(Vector3 metres) => new int3(
            Mathf.FloorToInt(metres.x / RegionMetres), 0, Mathf.FloorToInt(metres.z / RegionMetres));

        private void RefreshPending(int3 centre)
        {
            _pendingLoads.Clear();

            // Terrain lives entirely inside the y = 0 region layer, and an empty region still
            // costs 1 MB of brick pointers, so the demo keeps residency to that layer rather
            // than paying for a sphere of pure sky.
            for (int dx = -LoadRadiusRegions; dx <= LoadRadiusRegions; dx++)
            for (int dz = -LoadRadiusRegions; dz <= LoadRadiusRegions; dz++)
            {
                if (dx * dx + dz * dz > LoadRadiusRegions * LoadRadiusRegions) continue;

                var rc = new int3(centre.x + dx, 0, centre.z + dz);
                if (_generated.Contains(rc)) { ResidencyManager.TouchRegion(rc); continue; }
                if (_gen.Active && _gen.Coord.Equals(rc)) continue;

                _pendingLoads.Add(rc);
            }

            // Nearest first, so the hole in front of you fills before the one behind.
            _pendingLoads.Sort((a, b) =>
            {
                long da = (long)(a.x - centre.x) * (a.x - centre.x) + (long)(a.z - centre.z) * (a.z - centre.z);
                long db = (long)(b.x - centre.x) * (b.x - centre.x) + (long)(b.z - centre.z) * (b.z - centre.z);
                return da.CompareTo(db);
            });
        }

        private void EvictDistantRegions(int3 centre)
        {
            var resident = _table.GetResidentCoords(Allocator.Temp);

            for (int i = 0; i < resident.Length; i++)
            {
                var rc = resident[i];

                // The in-flight generator owns this Region value until FinishRegion commits it.
                // Evicting it here disposes BrickRefs out from under the next StepRegion call.
                if (_gen.Active && rc.Equals(_gen.Coord)) continue;

                int dx = rc.x - centre.x;
                int dz = rc.z - centre.z;

                if (dx * dx + dz * dz <= UnloadRadiusRegions * UnloadRadiusRegions) continue;

                // No write-back: the client owns no truth, so eviction discards and the region
                // regenerates from the seed on return.
                ResidencyManager.EvictWithoutWriteBack(rc, ref _table, ref _pool);
                _generated.Remove(rc);
                _changes.PublishRegion(rc, VoxelChangeKind.Residency);
                RegionsEvicted++;
            }

            resident.Dispose();
        }

        // -- terrain generation --------------------------------------------------

        /// <summary>
        /// In-progress generation of a single region.
        ///
        /// Heights are computed first because every brick column needs the min and max surface
        /// height across its footprint to decide whether it is uniform. Both phases resume from
        /// a cursor, which is what makes generation interruptible.
        /// </summary>
        private struct GenState
        {
            public bool Active;
            public int3 Coord;
            public Region Region;
            public NativeArray<int> Heights;
            public int Phase;      // 0 = heights, 1 = bricks
            public int Cursor;     // rows done, then brick columns done
        }

        private GenState _gen;

        /// <summary>Voxel rows of height sampled per slice.</summary>
        private const int HeightRowsPerSlice = 24;

        /// <summary>Brick columns filled per slice. One column is 64 bricks tall.</summary>
        private const int BrickColumnsPerSlice = 48;

        /// <summary>Fraction of the in-flight region that is built, 0..1.</summary>
        public float GenerationProgress => !_gen.Active ? 1f
            : _gen.Phase == 0
                ? _gen.Cursor / (float)RegionVoxelEdge * 0.3f
                : 0.3f + _gen.Cursor / (float)(VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge) * 0.7f;

        private void BeginRegion(int3 regionCoord)
        {
            _gen.Active = true;
            _gen.Coord = regionCoord;
            _gen.Region = _table.LoadRegion(regionCoord);
            _gen.Heights = new NativeArray<int>(RegionVoxelEdge * RegionVoxelEdge, Allocator.Persistent);
            _gen.Phase = 0;
            _gen.Cursor = 0;
        }

        /// <summary>Advances the in-flight region by one slice. Returns true when it is complete.</summary>
        private bool StepRegion()
        {
            int3 originVoxel = _gen.Coord * RegionVoxelEdge;

            if (_gen.Phase == 0)
            {
                int endRow = math.min(_gen.Cursor + HeightRowsPerSlice, RegionVoxelEdge);

                for (int lz = _gen.Cursor; lz < endRow; lz++)
                for (int lx = 0; lx < RegionVoxelEdge; lx++)
                    _gen.Heights[lx + lz * RegionVoxelEdge] = SurfaceHeight(originVoxel.x + lx, originVoxel.z + lz);

                _gen.Cursor = endRow;
                if (_gen.Cursor < RegionVoxelEdge) return false;

                _gen.Phase = 1;
                _gen.Cursor = 0;
                return false;
            }

            int columns = VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge;
            int endColumn = math.min(_gen.Cursor + BrickColumnsPerSlice, columns);

            for (int c = _gen.Cursor; c < endColumn; c++)
                FillBrickColumn(originVoxel, c % VoxelDimensions.RegionEdge, c / VoxelDimensions.RegionEdge);

            _gen.Cursor = endColumn;
            return _gen.Cursor >= columns;
        }

        /// <summary>
        /// Fills one 64-brick-tall column.
        ///
        /// Bricks entirely below the surface become uniform references and cost nothing; bricks
        /// entirely above become empty references and cost nothing. Only bricks the surface
        /// passes through take a pool slot, and those are written straight into the pool rather
        /// than through <see cref="VoxelAccess.SetVoxel"/> — the per-write collapse-to-uniform
        /// check is exactly right for edits and exactly wrong for bulk fill, where it would
        /// rescan 512 voxels on every one of them.
        /// </summary>
        private void FillBrickColumn(int3 originVoxel, int bx, int bz)
        {
            int minH = int.MaxValue, maxH = int.MinValue;

            for (int vz = 0; vz < VoxelDimensions.BrickEdge; vz++)
            for (int vx = 0; vx < VoxelDimensions.BrickEdge; vx++)
            {
                int h = _gen.Heights[(bx * VoxelDimensions.BrickEdge + vx)
                                     + (bz * VoxelDimensions.BrickEdge + vz) * RegionVoxelEdge];
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }

            for (int by = 0; by < VoxelDimensions.RegionEdge; by++)
            {
                int brickBaseY = originVoxel.y + by * VoxelDimensions.BrickEdge;
                int brickTopY = brickBaseY + VoxelDimensions.BrickEdge - 1;
                int idx = Region.BrickIndex(bx, by, bz);

                if (brickBaseY > maxH)
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.Empty;
                    continue;
                }

                if (brickTopY <= minH)
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.Uniform(
                        brickTopY < minH - DeepDepth ? MatBedrock : MatStone);
                    continue;
                }

                int poolIndex = _pool.Allocate();

                for (int vz = 0; vz < VoxelDimensions.BrickEdge; vz++)
                for (int vy = 0; vy < VoxelDimensions.BrickEdge; vy++)
                for (int vx = 0; vx < VoxelDimensions.BrickEdge; vx++)
                {
                    int h = _gen.Heights[(bx * VoxelDimensions.BrickEdge + vx)
                                         + (bz * VoxelDimensions.BrickEdge + vz) * RegionVoxelEdge];

                    _pool.SetVoxel(poolIndex,
                                   OccupancyMask.VoxelIndex(vx, vy, vz),
                                   MaterialAt(brickBaseY + vy, h));
                }

                if (_pool.TryGetUniformMaterial(poolIndex, out var uniform))
                {
                    _pool.Free(poolIndex);
                    _gen.Region.BrickRefs[idx] = uniform == VoxelDimensions.MaterialEmpty
                        ? BrickRef.Empty
                        : BrickRef.Uniform(uniform);
                }
                else
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.FromPoolIndex(poolIndex);
                }
            }
        }

        private void FinishRegion()
        {
            var coord = _gen.Coord;

            _table.CommitRegion(_gen.Region);
            ResidencyManager.TouchRegion(coord);

            _generated.Add(coord);
            _changes.PublishRegion(coord, VoxelChangeKind.All);

            // Neighbours must re-mesh too: faces along the shared border were meshed as the edge
            // of the loaded world and are now interior.

            // Features are generated after terrain, so they carve and build against finished
            // ground. Everything here is a function of (seed, catalogue, region coordinate) —
            // no neighbour is consulted, which is why regions may arrive in any order.
            if (_catalogue.IsCreated)
            {
                var featureStart = Time.realtimeSinceStartupAsDouble;

                var report = FeatureGeneration.GenerateRegion(
                    in _catalogue, Seed, coord, ref _table, ref _pool);

                FeatureVoxelsBuilt += report.VoxelsWritten;
                FeatureInstancesBuilt += report.InstancesRasterised;

                // Only record timing for regions that actually built something; otherwise the
                // number reads as the cost of doing nothing.
                if (report.InstancesRasterised > 0)
                    LastFeatureMs = (Time.realtimeSinceStartupAsDouble - featureStart) * 1000.0;

                if (report.BudgetExceeded)
                    Debug.LogWarning($"Feature budget exceeded in region {coord}; content was refused rather than truncated.");
            }

            RegionsGenerated++;

            // The pointer grid is only final now. Anything uploaded earlier described a
            // half-built region.
            _changes.PublishRegion(coord, VoxelChangeKind.All);

            FinishRegionForced();

            if (coord.Equals(int3.zero)) BuildLandmarks();
        }

        /// <summary>Releases the in-flight generation state without publishing it.</summary>
        private void FinishRegionForced()
        {
            if (_gen.Heights.IsCreated) _gen.Heights.Dispose();
            _gen = default;
        }

        /// <summary>Depth below the surface at which stone gives way to indestructible bedrock.</summary>
        private const int DeepDepth = 40;

        private static byte MaterialAt(int y, int surface)
        {
            if (y > surface) return VoxelDimensions.MaterialEmpty;
            if (y == surface) return surface < BaseHeight ? MatSand : Mat.Grass;
            if (y > surface - DeepDepth) return MatStone;
            return MatBedrock;
        }

        /// <summary>
        /// Surface height in voxels, from the engine's canonical sampler.
        ///
        /// This used to be a demo-side copy of the noise, written because
        /// <c>TerrainGenerator.SampleSurfaceHeight</c> reduced its inputs modulo the region edge
        /// and produced identical terrain in every region. That is fixed, so the copy is gone:
        /// the showcase and the feature generator must agree about where the ground is, and two
        /// implementations of the same function are two things that can drift.
        /// </summary>
        public int SurfaceHeight(int wx, int wz) => TerrainSampler.HeightAt(wx, wz, Seed);

        // -- landmarks -----------------------------------------------------------

        /// <summary>
        /// Hand-built structures at the spawn point, so there is something with corners and
        /// distinct materials to blow up before you go looking at terrain.
        /// </summary>
        /// <summary>
        /// The castle: sited, built, furnished, and undermined by its own dungeon.
        ///
        /// Built once when the origin region completes. It sculpts its own outcrop, so it must run
        /// after terrain rather than alongside it.
        /// </summary>
        private void BuildLandmarks()
        {
            int cx = RegionVoxelEdge / 2;
            int cz = RegionVoxelEdge / 2 + 120;
            int ground = SurfaceHeight(cx, cz);

            var plan = CastleBuilder.Plan(new int3(cx, ground, cz), Seed);

            // Every region the castle reaches into must exist *before* it is built. A castle is
            // wider than a region, and terrain generation writes a region's brick pointers
            // wholesale — so a neighbour generated afterwards silently erases the half of the
            // castle that stood in it. That is what left a scatter of blocks and a terraced
            // quarry where a castle should be.
            // Enumerate every coordinate in the touched range. Sampling centre +/- reach and
            // treating those samples as neighbours skipped intermediate coordinates whenever
            // reach exceeded one region; the castle then wrote into a default-empty region and
            // left enormous void wedges where its terrain foundation should have been.
            int reach = math.max(plan.PlateauRadius + plan.CliffDrop + 8, RegionVoxelEdge);
            int minRx = (cx - reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int maxRx = (cx + reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int minRz = (cz - reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int maxRz = (cz + reach) >> VoxelDimensions.RegionVoxelEdgeLog2;

            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
            {
                var neighbour = new int3(rx, 0, rz);
                if (neighbour.Equals(int3.zero)) continue;

                GenerateRegionBlocking(neighbour);
            }

            var brush = CastleBuilder.Build(ref _table, ref _pool, in plan, Seed, in _palette);
            int referenceArchVoxels = BuildReferenceArch(new int3(cx - 120, 0, cz - 210));
            _castlePlan = plan;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;
            BuildCastlePresentationLights(in plan);

            CastleVoxels = brush.TotalVoxelsWritten + referenceArchVoxels;

            // Everything the castle touched has to be re-meshed and re-uploaded.
            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
            {
                var rc = new int3(rx, 0, rz);
                _changes.PublishRegion(rc, VoxelChangeKind.All);
            }
        }

        private int BuildReferenceArch(int3 horizontalOrigin)
        {
            int ground = SurfaceHeight(horizontalOrigin.x, horizontalOrigin.z);
            int3 origin = new(horizontalOrigin.x, ground + 1, horizontalOrigin.z);
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 64,
                PierHeight = 48,
                RingThickness = 10,
                Depth = 12,
                VoussoirCount = 13,
                StoneMaterial = Mat.DarkStone,
                PierStyle = SurfaceStyles.Rounded,
                RingStyle = SurfaceStyles.MasonryJoint,
                Coating = Coatings.Moss
            };

            var primitives = new NativeList<Primitive>(arch.Metadata.MaxPrimitives, Allocator.Temp);
            try
            {
                ArchValidationError validation = arch.Validate(
                    in _palette, in _surfaceCatalogue, in _coatingCatalogue);
                if (validation != ArchValidationError.None
                    || !arch.Emit(origin, primitives, _profileBlocks))
                    throw new InvalidOperationException(
                        $"The built-in reference arch is invalid: {validation}.");
                int3 max = origin + arch.Metadata.Footprint;
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, max, ref _table, ref _pool);
                ReferenceArchMin = origin;
                ReferenceArchMax = max;
                return result.VoxelsWritten;
            }
            finally
            {
                primitives.Dispose();
            }
        }

        /// <summary>Voxels the castle wrote. Reported in the HUD so its cost is visible.</summary>
        public long CastleVoxels { get; private set; }
        public int3 ReferenceArchMin { get; private set; }
        public int3 ReferenceArchMax { get; private set; }
        public ProfileBlockStore ProfileBlocks => _profileBlocks;

        public Vector4[] CastlePresentationLights { get; private set; } = Array.Empty<Vector4>();
        public Vector4[] CastlePresentationLightColours { get; private set; } = Array.Empty<Vector4>();

        private void BuildCastlePresentationLights(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            int keepCentreZ = keepMinZ + plan.KeepHalfZ;
            int keepMaxX = plan.Centre.x + plan.KeepHalfX;
            int wingWidth = math.max(96, plan.KeepHalfX * 4 / 5);
            int wingDepth = math.max(80, plan.KeepHalfZ * 2 - 72);
            int wingCentreX = keepMaxX - 4 + wingWidth / 2;
            int wingCentreZ = keepMinZ + 24 + wingDepth / 2;
            int chapelWidth = math.max(78, plan.KeepHalfX * 2 / 3);
            int chapelDepth = math.max(96, plan.KeepHalfZ * 6 / 5);
            int chapelCentreX = plan.Centre.x - plan.KeepHalfX - chapelWidth / 2 + 4;
            int chapelCentreZ = keepMinZ + plan.KeepHalfZ * 2 - chapelDepth / 2 - 38;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;
            int trapZ = keepMinZ + plan.KeepHalfZ + 40;
            int caveZ = trapZ - 411;
            int3 bellTower = CastleBuilder.ChapelBellTowerCentre(in plan);

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            CastlePresentationLights = new[]
            {
                LightAt(plan.Centre.x - 45, baseY + 26, keepCentreZ - 28, 8.0f),
                LightAt(plan.Centre.x + 42, baseY + 26, keepCentreZ + 30, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight + 17, keepCentreZ, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17, keepCentreZ, 7.0f),
                LightAt(wingCentreX, baseY + 17, wingCentreZ, 7.5f),
                LightAt(wingCentreX, baseY + plan.FloorHeight + 17, wingCentreZ, 7.0f),
                LightAt(chapelCentreX - 18, baseY + 24, chapelCentreZ, 7.5f),
                LightAt(chapelCentreX + 22, baseY + 27, chapelCentreZ, 7.5f),
                LightAt(plan.Centre.x - 55, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x + 58, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x - 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 226, dungeonY + 16, trapZ, 8.0f),
                LightAt(plan.Centre.x - 226, dungeonY + 15, trapZ, 8.0f),
                LightAt(plan.Centre.x - 40, dungeonY + 9, caveZ - 15, 11.5f),
                LightAt(plan.Centre.x + 45, dungeonY + 11, caveZ + 24, 11.5f),
                LightAt(plan.Centre.x + 145, dungeonY + 12, caveZ + 25, 10.5f),
                LightAt(plan.Centre.x - 52, baseY + plan.FloorHeight + 16,
                        keepCentreZ + 27, 6.5f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ - 42, 6.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ + 42, 6.0f),
                LightAt(bellTower.x, baseY + 17, bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 2 + 17,
                        bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 3 + 17,
                        bellTower.z, 5.0f),
            };

            var hallWarm = new Vector4(1.00f, 0.38f, 0.10f, 1.85f);
            var upperWarm = new Vector4(1.00f, 0.40f, 0.13f, 1.05f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var sideRoomWarm = new Vector4(1.00f, 0.34f, 0.09f, 1.05f);
            var caveWarm = new Vector4(1.00f, 0.27f, 0.06f, 2.35f);
            var caveBlue = new Vector4(0.10f, 0.58f, 1.00f, 2.05f);
            CastlePresentationLightColours = new[]
            {
                hallWarm, hallWarm, upperWarm, upperWarm, hallWarm, upperWarm,
                chapelWarm, chapelWarm,
                cellarWarm, cellarWarm, cellarWarm, cellarWarm, sideRoomWarm, sideRoomWarm,
                caveWarm, caveWarm, caveBlue,
                upperWarm, upperWarm, upperWarm,
                chapelWarm, upperWarm, upperWarm,
            };
        }

        public bool CastleTrapdoorOpen => _castleTrapdoorOpen;
        public bool CastleFrontGateOpen => _castleFrontGateOpen;

        public Vector3 CastleFrontGatePosition
        {
            get
            {
                if (!_hasCastlePlan) return Vector3.positiveInfinity;
                int3 min = CastleBuilder.FrontGateMinimum(in _castlePlan);
                return new Vector3(min.x + CastleBuilder.FrontGateWidth * 0.5f,
                                   min.y,
                                   min.z - 8f) * VoxelSize;
            }
        }

        public bool CanOpenCastleFrontGate(Vector3 playerFeetMetres)
        {
            if (!_hasCastlePlan || _castleFrontGateOpen) return false;
            Vector3 delta = playerFeetMetres - CastleFrontGatePosition;
            return new Vector2(delta.x, delta.z).sqrMagnitude <= 4.2f * 4.2f
                && math.abs(delta.y) <= 3.0f;
        }

        public bool TryOpenCastleFrontGate(Vector3 playerFeetMetres)
        {
            if (!CanOpenCastleFrontGate(playerFeetMetres)) return false;

            int3 min = CastleBuilder.FrontGateMinimum(in _castlePlan);
            int half = CastleBuilder.FrontGateWidth / 2;
            int archTop = CastleBuilder.FrontGateHeight - half;
            var gateVoxels = new List<FallingVoxel>(CastleBuilder.FrontGateWidth
                                                    * CastleBuilder.FrontGateHeight
                                                    * CastleBuilder.FrontGateDepth);
            for (int d = 0; d < CastleBuilder.FrontGateDepth; d++)
            for (int w = 0; w < CastleBuilder.FrontGateWidth; w++)
            for (int h = 0; h < CastleBuilder.FrontGateHeight; h++)
            {
                int dx = w - half;
                if (h > archTop && dx * dx + (h - archTop) * (h - archTop) > half * half)
                    continue;

                var voxel = new int3(min.x + w, min.y + h, min.z + d);
                gateVoxels.Add(new FallingVoxel { Position = voxel, Material = MatWood });
            }
            ClearVoxelsBulk(gateVoxels);

            _castleFrontGateOpen = true;
            return true;
        }

        public Vector3 CastleTrapdoorPosition
        {
            get
            {
                if (!_hasCastlePlan) return Vector3.positiveInfinity;
                int3 centre = CastleBuilder.TrapdoorCentre(in _castlePlan);
                return ((Vector3)(float3)centre + new Vector3(0.5f, 0.2f, 0.5f)) * 0.1f;
            }
        }

        /// <summary>True while the player is close enough to operate the closed cellar hatch.</summary>
        public bool CanOpenCastleTrapdoor(Vector3 playerFeetMetres)
        {
            if (!_hasCastlePlan || _castleTrapdoorOpen) return false;
            Vector3 delta = playerFeetMetres - CastleTrapdoorPosition;
            return new Vector2(delta.x, delta.z).sqrMagnitude <= 3.2f * 3.2f
                && math.abs(delta.y) <= 2.5f;
        }

        /// <summary>
        /// Opens the secret hatch without invoking destruction physics. This is authored moving
        /// architecture, so only the known lid bounds change and the stair beneath remains intact.
        /// </summary>
        public bool TryOpenCastleTrapdoor(Vector3 playerFeetMetres)
        {
            if (!CanOpenCastleTrapdoor(playerFeetMetres)) return false;

            int3 centre = CastleBuilder.TrapdoorCentre(in _castlePlan);
            int half = CastleBuilder.TrapdoorHalfSize;
            for (int y = centre.y; y < centre.y + 4; y++)
            for (int z = centre.z - half; z < centre.z + half; z++)
            for (int x = centre.x - half; x < centre.x + half; x++)
            {
                var voxel = new int3(x, y, z);
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, voxel,
                                         VoxelDimensions.MaterialEmpty))
                    MarkDirty(voxel);
            }

            _castleTrapdoorOpen = true;
            return true;
        }

        // -- edits ---------------------------------------------------------------

        /// <summary>
        /// Carves a spherical blast.
        ///
        /// The palette decides the outcome per voxel: indestructible materials survive, and
        /// harder materials survive proportionally more of the blast's outer shell, so one
        /// explosion leaves a clean crater in sand and a ragged one in stone. Selection is a
        /// seeded integer draw, so the same blast resolves identically on every client.
        /// </summary>
        public int Explode(int3 centre, ushort radius, float3 impulseDirection = default)
        {
            var rng = new DeterministicRandom(MixSeed(centre, radius));
            var voxels = BuildBrushes.PlaceSphere(centre, radius, VoxelDimensions.MaterialEmpty, Seed);
            var removed = new List<FallingVoxel>(math.min(voxels.Length, 8192));

            int radiusSq = radius * radius;
            int changed = 0;

            for (int i = 0; i < voxels.Length; i++)
            {
                var v = voxels[i];
                var existing = VoxelAccess.GetVoxel(ref _table, in _pool, v);
                if (existing == VoxelDimensions.MaterialEmpty) continue;
                if (!_palette.IsDestructible(existing)) continue;

                var d = v - centre;
                int distSq = d.x * d.x + d.y * d.y + d.z * d.z;
                int rimFactor = radiusSq == 0 ? 0 : (distSq * 255) / radiusSq;
                int resistance = (_palette.GetHardness(existing) * rimFactor) / 255;

                if ((int)(rng.NextUint() & 0xFF) < resistance) continue;

                VoxelCell cell = VoxelAccess.GetCell(ref _table, in _pool, v);
                removed.Add(new FallingVoxel
                {
                    Position = v,
                    Material = existing,
                    Coating = cell.Surface.CoatingId,
                });
            }

            voxels.Dispose();
            changed = ClearVoxelsBulk(removed);
            int collapsed = ResolveUnsupportedAfterRemoval(removed, centre, radius, impulseDirection);
            _editCounter++;
            LastEditVoxels = changed + collapsed;
            return changed + collapsed;
        }

        /// <summary>
        /// Removes one exact voxel and runs the same support/collapse pass as an impact. Exposed
        /// for deterministic physics tests and for future non-explosive cutting tools.
        /// </summary>
        public int RemoveAndResolveCollapse(int3 voxel)
        {
            byte material = VoxelAccess.GetVoxel(ref _table, in _pool, voxel);
            if (material == VoxelDimensions.MaterialEmpty || !_palette.IsDestructible(material))
                return 0;
            VoxelCell cell = VoxelAccess.GetCell(ref _table, in _pool, voxel);
            var removed = new List<FallingVoxel>(1)
            {
                new()
                {
                    Position = voxel,
                    Material = material,
                    Coating = cell.Surface.CoatingId,
                },
            };
            if (ClearVoxelsBulk(removed) == 0) return 0;
            int collapsed = ResolveUnsupportedAfterRemoval(removed, voxel, 1, default);
            LastEditVoxels = 1 + collapsed;
            return 1 + collapsed;
        }

        public bool TryDequeueDetachedChunk(out DetachedVoxelChunk chunk) =>
            _detachedChunks.TryDequeue(out chunk);

        /// <summary>Returns the centre height at which a detached chunk should meet solid world.</summary>
        public float FindLandingCentreY(float3 pivotMetres, float halfHeightMetres)
        {
            int x = (int)math.floor(pivotMetres.x / VoxelSize);
            int z = (int)math.floor(pivotMetres.z / VoxelSize);
            int startY = (int)math.floor((pivotMetres.y - halfHeightMetres)
                                        / VoxelSize) - 1;
            for (int y = math.min(startY, RegionVoxelEdge - 1); y >= 0; y--)
                if (VoxelAccess.IsSolid(ref _table, in _pool, new int3(x, y, z)))
                    return (y + 1) * VoxelSize + halfHeightMetres;
            return halfHeightMetres;
        }

        /// <summary>
        /// Clears a destruction batch without running the mixed-to-uniform 512-byte scan after
        /// every voxel. Each touched brick is normalized once at the end. Large tower failures
        /// used to spend almost all of their frame repeating that same scan thousands of times.
        /// </summary>
        private int ClearVoxelsBulk(List<FallingVoxel> voxels)
        {
            if (voxels.Count == 0) return 0;

            var touchedBricks = new HashSet<int3>();
            var touchedRegions = new HashSet<int3>();
            int cleared = 0;

            for (int i = 0; i < voxels.Count; i++)
            {
                int3 position = voxels[i].Position;
                VoxelAccess.Decompose(position, out int3 regionCoord,
                                      out int3 brickInRegion, out int3 voxelInBrick);
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;

                int brickIndex = Region.BrickIndex(brickInRegion.x, brickInRegion.y,
                                                   brickInRegion.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsUniform)
                {
                    if (brick.UniformMaterial == VoxelDimensions.MaterialEmpty) continue;
                    int poolIndex = _pool.Allocate();
                    _pool.FillBrick(poolIndex, brick.UniformMaterial);
                    brick = BrickRef.FromPoolIndex(poolIndex);
                    region.BrickRefs[brickIndex] = brick;
                }

                int voxelIndex = VoxelEngine.Core.Occupancy.OccupancyMask.VoxelIndex(
                    voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);
                if (_pool.GetVoxel(brick.PoolIndex, voxelIndex)
                    == VoxelDimensions.MaterialEmpty) continue;

                _pool.SetVoxel(brick.PoolIndex, voxelIndex, VoxelDimensions.MaterialEmpty);
                touchedBricks.Add(position >> VoxelDimensions.BrickEdgeLog2);
                touchedRegions.Add(regionCoord);
                cleared++;
            }

            foreach (int3 worldBrick in touchedBricks)
            {
                int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                int brickIndex = Region.BrickIndex(localBrick.x, localBrick.y, localBrick.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsMixed
                    && _pool.TryGetUniformMaterial(brick.PoolIndex, out byte uniform))
                {
                    _pool.Free(brick.PoolIndex);
                    region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
                }

                MarkDirtyBrick(worldBrick);
            }

            foreach (int3 regionCoord in touchedRegions)
            {
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                region.Dirty = true;
                _table.CommitRegion(region);
            }

            return cleared;
        }

        private void MarkDirtyBrick(int3 worldBrick)
        {
            int3 min = worldBrick << VoxelDimensions.BrickEdgeLog2;
            int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
            _changes.Publish(regionCoord, min, min + VoxelDimensions.BrickEdge,
                VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);
        }

        private int ResolveUnsupportedAfterRemoval(List<FallingVoxel> removed, int3 impact, int radius,
                                                    float3 impulseDirection)
        {
            if (removed.Count == 0) return 0;

            // Deduplicate the solid boundary once. The previous removed×6 nested scan repeatedly
            // revisited empty neighbours and was the dominant cost when several tornadoes hit.
            var candidates = new HashSet<int3>();
            for (int r = 0; r < removed.Count; r++)
            for (int d = 0; d < s_Neighbours.Length; d++)
            {
                int3 candidate = removed[r].Position + s_Neighbours[d];
                if (VoxelAccess.IsSolid(ref _table, in _pool, candidate)) candidates.Add(candidate);
            }

            int collapsed = ResolveDisconnectedCandidates(candidates, impact, impulseDirection);

            // Load failure is brick-granular for speed and can cut through a larger connected
            // structure at the edge of its bounded analysis volume. Recheck that exact new edge
            // afterwards so clipped roofs, beams, banners, and ornaments cannot remain floating.
            var overloadBoundary = new HashSet<int3>();
            collapsed += ResolveOverloadedSupport(impact, radius, impulseDirection,
                                                  overloadBoundary);
            collapsed += ResolveDisconnectedCandidates(overloadBoundary, impact,
                                                       impulseDirection);
            return collapsed;
        }

        private int ResolveDisconnectedCandidates(HashSet<int3> candidates, int3 impact,
                                                  float3 impulseDirection)
        {
            if (candidates.Count == 0) return 0;

            var classified = new HashSet<int3>();
            var knownSupported = new HashSet<int3>();
            int collapsed = 0;

            foreach (int3 seed in candidates)
            {
                if (classified.Contains(seed)) continue;

                var component = new List<FallingVoxel>(512);
                var stack = new Stack<int3>();
                var localSeen = new HashSet<int3>();
                stack.Push(seed);
                localSeen.Add(seed);
                bool anchored = false;
                bool overflow = false;

                while (stack.Count > 0)
                {
                    int3 current = stack.Pop();
                    if (knownSupported.Contains(current)) { anchored = true; break; }
                    byte material = VoxelAccess.GetVoxel(ref _table, in _pool, current);
                    if (material == VoxelDimensions.MaterialEmpty) continue;

                    if (!_palette.IsDestructible(material) || current.y <= 0)
                    {
                        anchored = true;
                        break;
                    }

                    VoxelCell cell = VoxelAccess.GetCell(ref _table, in _pool, current);
                    component.Add(new FallingVoxel
                    {
                        Position = current,
                        Material = material,
                        Coating = cell.Surface.CoatingId,
                    });
                    if (component.Count >= MaxCollapseComponentVoxels)
                    {
                        overflow = true;
                        break;
                    }

                    // Push downward last so the LIFO traversal follows columns toward ground
                    // before spreading sideways across an entire terrain surface.
                    for (int n = 0; n < s_Neighbours.Length; n++)
                    {
                        int3 next = current + s_Neighbours[n];
                        if (localSeen.Contains(next)
                            || !VoxelAccess.IsSolid(ref _table, in _pool, next)) continue;
                        localSeen.Add(next);
                        stack.Push(next);
                    }
                }

                if (anchored || overflow)
                {
                    foreach (int3 voxel in localSeen)
                    {
                        classified.Add(voxel);
                        knownSupported.Add(voxel);
                    }
                    continue;
                }

                collapsed += DetachComponent(component, impact, impulseDirection);
            }
            return collapsed;
        }

        /// <summary>
        /// Connectivity is necessary but not sufficient: a surviving one-voxel neck cannot carry
        /// an entire tower. Find the weakest vertical contact plane through the damaged band,
        /// then compare the mass above it with material-weighted contact capacity.
        /// </summary>
        private int ResolveOverloadedSupport(int3 impact, int radius, float3 impulseDirection,
                                             HashSet<int3> detachedBoundary)
        {
            int influenceRadius = math.clamp(radius * 2 + 56, 64, 96);
            int scanRadius = influenceRadius;
            int weakestPlane = impact.y;
            // A sphere's widest cut—and therefore its weakest remaining contact—is its centre
            // plane. Scanning every layer (and later three representative layers) only repeated
            // tens of thousands of sparse lookups before debris could start moving.
            if (CountVerticalContacts(impact.x, impact.z, weakestPlane, scanRadius) == 0) return 0;

            int collapsed = 0;
            int seedY = weakestPlane + 1;
            int minBrickX = (impact.x - influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int maxBrickX = (impact.x + influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int minBrickZ = (impact.z - influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int maxBrickZ = (impact.z + influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int minBrickY = seedY >> VoxelDimensions.BrickEdgeLog2;
            // Castle towers are under 26 m tall. Do not probe empty sky to the top of the
            // 51.2 m region after every impact.
            int maxBrickY = math.min(RegionVoxelEdge - 1, seedY + 256)
                          >> VoxelDimensions.BrickEdgeLog2;
            int influenceRadiusSq = influenceRadius * influenceRadius;
            var candidates = new Dictionary<int3, BrickCollapseInfo>();

            // Structural overload is intentionally brick-granular. A castle-scale tower can
            // contain hundreds of thousands of voxels; walking a HashSet node for every one was
            // the remaining impact hitch. At 8^3 voxels per brick this bounds the same search to
            // a few thousand compact records, while voxel collision remains exact until failure.
            for (int bz = minBrickZ; bz <= maxBrickZ; bz++)
            for (int by = minBrickY; by <= maxBrickY; by++)
            for (int bx = minBrickX; bx <= maxBrickX; bx++)
            {
                int centreX = (bx << VoxelDimensions.BrickEdgeLog2) + 4 - impact.x;
                int centreZ = (bz << VoxelDimensions.BrickEdgeLog2) + 4 - impact.z;
                if (centreX * centreX + centreZ * centreZ
                    > influenceRadiusSq + 64) continue;
                int3 worldBrick = new int3(bx, by, bz);
                BrickCollapseInfo info = ReadBrickCollapseInfo(worldBrick, seedY);
                if (info.OccupiedCount > 0) candidates.Add(worldBrick, info);
            }

            var visited = new HashSet<int3>();
            foreach (var pair in candidates)
            {
                int3 seed = pair.Key;
                if (seed.y != minBrickY || visited.Contains(seed))
                    continue;

                var component = new List<int3>(128);
                var stack = new Stack<int3>();
                stack.Push(seed);
                visited.Add(seed);
                long supportCapacity = 0;
                bool containsStructuralMaterial = false;
                int occupiedCount = 0;

                while (stack.Count > 0)
                {
                    int3 current = stack.Pop();
                    BrickCollapseInfo info = candidates[current];
                    containsStructuralMaterial |= info.HasStructuralMarker;
                    occupiedCount += info.OccupiedCount;
                    component.Add(current);

                    if (current.y == minBrickY)
                    {
                        int brickOriginX = current.x << VoxelDimensions.BrickEdgeLog2;
                        int brickOriginZ = current.z << VoxelDimensions.BrickEdgeLog2;
                        for (int lz = 0; lz < VoxelDimensions.BrickEdge; lz++)
                        for (int lx = 0; lx < VoxelDimensions.BrickEdge; lx++)
                        {
                            int3 upper = new int3(brickOriginX + lx, seedY,
                                                  brickOriginZ + lz);
                            if (!VoxelAccess.IsSolid(ref _table, in _pool, upper)) continue;
                            byte below = VoxelAccess.GetVoxel(ref _table, in _pool,
                                                             upper + new int3(0, -1, 0));
                            if (below != VoxelDimensions.MaterialEmpty)
                            {
                                // One intact voxel column carries dozens of voxels above it, not
                                // merely one short course. The former 6..12 capacity made even a
                                // fully supported tower fail after any nearby impact. Material
                                // hardness still matters, while a one-voxel thread remains far
                                // too weak for a castle-scale mass.
                                supportCapacity += 48 + _palette.GetHardness(below);
                            }
                        }
                    }

                    for (int n = 0; n < s_Neighbours.Length; n++)
                    {
                        int3 next = current + s_Neighbours[n];
                        if (visited.Contains(next) || !candidates.ContainsKey(next)) continue;
                        visited.Add(next);
                        stack.Push(next);
                    }
                }

                if (containsStructuralMaterial && occupiedCount > supportCapacity)
                    collapsed += DetachBrickComponent(component, seedY, impact, impulseDirection,
                                                      detachedBoundary);
            }

            return collapsed;
        }

        private BrickCollapseInfo ReadBrickCollapseInfo(int3 worldBrick, int minimumVoxelY)
        {
            int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
            int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
            if (!_table.TryGetRegion(regionCoord, out var region)) return default;
            BrickRef brick = region.GetBrick(localBrick.x, localBrick.y, localBrick.z);
            int firstY = math.max(0, minimumVoxelY
                                  - (worldBrick.y << VoxelDimensions.BrickEdgeLog2));
            if (firstY >= VoxelDimensions.BrickEdge || brick.IsEmpty) return default;

            if (brick.IsUniform)
            {
                byte material = brick.UniformMaterial;
                if (!_palette.IsDestructible(material)) return default;
                return new BrickCollapseInfo
                {
                    OccupiedCount = (VoxelDimensions.BrickEdge - firstY)
                                    * VoxelDimensions.BrickEdge * VoxelDimensions.BrickEdge,
                    HasStructuralMarker = IsStructuralMaterial(material),
                };
            }

            int count = 0;
            bool marker = false;
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
            for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
            {
                int index = VoxelEngine.Core.Occupancy.OccupancyMask.VoxelIndex(x, y, z);
                byte material = _pool.GetVoxel(brick.PoolIndex, index);
                if (material == VoxelDimensions.MaterialEmpty
                    || !_palette.IsDestructible(material)) continue;
                count++;
                marker |= IsStructuralMaterial(material);
            }
            return new BrickCollapseInfo
            {
                OccupiedCount = count,
                HasStructuralMarker = marker,
            };
        }

        private int DetachBrickComponent(List<int3> component, int minimumVoxelY, int3 impact,
                                         float3 impulseDirection,
                                         HashSet<int3> detachedBoundary)
        {
            var buckets = new List<VisualBucket>(component.Count);
            var touchedRegions = new HashSet<int3>();
            int detached = 0;

            for (int b = 0; b < component.Count; b++)
            {
                int3 worldBrick = component[b];
                int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                int brickIndex = Region.BrickIndex(localBrick.x, localBrick.y, localBrick.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsEmpty) continue;

                int firstY = math.max(0, minimumVoxelY
                                      - (worldBrick.y << VoxelDimensions.BrickEdgeLog2));
                var visual = new VisualBucket { Priority = VisualHash(worldBrick) };

                if (brick.IsUniform && firstY == 0 && _palette.IsDestructible(brick.UniformMaterial))
                {
                    byte material = brick.UniformMaterial;
                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = 0; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int3 position = (worldBrick << VoxelDimensions.BrickEdgeLog2)
                                      + new int3(x, y, z);
                        AddVisualSample(visual,
                            new FallingVoxel { Position = position, Material = material });
                    }
                    region.BrickRefs[brickIndex] = BrickRef.Empty;
                    detached += visual.SourceVoxelCount;
                }
                else
                {
                    if (brick.IsUniform)
                    {
                        if (!_palette.IsDestructible(brick.UniformMaterial)) continue;
                        int poolIndex = _pool.Allocate();
                        _pool.FillBrick(poolIndex, brick.UniformMaterial);
                        brick = BrickRef.FromPoolIndex(poolIndex);
                        region.BrickRefs[brickIndex] = brick;
                    }

                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int index = VoxelEngine.Core.Occupancy.OccupancyMask.VoxelIndex(x, y, z);
                        byte material = _pool.GetVoxel(brick.PoolIndex, index);
                        if (material == VoxelDimensions.MaterialEmpty
                            || !_palette.IsDestructible(material)) continue;
                        int3 position = (worldBrick << VoxelDimensions.BrickEdgeLog2)
                                      + new int3(x, y, z);
                        byte coating = _pool.GetSurface(brick.PoolIndex, index).CoatingId;
                        AddVisualSample(visual,
                            new FallingVoxel
                            {
                                Position = position,
                                Material = material,
                                Coating = coating,
                            });
                        _pool.SetVoxel(brick.PoolIndex, index, VoxelDimensions.MaterialEmpty);
                    }

                    if (_pool.TryGetUniformMaterial(brick.PoolIndex, out byte uniform))
                    {
                        _pool.Free(brick.PoolIndex);
                        region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
                    }
                    detached += visual.SourceVoxelCount;
                }

                if (visual.SourceVoxelCount == 0) continue;
                buckets.Add(visual);
                touchedRegions.Add(regionCoord);
                MarkDirtyBrick(worldBrick);
            }

            foreach (int3 regionCoord in touchedRegions)
            {
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                region.Dirty = true;
                _table.CommitRegion(region);
            }

            CollectSolidBrickBoundary(component, minimumVoxelY, detachedBoundary);

            buckets.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            int visualCount = math.min(MaxVisualChunksPerCollapse,
                                       MaxQueuedDetachedChunks - _detachedChunks.Count);
            visualCount = math.min(visualCount, buckets.Count);
            int represented = 0;
            for (int b = 0; b < visualCount; b++) represented += buckets[b].SourceVoxelCount;
            float coverageScale = represented > 0 ? detached / (float)represented : 1f;
            for (int b = 0; b < visualCount; b++)
            {
                VisualBucket bucket = buckets[b];
                var chunk = new DetachedVoxelChunk
                {
                    Voxels = new int3[bucket.Samples.Count],
                    Materials = new byte[bucket.Samples.Count],
                    Coatings = new byte[bucket.Samples.Count],
                    SourceVoxelCount = math.max(bucket.SourceVoxelCount,
                                                (int)math.ceil(bucket.SourceVoxelCount
                                                               * coverageScale)),
                    ImpactMetres = ((float3)impact + 0.5f) * VoxelSize,
                    ImpulseDirection = impulseDirection,
                };
                for (int i = 0; i < bucket.Samples.Count; i++)
                {
                    chunk.Voxels[i] = bucket.Samples[i].Position;
                    chunk.Materials[i] = bucket.Samples[i].Material;
                    chunk.Coatings[i] = bucket.Samples[i].Coating;
                }
                _detachedChunks.Enqueue(chunk);
            }

            return detached;
        }

        /// <summary>
        /// Collects only the surviving voxel faces immediately adjacent to a brick-granular
        /// failure. Scanning perimeter faces instead of every detached voxel keeps the cascade
        /// proportional to the exposed fracture surface.
        /// </summary>
        private void CollectSolidBrickBoundary(List<int3> component, int minimumVoxelY,
                                               HashSet<int3> boundary)
        {
            var clearedBricks = new HashSet<int3>(component);
            int minimumBrickY = minimumVoxelY >> VoxelDimensions.BrickEdgeLog2;

            for (int i = 0; i < component.Count; i++)
            {
                int3 brick = component[i];
                for (int d = 0; d < s_Neighbours.Length; d++)
                {
                    int3 neighbour = brick + s_Neighbours[d];
                    if (clearedBricks.Contains(neighbour)) continue;
                    AddSolidFaceSeeds(neighbour, s_Neighbours[d], minimumVoxelY, boundary);
                }

                // The lowest brick is only cleared above the failure plane. Include the exact
                // surviving face inside that same mixed brick as a possible support/remnant.
                if (brick.y == minimumBrickY && minimumVoxelY > 0)
                {
                    int originX = brick.x << VoxelDimensions.BrickEdgeLog2;
                    int originZ = brick.z << VoxelDimensions.BrickEdgeLog2;
                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int3 voxel = new int3(originX + x, minimumVoxelY - 1, originZ + z);
                        if (VoxelAccess.IsSolid(ref _table, in _pool, voxel)) boundary.Add(voxel);
                    }
                }
            }
        }

        private void AddSolidFaceSeeds(int3 worldBrick, int3 directionFromCleared,
                                       int minimumVoxelY, HashSet<int3> boundary)
        {
            int3 origin = worldBrick << VoxelDimensions.BrickEdgeLog2;
            int edge = VoxelDimensions.BrickEdge - 1;

            for (int v = 0; v < VoxelDimensions.BrickEdge; v++)
            for (int u = 0; u < VoxelDimensions.BrickEdge; u++)
            {
                int3 local;
                if (directionFromCleared.x != 0)
                    local = new int3(directionFromCleared.x > 0 ? 0 : edge, v, u);
                else if (directionFromCleared.y != 0)
                    local = new int3(u, directionFromCleared.y > 0 ? 0 : edge, v);
                else
                    local = new int3(u, v, directionFromCleared.z > 0 ? 0 : edge);

                int3 voxel = origin + local;
                if (voxel.y < minimumVoxelY
                    || !VoxelAccess.IsSolid(ref _table, in _pool, voxel)) continue;
                boundary.Add(voxel);
            }
        }

        private static void AddVisualSample(VisualBucket bucket, FallingVoxel voxel)
        {
            bucket.SourceVoxelCount++;
            int capacity = GpuDebrisSystem.RenderInstancesPerChunk;
            if (bucket.Samples.Count < capacity)
            {
                bucket.Samples.Add(voxel);
                return;
            }

            uint sampleHash = VisualHash(voxel.Position)
                            ^ (uint)bucket.SourceVoxelCount * 0x9E3779B9u;
            uint selected = sampleHash % (uint)bucket.SourceVoxelCount;
            if (selected < capacity) bucket.Samples[(int)selected] = voxel;
        }

        private int CountVerticalContacts(int centreX, int centreZ, int planeY, int radius)
        {
            int count = 0;
            int radiusSq = radius * radius;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                int3 lower = new int3(centreX + dx, planeY, centreZ + dz);
                if (VoxelAccess.IsSolid(ref _table, in _pool, lower)
                    && VoxelAccess.IsSolid(ref _table, in _pool, lower + new int3(0, 1, 0)))
                    count++;
            }
            return count;
        }

        private static bool IsStructuralMaterial(byte material) => material switch
        {
            MatWood or MatGlass or 6 or 7 or 8 or 9 or 12 or 15 => true,
            _ => false,
        };

        private int DetachComponent(List<FallingVoxel> component, int3 impact,
                                    float3 impulseDirection)
        {
            int detachedCount = ClearVoxelsBulk(component);
            if (detachedCount == 0) return 0;

            // Preserve the whole silhouette, not merely the first part visited by the flood
            // fill. Every spatial bucket gets a tiny reservoir sample, then a deterministic
            // subset of buckets spanning the component is handed to the GPU.
            var buckets = new Dictionary<int3, VisualBucket>();
            for (int i = 0; i < component.Count; i++)
            {
                var voxel = component[i];
                int3 p = voxel.Position;
                int3 bucketCoord = new int3(FloorDiv(p.x, FallingChunkEdge),
                                            FloorDiv(p.y, FallingChunkEdge),
                                            FloorDiv(p.z, FallingChunkEdge));
                if (!buckets.TryGetValue(bucketCoord, out var bucket))
                {
                    bucket = new VisualBucket { Priority = VisualHash(bucketCoord) };
                    buckets.Add(bucketCoord, bucket);
                }

                bucket.SourceVoxelCount++;
                int capacity = GpuDebrisSystem.RenderInstancesPerChunk;
                if (bucket.Samples.Count < capacity)
                    bucket.Samples.Add(voxel);
                else
                {
                    uint sampleHash = VisualHash(p) ^ (uint)bucket.SourceVoxelCount * 0x9E3779B9u;
                    uint selected = sampleHash % (uint)bucket.SourceVoxelCount;
                    if (selected < capacity) bucket.Samples[(int)selected] = voxel;
                }
            }

            var selectedBuckets = new List<VisualBucket>(buckets.Values);
            selectedBuckets.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            int visualCount = math.min(MaxVisualChunksPerCollapse,
                                       MaxQueuedDetachedChunks - _detachedChunks.Count);
            visualCount = math.min(visualCount, selectedBuckets.Count);
            int represented = 0;
            for (int b = 0; b < visualCount; b++)
                represented += selectedBuckets[b].SourceVoxelCount;
            float coverageScale = represented > 0 ? detachedCount / (float)represented : 1f;
            for (int b = 0; b < visualCount; b++)
            {
                VisualBucket bucket = selectedBuckets[b];
                var voxels = bucket.Samples;
                var chunk = new DetachedVoxelChunk
                {
                    Voxels = new int3[voxels.Count],
                    Materials = new byte[voxels.Count],
                    Coatings = new byte[voxels.Count],
                    SourceVoxelCount = math.max(bucket.SourceVoxelCount,
                                                (int)math.ceil(bucket.SourceVoxelCount
                                                               * coverageScale)),
                    ImpactMetres = ((float3)impact + 0.5f) * VoxelSize,
                    ImpulseDirection = impulseDirection,
                };
                for (int i = 0; i < voxels.Count; i++)
                {
                    chunk.Voxels[i] = voxels[i].Position;
                    chunk.Materials[i] = voxels[i].Material;
                    chunk.Coatings[i] = voxels[i].Coating;
                }
                _detachedChunks.Enqueue(chunk);
            }

            return detachedCount;
        }

        private static uint VisualHash(int3 value)
        {
            uint hash = (uint)(value.x * 73856093)
                      ^ (uint)(value.y * 19349663)
                      ^ (uint)(value.z * 83492791);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            return hash ^ (hash >> 16);
        }

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

        /// <summary>Places a solid sphere — the build half of the loop.</summary>
        public int Place(int3 centre, ushort radius, byte material)
        {
            var voxels = BuildBrushes.PlaceSphere(centre, radius, material, Seed);
            int changed = 0;

            for (int i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].y < 0) continue;
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, voxels[i], material))
                {
                    changed++;
                    MarkDirty(voxels[i]);
                }
            }

            voxels.Dispose();
            _editCounter++;
            LastEditVoxels = changed;
            return changed;
        }

        /// <summary>
        /// Publishes the exact changed cell. The scheduler expands the extraction halo.
        /// </summary>
        private void MarkDirty(int3 voxel)
        {
            var rc = new int3(voxel.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                              voxel.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                              voxel.z >> VoxelDimensions.RegionVoxelEdgeLog2);

            // Storage may allocate or collapse a mixed brick, but render domains consume only
            // this logical changed range and expand their own extraction halos.
            _changes.Publish(rc, voxel, voxel + 1,
                VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);

        }

        // -- brush stamps --------------------------------------------------------

        private void FillBox(int3 minCorner, int3 size, byte material)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                var v = minCorner + new int3(x, y, z);
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, v, material)) MarkDirty(v);
            }
        }

        private void StampCylinder(int3 baseCentre, ushort radius, int height, byte material)
        {
            var voxels = BuildBrushes.PlaceCylinder(baseCentre, radius, height, material, Seed);
            for (int i = 0; i < voxels.Length; i++)
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, voxels[i], material)) MarkDirty(voxels[i]);
            voxels.Dispose();
        }

        private void StampSphere(int3 centre, ushort radius, byte material)
        {
            var voxels = BuildBrushes.PlaceSphere(centre, radius, material, Seed);
            for (int i = 0; i < voxels.Length; i++)
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, voxels[i], material)) MarkDirty(voxels[i]);
            voxels.Dispose();
        }

        private uint MixSeed(int3 centre, ushort radius) =>
            Seed ^ (uint)(centre.x * 73856093) ^ (uint)(centre.y * 19349663)
                 ^ (uint)(centre.z * 83492791) ^ ((uint)radius << 3) ^ (_editCounter * 2654435761u);

        /// <summary>
        /// Spawn point in metres, on the open southern approach to the castle.
        ///
        /// The former z=166 position lies inside the castle's sculpted outcrop. Grounding there
        /// against the unmodified terrain sampler put the character's body inside raised rock.
        /// This point is beyond both the cliff skirt and the longest possible gate bridge.
        /// </summary>
        public Vector3 SpawnPosition()
        {
            // Offset east of the bridge axis so the first view layers the eastern tower,
            // gatehouse, keep, and west wing instead of collapsing them into a flat symmetric
            // elevation. This remains beyond the sculpted cliff skirt and bridge footprint.
            int cx = RegionVoxelEdge / 2 + 190;
            const int cz = -220;
            int h = SurfaceHeight(cx, cz);
            return new Vector3(cx * 0.1f, (h + 40) * 0.1f, cz * 0.1f);
        }

        /// <summary>
        /// Highest occupied voxel in a generated world column.
        ///
        /// Unlike <see cref="SurfaceHeight"/>, this includes landmarks and player edits. Spawn
        /// and respawn must use the world that collision actually reads, not the terrain that
        /// existed before the castle sculpted it.
        /// </summary>
        public int OccupiedSurfaceHeight(int wx, int wz)
        {
            for (int y = RegionVoxelEdge - 1; y >= 0; y--)
            {
                byte material = VoxelAccess.GetVoxel(ref _table, in _pool, new int3(wx, y, wz));
                if (material != VoxelDimensions.MaterialEmpty) return y;
            }

            // A generated terrain column always has a surface, but retaining the canonical
            // sampler fallback makes this method safe if called before its region is resident.
            return SurfaceHeight(wx, wz);
        }

        /// <summary>
        /// Feature voxels written so far, accumulated. Reporting only the most recent region
        /// showed zero almost always, since most regions contain no features.
        /// </summary>
        public int FeatureVoxelsBuilt { get; private set; }
        public int FeatureInstancesBuilt { get; private set; }
        public double LastFeatureMs { get; private set; }

        public void Dispose()
        {
            FinishRegionForced();
            _detachedChunks.Clear();
            if (_catalogue.IsCreated) _catalogue.Dispose();
            if (_table.IsCreated) _table.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
        }
    }
}
