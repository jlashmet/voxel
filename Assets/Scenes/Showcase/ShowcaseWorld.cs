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
            "darkstone", "slate", "tile", "cloth", "grass", "water", "gold", "dirt", "moss"
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
        private uint _editCounter;
        private CastlePlan _castlePlan;
        private bool _hasCastlePlan;
        private bool _castleTrapdoorOpen;

        private readonly HashSet<int3> _generated = new();
        private readonly HashSet<int3> _dirtyRegions = new();
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
        }

        public sealed class DetachedVoxelChunk
        {
            public int3[] Voxels;
            public byte[] Materials;
            public float3 ImpactMetres;
            public float3 ImpulseDirection;
        }

        private const int MaxCollapseComponentVoxels = 1_048_576;
        private const int FallingChunkEdge = 8;

        /// <summary>
        /// Regions whose brick pointer grid changed and must be re-uploaded to the GPU mirror.
        /// Separate from <see cref="DirtyRegions"/> because the two consumers drain at different
        /// rates: the mesher takes one region per budget slice, the uploader takes all of them.
        /// </summary>
        private readonly HashSet<int3> _regionsNeedingUpload = new();
        private readonly List<int3> _pendingLoads = new();

        public ref RegionTable Table => ref _table;
        public ref BrickPool Pool => ref _pool;
        public MaterialPalette Palette => _palette;

        /// <summary>Regions whose geometry changed and need their surface mesh rebuilt.</summary>
        public HashSet<int3> DirtyRegions => _dirtyRegions;

        /// <summary>Regions whose brick pointers the renderer must re-upload.</summary>
        public HashSet<int3> RegionsNeedingUpload => _regionsNeedingUpload;

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
            _palette.Register(MatStone, 200, DestructionClass.Crumble);
            _palette.Register(MatWood, 90, DestructionClass.Splinter);
            _palette.Register(MatSand, 20, DestructionClass.Powder);
            _palette.Register(MatGlass, 10, DestructionClass.Powder);
            _palette.Register(MatBedrock, 255, DestructionClass.None);

            // Castle materials. Weathering and roofing read as different stone, which is most of
            // what stops masonry looking extruded.
            _palette.Register(6, 210, DestructionClass.Crumble);   // dark stone
            _palette.Register(7, 120, DestructionClass.Crumble);   // slate
            _palette.Register(8, 110, DestructionClass.Crumble);   // tile
            _palette.Register(9, 15, DestructionClass.Splinter);   // cloth
            _palette.Register(10, 25, DestructionClass.Powder);    // grass
            _palette.Register(11, 5, DestructionClass.Spreading);  // water
            _palette.Register(12, 180, DestructionClass.Crumble);  // gold
            _palette.Register(13, 30, DestructionClass.Powder);    // dirt
            _palette.Register(14, 40, DestructionClass.Powder);    // moss

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
                _dirtyRegions.Remove(rc);
                _regionsNeedingUpload.Remove(rc);
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
            _dirtyRegions.Add(coord);
            _regionsNeedingUpload.Add(coord);

            // Neighbours must re-mesh too: faces along the shared border were meshed as the edge
            // of the loaded world and are now interior.
            _dirtyRegions.Add(coord + new int3(1, 0, 0));
            _dirtyRegions.Add(coord + new int3(-1, 0, 0));
            _dirtyRegions.Add(coord + new int3(0, 0, 1));
            _dirtyRegions.Add(coord + new int3(0, 0, -1));

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
            _regionsNeedingUpload.Add(coord);

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

            var brush = CastleBuilder.Build(ref _table, ref _pool, in plan, Seed);
            _castlePlan = plan;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            BuildCastlePresentationLights(in plan);

            CastleVoxels = brush.TotalVoxelsWritten;

            // Everything the castle touched has to be re-meshed and re-uploaded.
            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
            {
                var rc = new int3(rx, 0, rz);
                _dirtyRegions.Add(rc);
                _regionsNeedingUpload.Add(rc);
            }
        }

        /// <summary>Voxels the castle wrote. Reported in the HUD so its cost is visible.</summary>
        public long CastleVoxels { get; private set; }

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

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            CastlePresentationLights = new[]
            {
                LightAt(plan.Centre.x - 45, baseY + 17, keepCentreZ - 28, 7.0f),
                LightAt(plan.Centre.x + 42, baseY + 17, keepCentreZ + 30, 7.0f),
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
                LightAt(plan.Centre.x - 40, dungeonY + 9, caveZ - 15, 9.0f),
                LightAt(plan.Centre.x + 45, dungeonY + 11, caveZ + 24, 9.0f),
            };

            var warm = new Vector4(1.00f, 0.38f, 0.10f, 2.35f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var caveBlue = new Vector4(0.12f, 0.62f, 1.00f, 1.85f);
            CastlePresentationLightColours = new[]
            {
                warm, warm, warm, warm, warm, warm, chapelWarm, chapelWarm,
                cellarWarm, cellarWarm, cellarWarm, cellarWarm,
                caveBlue, caveBlue,
            };
        }

        public bool CastleTrapdoorOpen => _castleTrapdoorOpen;

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
            var removed = new List<int3>(math.min(voxels.Length, 8192));

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

                if (VoxelAccess.SetVoxel(ref _table, ref _pool, v, VoxelDimensions.MaterialEmpty))
                {
                    changed++;
                    removed.Add(v);
                    MarkDirty(v);
                }
            }

            voxels.Dispose();
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
            if (!VoxelAccess.SetVoxel(ref _table, ref _pool, voxel, VoxelDimensions.MaterialEmpty))
                return 0;

            MarkDirty(voxel);
            var removed = new List<int3>(1) { voxel };
            int collapsed = ResolveUnsupportedAfterRemoval(removed, voxel, 1, default);
            LastEditVoxels = 1 + collapsed;
            return 1 + collapsed;
        }

        public bool TryDequeueDetachedChunk(out DetachedVoxelChunk chunk) =>
            _detachedChunks.TryDequeue(out chunk);

        /// <summary>Restores a chunk when the bounded GPU debris pool cannot accept it.</summary>
        public void RestoreDetachedChunk(DetachedVoxelChunk chunk)
        {
            for (int i = 0; i < chunk.Voxels.Length; i++)
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, chunk.Voxels[i], chunk.Materials[i]))
                    MarkDirty(chunk.Voxels[i]);
        }

        /// <summary>
        /// Voxelizes a settled, arbitrarily rotated presentation chunk back into authoritative
        /// grid cells. Occupied targets climb a few cells rather than overwriting world geometry.
        /// </summary>
        public void SettleDetachedChunk(DetachedVoxelChunk chunk, Vector3 pivotMetres,
                                        Quaternion rotation, Vector3 originalPivotMetres)
        {
            var occupiedTargets = new HashSet<int3>();
            for (int i = 0; i < chunk.Voxels.Length; i++)
            {
                Vector3 originalCentre = ((Vector3)(float3)chunk.Voxels[i] + Vector3.one * 0.5f)
                                       * VoxelSurfaceRenderer.VoxelSize;
                Vector3 targetCentre = pivotMetres + rotation * (originalCentre - originalPivotMetres);
                int3 target = (int3)math.round((float3)targetCentre / VoxelSurfaceRenderer.VoxelSize - 0.5f);

                int lift = 0;
                while (lift < 6 && (occupiedTargets.Contains(target)
                       || VoxelAccess.IsSolid(ref _table, in _pool, target)))
                {
                    target.y++;
                    lift++;
                }

                if (lift == 6) continue;
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, target, chunk.Materials[i]))
                {
                    occupiedTargets.Add(target);
                    MarkDirty(target);
                }
            }
        }

        /// <summary>Returns the centre height at which a detached chunk should meet solid world.</summary>
        public float FindLandingCentreY(float3 pivotMetres, float halfHeightMetres)
        {
            int x = (int)math.floor(pivotMetres.x / VoxelSurfaceRenderer.VoxelSize);
            int z = (int)math.floor(pivotMetres.z / VoxelSurfaceRenderer.VoxelSize);
            int startY = (int)math.floor((pivotMetres.y - halfHeightMetres)
                                        / VoxelSurfaceRenderer.VoxelSize) - 1;
            for (int y = math.min(startY, RegionVoxelEdge - 1); y >= 0; y--)
                if (VoxelAccess.IsSolid(ref _table, in _pool, new int3(x, y, z)))
                    return (y + 1) * VoxelSurfaceRenderer.VoxelSize + halfHeightMetres;
            return halfHeightMetres;
        }

        private int ResolveUnsupportedAfterRemoval(List<int3> removed, int3 impact, int radius,
                                                    float3 impulseDirection)
        {
            if (removed.Count == 0) return 0;

            // Deduplicate the solid boundary once. The previous removed×6 nested scan repeatedly
            // revisited empty neighbours and was the dominant cost when several tornadoes hit.
            var candidates = new HashSet<int3>();
            for (int r = 0; r < removed.Count; r++)
            for (int d = 0; d < s_Neighbours.Length; d++)
            {
                int3 candidate = removed[r] + s_Neighbours[d];
                if (VoxelAccess.IsSolid(ref _table, in _pool, candidate)) candidates.Add(candidate);
            }

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

                    component.Add(new FallingVoxel { Position = current, Material = material });
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

            collapsed += ResolveOverloadedSupport(impact, radius, impulseDirection);
            return collapsed;
        }

        /// <summary>
        /// Connectivity is necessary but not sufficient: a surviving one-voxel neck cannot carry
        /// an entire tower. Find the weakest vertical contact plane through the damaged band,
        /// then compare the mass above it with material-weighted contact capacity.
        /// </summary>
        private int ResolveOverloadedSupport(int3 impact, int radius, float3 impulseDirection)
        {
            int influenceRadius = math.clamp(radius * 4 + 48, 64, 128);
            int scanRadius = influenceRadius;
            int minY = math.max(0, impact.y - radius - 2);
            int maxY = math.min(RegionVoxelEdge - 2, impact.y + radius + 2);
            int yStep = math.max(1, radius / 8);
            int weakestPlane = impact.y;
            int weakestContacts = int.MaxValue;
            long weakestScore = long.MaxValue;

            for (int y = minY; y <= maxY; y += yStep)
            {
                int contacts = CountVerticalContacts(impact.x, impact.z, y, scanRadius);
                long score = (long)contacts * (4 + math.abs(y - impact.y));
                if (contacts > 0 && score < weakestScore)
                {
                    weakestContacts = contacts;
                    weakestPlane = y;
                    weakestScore = score;
                }
            }
            // The exact impact layer matters even when a large brush samples every few layers.
            int impactContacts = CountVerticalContacts(impact.x, impact.z, impact.y, scanRadius);
            if (impactContacts > 0 && (long)impactContacts * 4 < weakestScore)
            {
                weakestContacts = impactContacts;
                weakestPlane = impact.y;
                weakestScore = (long)impactContacts * 4;
            }
            if (weakestContacts == int.MaxValue) return 0;

            int collapsed = 0;
            int seedY = weakestPlane + 1;
            int radiusSq = scanRadius * scanRadius;
            int influenceRadiusSq = influenceRadius * influenceRadius;
            var visited = new HashSet<int3>();

            for (int dz = -scanRadius; dz <= scanRadius; dz++)
            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                int3 seed = new int3(impact.x + dx, seedY, impact.z + dz);
                if (visited.Contains(seed) || !VoxelAccess.IsSolid(ref _table, in _pool, seed))
                    continue;

                var component = new List<FallingVoxel>(512);
                var stack = new Stack<int3>();
                stack.Push(seed);
                visited.Add(seed);
                long supportCapacity = 0;
                bool containsStructuralMaterial = false;
                bool overflow = false;

                while (stack.Count > 0)
                {
                    int3 current = stack.Pop();
                    byte material = VoxelAccess.GetVoxel(ref _table, in _pool, current);
                    if (material == VoxelDimensions.MaterialEmpty) continue;
                    containsStructuralMaterial |= IsStructuralMaterial(material);
                    component.Add(new FallingVoxel { Position = current, Material = material });
                    if (component.Count >= MaxCollapseComponentVoxels)
                    {
                        overflow = true;
                        break;
                    }

                    if (current.y == seedY)
                    {
                        byte below = VoxelAccess.GetVoxel(ref _table, in _pool,
                                                         current + new int3(0, -1, 0));
                        if (below != VoxelDimensions.MaterialEmpty)
                            supportCapacity += 48 + _palette.GetHardness(below);
                    }

                    for (int n = 0; n < s_Neighbours.Length; n++)
                    {
                        int3 next = current + s_Neighbours[n];
                        int relX = next.x - impact.x;
                        int relZ = next.z - impact.z;
                        if (next.y <= weakestPlane || visited.Contains(next)
                            || relX * relX + relZ * relZ > influenceRadiusSq
                            || !VoxelAccess.IsSolid(ref _table, in _pool, next)) continue;
                        visited.Add(next);
                        stack.Push(next);
                    }
                }

                if (!overflow && containsStructuralMaterial && component.Count > supportCapacity)
                    collapsed += DetachComponent(component, impact, impulseDirection);
            }

            return collapsed;
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
            MatWood or MatGlass or 6 or 7 or 8 or 9 or 12 => true,
            _ => false,
        };

        private int DetachComponent(List<FallingVoxel> component, int3 impact,
                                    float3 impulseDirection)
        {
            var detached = new List<FallingVoxel>(component.Count);
            for (int i = 0; i < component.Count; i++)
            {
                var voxel = component[i];
                if (VoxelAccess.SetVoxel(ref _table, ref _pool, voxel.Position,
                                         VoxelDimensions.MaterialEmpty))
                {
                    MarkDirty(voxel.Position);
                    detached.Add(voxel);
                }
            }
            if (detached.Count == 0) return 0;

            // Spatial buckets make coherent small chunks instead of arbitrary graph slices.
            var buckets = new Dictionary<int3, List<FallingVoxel>>();
            for (int i = 0; i < detached.Count; i++)
            {
                int3 p = detached[i].Position;
                int3 bucket = new int3(FloorDiv(p.x, FallingChunkEdge),
                                       FloorDiv(p.y, FallingChunkEdge),
                                       FloorDiv(p.z, FallingChunkEdge));
                if (!buckets.TryGetValue(bucket, out var voxels))
                    buckets.Add(bucket, voxels = new List<FallingVoxel>(512));
                voxels.Add(detached[i]);
            }

            foreach (var pair in buckets)
            {
                var voxels = pair.Value;
                var chunk = new DetachedVoxelChunk
                {
                    Voxels = new int3[voxels.Count],
                    Materials = new byte[voxels.Count],
                    ImpactMetres = ((float3)impact + 0.5f) * VoxelSurfaceRenderer.VoxelSize,
                    ImpulseDirection = impulseDirection,
                };
                for (int i = 0; i < voxels.Count; i++)
                {
                    chunk.Voxels[i] = voxels[i].Position;
                    chunk.Materials[i] = voxels[i].Material;
                }
                _detachedChunks.Enqueue(chunk);
            }

            return detached.Count;
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
        /// Marks the region owning this voxel for a mesh rebuild, plus the neighbour when the
        /// voxel sits on a region border — a face there is exposed by geometry on the far side.
        /// </summary>
        private void MarkDirty(int3 voxel)
        {
            var rc = new int3(voxel.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                              voxel.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                              voxel.z >> VoxelDimensions.RegionVoxelEdgeLog2);
            _dirtyRegions.Add(rc);

            // An edit can allocate or collapse a brick, which rewrites the pointer, so the GPU
            // copy of this region's grid is stale until it is sent again.
            _regionsNeedingUpload.Add(rc);

            int lx = voxel.x & (RegionVoxelEdge - 1);
            int lz = voxel.z & (RegionVoxelEdge - 1);

            if (lx == 0) _dirtyRegions.Add(rc + new int3(-1, 0, 0));
            if (lx == RegionVoxelEdge - 1) _dirtyRegions.Add(rc + new int3(1, 0, 0));
            if (lz == 0) _dirtyRegions.Add(rc + new int3(0, 0, -1));
            if (lz == RegionVoxelEdge - 1) _dirtyRegions.Add(rc + new int3(0, 0, 1));
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
            int cx = RegionVoxelEdge / 2;
            const int cz = -80;
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
