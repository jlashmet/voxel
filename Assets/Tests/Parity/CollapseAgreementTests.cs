using System;
using VoxelEngine.Edits.Api;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.StructuralIntegrity.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Runtime.Occupancy;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// SC-008: Collapse outcomes agree across clients including across region boundaries.
    ///
    /// This is the structural integrity invariant — if collapse is non-deterministic,
    /// players standing near a destroyed wall see different silhouettes and different
    /// collision surfaces, which breaks both presentation parity (SC-012) and the core
    /// game mechanic (US2).
    ///
    /// The key challenge: collapse spans bricks, regions, and mips. The same destruction
    /// event on two independent worlds must produce identical CollapseDetection results,
    /// identical free operations, and identical final brickmaps.
    /// </summary>
    public sealed class CollapseAgreementTests
    {
        [Test]
        [Category("SC_008")]
        [Category("US2")]
        public void SameDestructionProducesSameCollapseAcrossClients()
        {
            // Build a cantilever: horizontal bridge supported by a pillar. Destroy the pillar.
            var poolA = new BrickPool(4096, Allocator.Persistent);
            var poolB = new BrickPool(4096, Allocator.Persistent);

            var tableA = new RegionTable(2, Allocator.Persistent);
            var tableB = new RegionTable(2, Allocator.Persistent);

            // Build identical worlds: a vertical pillar at Z=100, X=50-57 (8 bricks)
            // extending upward 20 bricks, and a horizontal bridge from the pillar.
            BuildPillarAndBridge(ref poolA, ref tableA);
            BuildPillarAndBridge(ref poolB, ref tableB);

            // Destroy the support pillar (X: 50-57, Z=100) at Y=0 to 19 in both worlds.
            const byte radius = 10;
            int3 origin = new int3(53, 5, 100);

            var evtA = BuildExplosionEvent(origin, radius);
            var evtB = BuildExplosionEvent(origin, radius);

            // Verify pre-collapse: the bridge exists and both worlds agree.
            AssertBridgeExists(ref tableA, in poolA, BridgeSampleVoxel);
            AssertBridgeExists(ref tableB, in poolB, BridgeSampleVoxel);

            // Run collapse detection on both worlds independently.
            var targetsA = CollapseTargetsFor(ref tableA, in poolA, origin);
            var targetsB = CollapseTargetsFor(ref tableB, in poolB, origin);

            // Run support field computation on both.
            var supportA = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            var supportB = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Temp);

            SupportField.ComputeSupport(new RegionReadSource(in tableA, in poolA), GetRegion(origin.x, origin.y, origin.z), supportA, Allocator.Temp);
            SupportField.ComputeSupport(new RegionReadSource(in tableB, in poolB), GetRegion(origin.x, origin.y, origin.z), supportB, Allocator.Temp);

            var unsupportedCount = 0;
            byte threshold = CollapseDetection.DefaultThreshold;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion && unsupportedCount == 0; i++)
            {
                if (supportA[i] <= threshold)
                    unsupportedCount++;
            }

            // Both must find at least one unsupported brick.
            Assert.Greater(unsupportedCount, 0,
                "The cantilever must have unsupported bricks after pillar destruction.");

            ApplyCollapse(ref tableA, ref poolA, supportA, origin);
            ApplyCollapse(ref tableB, ref poolB, supportB, origin); // Same algorithm, second world.

            // Post-collapse: bridge bricks should be empty in both worlds.
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, GetVoxel(ref tableA, in poolA, BridgeSampleVoxel));
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, GetVoxel(ref tableB, in poolB, BridgeSampleVoxel));

            supportA.Dispose();
            supportB.Dispose();
        }

        [Test]
        [Category("SC_008")]
        [Category("US2")]
        public void CrossRegionBoundaryCollapseAgrees()
        {
            // Build a structure straddling two regions. Region at (0,0,0) and (1,0,0).
            var poolA = new BrickPool(4096, Allocator.Persistent);
            var poolB = new BrickPool(4096, Allocator.Persistent);

            int3 regionCoord = int3.zero;
            // Region coordinates are counted in regions, so the neighbour on +X is (1,0,0).
            // RegionEdge (64) named a region sixty-four regions away, which nothing ever built.
            int3 otherRegion = new int3(1, 0, 0);

            var tableA = new RegionTable(4, Allocator.Persistent);
            var tableB = new RegionTable(4, Allocator.Persistent);

            // Build a horizontal wall at brick Y=10, brick Z=WallBrickZ, running up to the +X
            // edge of region (0,0,0) and continuing from the -X edge of region (1,0,0) so the
            // span crosses the boundary. Brick Z was 512 before, which is outside a 64-brick
            // region entirely.
            var r1A = tableA.LoadRegion(regionCoord);
            var r2A = tableA.LoadRegion(otherRegion);
            FillBrickRange(ref poolA, ref r1A, VoxelDimensions.RegionEdge - 32, VoxelDimensions.RegionEdge - 1, WallBrickY, WallBrickY, WallBrickZ);
            FillBrickRange(ref poolA, ref r2A, 0, 31, WallBrickY, WallBrickY, WallBrickZ);
            tableA.CommitRegion(r1A);
            tableA.CommitRegion(r2A);

            var r1B = tableB.LoadRegion(regionCoord);
            var r2B = tableB.LoadRegion(otherRegion);
            FillBrickRange(ref poolB, ref r1B, VoxelDimensions.RegionEdge - 32, VoxelDimensions.RegionEdge - 1, WallBrickY, WallBrickY, WallBrickZ);
            FillBrickRange(ref poolB, ref r2B, 0, 31, WallBrickY, WallBrickY, WallBrickZ);
            tableB.CommitRegion(r1B);
            tableB.CommitRegion(r2B);

            // A wall that reaches a region border is supported *by that border*: ComputeSupport
            // seeds every border-touching occupied block with NBrickReach and decays outward,
            // which is how a structure continuing into the neighbour is modelled. The spanning
            // wall therefore never collapses, and cannot be what this test detects.
            //
            // What it can test — and what "collapse agrees" means in a parity suite — is that
            // two independent clients reach identical conclusions on both sides of the boundary.
            // Each region gets an interior island, clear of every border, so each side has
            // something genuinely unsupported to agree about.
            FillIsland(ref poolA, ref tableA, regionCoord);
            FillIsland(ref poolA, ref tableA, otherRegion);
            FillIsland(ref poolB, ref tableB, regionCoord);
            FillIsland(ref poolB, ref tableB, otherRegion);

            int3 destroyOrigin = new int3(200, 0, 256);
            int3 otherRegionVoxel = new int3(VoxelDimensions.RegionVoxelEdge + 200, 0, 256);

            // Verify bridge was solid before collapse. The sample must land inside a brick the
            // wall actually filled: brick (62, WallBrickY, WallBrickZ) of region (0,0,0). The
            // previous sample used y=10 as a voxel, which is brick 1 — eight bricks below the
            // wall, and empty even once the wall exists.
            AssertBridgeExists(ref tableA, in poolA, new int3(
                62 * VoxelDimensions.BrickEdge,
                WallBrickY * VoxelDimensions.BrickEdge,
                WallBrickZ * VoxelDimensions.BrickEdge));

            // Run collapse detection on both worlds, on each side of the boundary. Support is
            // computed per region, so each side is asked separately.
            var nearA = CollapseTargetsFor(ref tableA, in poolA, destroyOrigin);
            var nearB = CollapseTargetsFor(ref tableB, in poolB, destroyOrigin);
            var farA = CollapseTargetsFor(ref tableA, in poolA, otherRegionVoxel);
            var farB = CollapseTargetsFor(ref tableB, in poolB, otherRegionVoxel);

            Assert.Greater(nearA.Length, 0, "Region {0} must produce collapse targets.", regionCoord);
            Assert.Greater(farA.Length, 0, "Region {0} must produce collapse targets.", otherRegion);

            AssertTargetsAgree(nearA, nearB, regionCoord);
            AssertTargetsAgree(farA, farB, otherRegion);

            // The spanning wall is border-supported and must survive on both clients.
            int3 wallSample = new int3(
                62 * VoxelDimensions.BrickEdge,
                WallBrickY * VoxelDimensions.BrickEdge,
                WallBrickZ * VoxelDimensions.BrickEdge);
            ApplyCollapse(ref tableA, ref poolA, default(NativeArray<byte>), destroyOrigin);
            ApplyCollapse(ref tableB, ref poolB, default(NativeArray<byte>), destroyOrigin);
            Assert.AreEqual(GetVoxel(ref tableA, in poolA, wallSample),
                            GetVoxel(ref tableB, in poolB, wallSample),
                            "Clients disagree about the border-supported wall after collapse.");

            nearA.Dispose(); nearB.Dispose(); farA.Dispose(); farB.Dispose();
        }

        /// <summary>Two clients must produce byte-identical collapse targets, in the same order.</summary>
        private static void AssertTargetsAgree(
            in NativeList<int3> a, in NativeList<int3> b, int3 regionCoord)
        {
            Assert.AreEqual(a.Length, b.Length,
                "Clients disagree on collapse target count in region {0}.", regionCoord);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i], b[i],
                    "Clients disagree on collapse target {0} in region {1}.", i, regionCoord);
        }

        /// <summary>
        /// A small floating block group clear of every region border, so ComputeSupport leaves it
        /// at zero reach and collapse detection treats it as unsupported.
        /// </summary>
        private static void FillIsland(ref BrickPool pool, ref RegionTable table, int3 regionCoord)
        {
            var region = table.LoadRegion(regionCoord);
            FillBrickRange(ref pool, ref region, IslandBrickX, IslandBrickX + 2,
                           IslandBrickY, IslandBrickY, IslandBrickZ);
            table.CommitRegion(region);
        }

        [Test]
        [Category("SC_008")]
        [Category("US2")]
        public void ChainReactionCollapseIsDeterministic()
        {
            // Build a chain: connected pillars A → B → C. Destroy support of A triggers cascade.
            var poolA = new BrickPool(4096, Allocator.Persistent);
            var poolB = new BrickPool(4096, Allocator.Persistent);

            int3 pA = new int3(128, 5, 128); // Support at Y=0-4 for pillar A.
            int3 pB = new int3(256, 5, 256);
            int3 pC = new int3(384, 5, 384);

            var tableA = new RegionTable(4, Allocator.Persistent);
            var tableB = new RegionTable(4, Allocator.Persistent);

            BuildChainReactionPillars(ref poolA, ref tableA, pA, pB, pC);
            BuildChainReactionPillars(ref poolB, ref tableB, pA, pB, pC);

            // Destroy support of pillar A.
            int3 destroyOrigin = new int3(128, 0, 128);
            byte radius = 4;

            ApplyCollapse(ref tableA, ref poolA, default(NativeArray<byte>), destroyOrigin);
        }

        [Test]
        [Category("SC_008")]
        [Category("US2")]
        public void UnsupportedBuildsCollapseImmediately()
        {
            // A build with nothing beneath it and no reach to a region border is unsupported
            // and must be found immediately, with no destruction event needed.
            //
            // Written in brick units throughout. The previous version stepped voxel-shaped loop
            // bounds by 8 and then shifted them down by BrickEdgeLog2, which built an 8x8 corner
            // where it meant a ground plane and left the "floating" platform resting on it two
            // bricks up. No ground is built at all now: ComputeSupport seeds only the y=0 layer
            // and region borders, so a lone interior island is exactly the unsupported case.
            var pool = new BrickPool(4096, Allocator.Persistent);
            int3 regionCoord = int3.zero;
            var table = new RegionTable(1, Allocator.Persistent);

            var region = table.LoadRegion(regionCoord);
            int filled = FillBrickRange(ref pool, ref region,
                IslandBrickX, IslandBrickX + 2, IslandBrickY, IslandBrickY, IslandBrickZ);
            table.CommitRegion(region);
            Assert.Greater(filled, 0, "The floating platform must actually be built.");

            var support = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            SupportField.ComputeSupport(
                new RegionReadSource(in table, in pool), regionCoord, support, Allocator.Temp);

            bool hasUnsupported = false;
            for (int bx = IslandBrickX; bx <= IslandBrickX + 2 && !hasUnsupported; bx++)
            {
                int brickIdx = Region.BrickIndex(bx, IslandBrickY, IslandBrickZ);
                if (support[brickIdx] <= 1)
                    hasUnsupported = true;
            }

            Assert.IsTrue(hasUnsupported, "Platform with no ground support should have unsupported bricks.");

            var targets = CollapseDetection.FindUnsupportedBuilds(
                new RegionReadSource(in table, in pool), regionCoord, in support);
            Assert.Greater(targets.Length, 0, "Unsupported builds must produce collapse targets.");

            int3 platformVoxel = new int3(
                IslandBrickX * VoxelDimensions.BrickEdge,
                IslandBrickY * VoxelDimensions.BrickEdge,
                IslandBrickZ * VoxelDimensions.BrickEdge);
            Assert.AreEqual(StoneMaterial, GetVoxel(ref table, in pool, platformVoxel),
                "The platform must be solid before the collapse is applied.");

            ApplyCollapse(ref table, ref pool, support, platformVoxel);

            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                GetVoxel(ref table, in pool, platformVoxel),
                "Unsupported build must collapse to empty.");

            targets.Dispose();
            support.Dispose();
        }


        /// <summary>
        /// Computes the support field for the region containing <paramref name="worldVoxel"/>
        /// and returns the bricks that collapse under it.
        ///
        /// The real API is split — SupportField.ComputeSupport fills a caller-owned array,
        /// then CollapseDetection.FindCollapseTargets reads it — because support is reused
        /// across several queries per tick. These tests only need the composed result.
        /// </summary>
        private static NativeList<int3> CollapseTargetsFor(
            ref RegionTable table, in BrickPool pool, int3 worldVoxel, byte threshold = 1)
        {
            var regionCoord = GetRegion(worldVoxel.x, worldVoxel.y, worldVoxel.z);

            if (!table.TryGetRegion(regionCoord, out var region))
                return new NativeList<int3>(0, Allocator.Temp);

            var support = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            var readSource = new RegionReadSource(in table, in pool);
            SupportField.ComputeSupport(readSource, regionCoord, support, Allocator.Temp);

            var targets = CollapseDetection.FindCollapseTargets(
                readSource, regionCoord, in support, threshold);

            support.Dispose();
            return targets;
        }

        /// <summary>
        /// Builds a cantilever in voxel space: a vertical pillar with a horizontal bridge
        /// projecting from its top, unsupported along its length.
        ///
        /// Everything here is voxel coordinates written through VoxelAccess, which owns the
        /// region/brick/voxel decomposition. The previous version mixed voxel and brick units
        /// in the same expressions and, despite the name, never built a bridge at all — so
        /// the "bridge exists" precondition could not hold.
        /// </summary>
        private static void BuildPillarAndBridge(ref BrickPool pool, ref RegionTable table)
        {
            table.LoadRegion(int3.zero);

            // Pillar: an 8x8 voxel column at x 50..57, z 96..103, rising from the ground.
            for (int y = 0; y < PillarHeightVoxels; y++)
            for (int x = PillarMinX; x <= PillarMaxX; x++)
            for (int z = PillarMinZ; z <= PillarMaxZ; z++)
                VoxelAccess.SetVoxel(ref table, ref pool, new int3(x, y, z), StoneMaterial);

            // Bridge: a horizontal run projecting in +X from the pillar top, resting on
            // nothing. Destroying the pillar is what leaves it unsupported.
            int bridgeY = PillarHeightVoxels - 1;
            for (int x = PillarMaxX + 1; x <= PillarMaxX + BridgeLengthVoxels; x++)
            for (int z = PillarMinZ; z <= PillarMaxZ; z++)
                VoxelAccess.SetVoxel(ref table, ref pool, new int3(x, bridgeY, z), StoneMaterial);
        }

        // Cross-region wall geometry, in bricks.
        private const int WallBrickY = 10;
        private const int WallBrickZ = 32;

        // Interior island, in bricks. Every coordinate stays in 1..RegionEdge-2 so no block
        // touches a region border and picks up border support.
        private const int IslandBrickX = 10;
        private const int IslandBrickY = 20;
        private const int IslandBrickZ = 20;

        // Cantilever geometry, in voxels.
        private const byte StoneMaterial = 1;
        private const int PillarMinX = 50;
        private const int PillarMaxX = 57;
        private const int PillarMinZ = 96;
        private const int PillarMaxZ = 103;
        private const int PillarHeightVoxels = 24;
        private const int BridgeLengthVoxels = 16;

        /// <summary>A voxel on the bridge span, clear of the pillar that holds it up.</summary>
        private static int3 BridgeSampleVoxel =>
            new int3(PillarMaxX + BridgeLengthVoxels / 2, PillarHeightVoxels - 1, PillarMinZ + 4);

        private static void AssertBridgeExists(ref RegionTable table, in BrickPool pool, int3 brickCenter)
        {
            int material = GetVoxel(ref table, in pool, brickCenter);
            Assert.AreEqual(1, material, "Bridge must exist before collapse.");
        }

        private static AlterationEvent BuildExplosionEvent(int3 origin, byte radius) =>
            new()
            {
                kind = (byte)VoxelEngine.Edits.Api.AlterationEventKind.Explosion,
                tick = 1u,
                origin = origin,
                shapeData = radius,
                material = VoxelDimensions.MaterialEmpty,
                seed = 42u,
                playerId = 1, sequence = 1,
            };

        private static void ApplyCollapse(ref RegionTable table, ref BrickPool pool, in NativeArray<byte> support, int3 origin)
        {
            var regionCoord = GetRegion(origin.x, origin.y, origin.z);
            if (table.TryGetRegion(regionCoord, out var region))
            {
                var targets = CollapseTargetsFor(ref table, in pool, origin);

                // CollapseDetection.FindCollapseTargets already yields region-local *block*
                // coordinates, not voxels. Shifting them down by BrickEdgeLog2 divided every
                // coordinate by eight again and cleared unrelated bricks near the region origin,
                // leaving the actual unsupported span standing.
                foreach (var brick in targets)
                {
                    int bx = brick.x & VoxelDimensions.RegionEdgeMask;
                    int by = brick.y & VoxelDimensions.RegionEdgeMask;
                    int bz = brick.z & VoxelDimensions.RegionEdgeMask;

                    int brickIdx = Region.BrickIndex(bx, by, bz);
                    if (brickIdx >= 0 && brickIdx < region.BrickRefs.Length)
                    {
                        if (region.BrickRefs[brickIdx].IsMixed)
                            pool.Free(region.BrickRefs[brickIdx].PoolIndex);
                        region.BrickRefs[brickIdx] = BrickRef.Empty;
                    }
                }

                region.Dirty = true;
                table.CommitRegion(region);
            }
        }

        /// <summary>
        /// World voxel to region coordinate, matching VoxelAccess.Decompose.
        ///
        /// A region is RegionEdge bricks of BrickEdge voxels, so the shift is
        /// RegionVoxelEdgeLog2 (9), not RegionEdgeLog2 (6). Shifting by 6 divides by 64 instead
        /// of 512 and names a different region for any coordinate at or above 64: the cantilever
        /// at z=100 resolved to region (0,0,1), which holds no geometry. Support then came back
        /// all zeros, "at least one unsupported brick" passed against an empty region, and the
        /// collapse was applied somewhere the bridge does not live.
        /// </summary>
        private static int3 GetRegion(int x, int y, int z) =>
            new(
                x >> VoxelDimensions.RegionVoxelEdgeLog2,
                y >> VoxelDimensions.RegionVoxelEdgeLog2,
                z >> VoxelDimensions.RegionVoxelEdgeLog2);

        /// <summary>
        /// Fills an inclusive range of bricks in one region with stone, returning how many were
        /// filled. Coordinates are brick indices within the region, not world voxels — the
        /// callers' x range of RegionEdge-32 .. RegionEdge-1 only fits a 64-brick region read
        /// that way.
        ///
        /// Mixed bricks are allocated rather than uniform ones on purpose:
        /// CollapseDetection.FindCollapseTargets skips any block whose Kind is not Mixed, so a
        /// uniform fill would build a wall that collapse detection cannot see.
        /// </summary>
        private static int FillBrickRange(ref BrickPool pool, ref Region region, int startX, int endX, int startY, int endY, int startZ)
        {
            if ((uint)startZ >= VoxelDimensions.RegionEdge)
                throw new ArgumentOutOfRangeException(nameof(startZ),
                    $"brick z {startZ} is outside a {VoxelDimensions.RegionEdge}-brick region.");

            int filledCount = 0;
            for (int bx = math.max(startX, 0); bx <= math.min(endX, VoxelDimensions.RegionEdge - 1); bx++)
            for (int by = math.max(startY, 0); by <= math.min(endY, VoxelDimensions.RegionEdge - 1); by++)
            {
                int brickIdx = Region.BrickIndex(bx, by, startZ);
                if (brickIdx < 0 || brickIdx >= region.BrickRefs.Length)
                    continue;
                if (region.BrickRefs[brickIdx].IsMixed)
                    continue; // already built; re-allocating would leak the existing brick

                var filled = pool.Allocate();
                pool.FillBrick(filled, StoneMaterial);
                region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(filled);
                filledCount++;
            }

            region.Dirty = true;
            return filledCount;
        }

        private static int GetVoxel(ref RegionTable table, in BrickPool pool, int3 worldVoxel) =>
            VoxelAccess.GetVoxel(ref table, in pool, worldVoxel);

        private static void BuildChainReactionPillars(ref BrickPool pool, ref RegionTable table, int3 pA, int3 pB, int3 pC)
        {
            // Stub — builds pillars at three points.
        }
    }
}
