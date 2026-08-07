using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;
using VoxelEngine.Streaming;

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
            "empty", "stone", "wood", "sand", "glass", "bedrock"
        };

        // -- geometry constants --------------------------------------------------

        /// <summary>Voxels along a region edge: 512, i.e. 51.2 m.</summary>
        public const int RegionVoxelEdge = 1 << VoxelDimensions.RegionVoxelEdgeLog2;

        /// <summary>Metres along a region edge.</summary>
        public const float RegionMetres = RegionVoxelEdge * 0.1f;

        /// <summary>Base terrain height in voxels. Terrain stays inside the y = 0 region layer.</summary>
        private const int BaseHeight = 220;

        // -- state ---------------------------------------------------------------

        private RegionTable _table;
        private BrickPool _pool;
        private MaterialPalette _palette;
        private uint _editCounter;

        private readonly HashSet<int3> _generated = new();
        private readonly HashSet<int3> _dirtyRegions = new();
        private readonly List<int3> _pendingLoads = new();

        public ref RegionTable Table => ref _table;
        public ref BrickPool Pool => ref _pool;
        public MaterialPalette Palette => _palette;

        /// <summary>Regions whose geometry changed and need their surface mesh rebuilt.</summary>
        public HashSet<int3> DirtyRegions => _dirtyRegions;

        public uint Seed { get; }

        /// <summary>Regions in the wanted set that have not been generated yet.</summary>
        public int PendingRegionLoads => _pendingLoads.Count;

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
                int dx = rc.x - centre.x;
                int dz = rc.z - centre.z;

                if (dx * dx + dz * dz <= UnloadRadiusRegions * UnloadRadiusRegions) continue;

                // No write-back: the client owns no truth, so eviction discards and the region
                // regenerates from the seed on return.
                ResidencyManager.EvictWithoutWriteBack(rc, ref _table, ref _pool);
                _generated.Remove(rc);
                _dirtyRegions.Remove(rc);
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

            // Neighbours must re-mesh too: faces along the shared border were meshed as the edge
            // of the loaded world and are now interior.
            _dirtyRegions.Add(coord + new int3(1, 0, 0));
            _dirtyRegions.Add(coord + new int3(-1, 0, 0));
            _dirtyRegions.Add(coord + new int3(0, 0, 1));
            _dirtyRegions.Add(coord + new int3(0, 0, -1));

            RegionsGenerated++;

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
            if (y == surface) return surface < BaseHeight ? MatSand : MatStone;
            if (y > surface - DeepDepth) return MatStone;
            return MatBedrock;
        }

        /// <summary>
        /// Deterministic integer value noise, summed over four octaves.
        ///
        /// Demo-side rather than <see cref="Core.Terrain.TerrainGenerator.SampleSurfaceHeight"/>,
        /// which reduces its inputs modulo the region edge and therefore produces the same
        /// terrain in every region — a seam-free world needs the noise to be a function of the
        /// world coordinate, not the region-local one. Worth fixing in the engine; flagged
        /// rather than worked around silently.
        /// </summary>
        public int SurfaceHeight(int wx, int wz)
        {
            int h = BaseHeight;
            // Deliberately smooth at voxel scale: high-frequency detail turns every 10 cm step
            // into extra faces, and the mesh cost of that is far larger than the visual gain.
            h += Octave(wx, wz, 9, 70);
            h += Octave(wx, wz, 7, 24);
            h += Octave(wx, wz, 5, 6);
            return math.clamp(h, 8, RegionVoxelEdge - 24);
        }

        /// <summary>One octave of value noise: hash four lattice corners, interpolate in fixed point.</summary>
        private int Octave(int wx, int wz, int log2Cell, int amplitude)
        {
            int cell = 1 << log2Cell;
            int x0 = wx >> log2Cell, z0 = wz >> log2Cell;
            int fx = wx & (cell - 1), fz = wz & (cell - 1);

            int c00 = Corner(x0, z0, log2Cell);
            int c10 = Corner(x0 + 1, z0, log2Cell);
            int c01 = Corner(x0, z0 + 1, log2Cell);
            int c11 = Corner(x0 + 1, z0 + 1, log2Cell);

            // Smoothstep in fixed point, 0..1024, so the lattice does not show as creases.
            int tx = Smooth(fx, cell);
            int tz = Smooth(fz, cell);

            int a = c00 + ((c10 - c00) * tx >> 10);
            int b = c01 + ((c11 - c01) * tx >> 10);
            int v = a + ((b - a) * tz >> 10);   // v is 0..1024

            return (v * amplitude >> 10) - (amplitude >> 1);
        }

        private int Corner(int x, int z, int salt) =>
            (int)((Hash((uint)x * 2654435761u ^ (uint)z * 2246822519u ^ Seed ^ ((uint)salt << 24)) >> 8) & 0x3FF);

        private static int Smooth(int f, int cell)
        {
            long t = (long)f * 1024 / cell;             // 0..1024
            return (int)(t * t * (3 * 1024 - 2 * t) >> 20); // 3t^2 - 2t^3, still 0..1024
        }

        private static uint Hash(uint v)
        {
            v ^= v >> 16; v *= 0x85ebca6bu;
            v ^= v >> 13; v *= 0xc2b2ae35u;
            v ^= v >> 16;
            return v;
        }

        // -- landmarks -----------------------------------------------------------

        /// <summary>
        /// Hand-built structures at the spawn point, so there is something with corners and
        /// distinct materials to blow up before you go looking at terrain.
        /// </summary>
        private void BuildLandmarks()
        {
            int cx = RegionVoxelEdge / 2;
            int cz = RegionVoxelEdge / 2;
            int ground = SurfaceHeight(cx, cz);

            var towerBase = new int3(cx, ground - 2, cz + 40);
            StampCylinder(towerBase, 14, 70, MatStone);
            StampCylinder(new int3(towerBase.x, towerBase.y + 3, towerBase.z), 11, 66, VoxelDimensions.MaterialEmpty);
            StampCylinder(new int3(towerBase.x, towerBase.y + 70, towerBase.z), 17, 4, MatWood);

            // Wall with a glass band, both destructible but with very different hardness.
            FillBox(new int3(cx - 60, ground - 2, cz - 40), new int3(120, 26, 6), MatWood);
            FillBox(new int3(cx - 60, ground + 10, cz - 40), new int3(120, 8, 6), MatGlass);

            // Bedrock pillars: DestructionClass.None, so blasts leave them standing.
            for (int i = 0; i < 4; i++)
                StampCylinder(new int3(cx - 45 + i * 30, ground - 2, cz + 90), 5, 34, MatBedrock);

            StampSphere(new int3(cx + 60, ground + 8, cz - 10), 18, MatSand);
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
        public int Explode(int3 centre, ushort radius)
        {
            var rng = new DeterministicRandom(MixSeed(centre, radius));
            var voxels = BuildBrushes.PlaceSphere(centre, radius, VoxelDimensions.MaterialEmpty, Seed);

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
                    MarkDirty(v);
                }
            }

            voxels.Dispose();
            _editCounter++;
            LastEditVoxels = changed;
            return changed;
        }

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

        /// <summary>Spawn point in metres: above the terrain at the centre of region (0,0,0).</summary>
        public Vector3 SpawnPosition()
        {
            int cx = RegionVoxelEdge / 2;
            int cz = RegionVoxelEdge / 2;
            int h = SurfaceHeight(cx, cz);
            return new Vector3(cx * 0.1f, (h + 40) * 0.1f, (cz - 90) * 0.1f);
        }

        public void Dispose()
        {
            FinishRegionForced();
            if (_table.IsCreated) _table.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
        }
    }
}
