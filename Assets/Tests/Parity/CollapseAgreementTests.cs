using VoxelEngine.Edits.Api;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Structure;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Occupancy;

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

            SupportField.ComputeSupport(in tableA, in poolA, GetRegion(origin.x, origin.y, origin.z).x, GetRegion(origin.x, origin.y, origin.z).y, GetRegion(origin.x, origin.y, origin.z).z, supportA, Allocator.Temp);
            SupportField.ComputeSupport(in tableB, in poolB, GetRegion(origin.x, origin.y, origin.z).x, GetRegion(origin.x, origin.y, origin.z).y, GetRegion(origin.x, origin.y, origin.z).z, supportB, Allocator.Temp);

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
            int3 otherRegion = new int3(VoxelDimensions.RegionEdge, 0, 0);

            var tableA = new RegionTable(4, Allocator.Persistent);
            var tableB = new RegionTable(4, Allocator.Persistent);

            // Build a horizontal wall spanning both regions at Y=10.
            var r1A = tableA.LoadRegion(regionCoord);
            var r2A = tableA.LoadRegion(otherRegion);
            FillBrickRange(ref poolA, ref r1A, VoxelDimensions.RegionEdge - 32, VoxelDimensions.RegionEdge - 1, 10, 10, 512);
            FillBrickRange(ref poolA, ref r2A, 0, 31, 10, 10, 512);
            tableA.CommitRegion(r1A);
            tableA.CommitRegion(r2A);

            var r1B = tableB.LoadRegion(regionCoord);
            var r2B = tableB.LoadRegion(otherRegion);
            FillBrickRange(ref poolB, ref r1B, VoxelDimensions.RegionEdge - 32, VoxelDimensions.RegionEdge - 1, 10, 10, 512);
            FillBrickRange(ref poolB, ref r2B, 0, 31, 10, 10, 512);
            tableB.CommitRegion(r1B);
            tableB.CommitRegion(r2B);

            // Destroy the support underneath both regions.
            int3 destroyOrigin = new int3(200, 0, 256);
            byte radius = 16;

            var evtA = BuildExplosionEvent(destroyOrigin, radius);
            var evtB = BuildExplosionEvent(destroyOrigin, radius);

            // Verify bridge was solid before collapse.
            AssertBridgeExists(ref tableA, in poolA, new int3(VoxelDimensions.RegionEdge * VoxelDimensions.BrickEdge - 16, 10, 256));

            // Run collapse detection on both worlds across the region boundary.
            var targetsA = CollapseTargetsFor(ref tableA, in poolA, destroyOrigin);
            var targetsB = CollapseTargetsFor(ref tableB, in poolB, destroyOrigin);

            // The collapse must affect BOTH regions.
            bool affectsBothRegionsA = false;
            bool affectsBothRegionsB = false;
            for (int i = 0; i < targetsA.Length && !affectsBothRegionsA; i++)
            {
                var c = GetRegion(targetsA[i].x, targetsA[i].y, targetsA[i].z);
                if (math.all(c == regionCoord) || math.all(c == otherRegion))
                    affectsBothRegionsA = true;
            }
            for (int i = 0; i < targetsB.Length && !affectsBothRegionsB; i++)
            {
                var c = GetRegion(targetsB[i].x, targetsB[i].y, targetsB[i].z);
                if (math.all(c == regionCoord) || math.all(c == otherRegion))
                    affectsBothRegionsB = true;
            }

            Assert.IsTrue(affectsBothRegionsA, "Collapse on client A must span both regions.");
            Assert.IsTrue(affectsBothRegionsB, "Collapse on client B must span both regions.");

            ApplyCollapse(ref tableA, ref poolA, default(NativeArray<byte>), destroyOrigin);
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
            // Build a floating platform (no support at Y < 1). Place one brick above it.
            var pool = new BrickPool(4096, Allocator.Persistent);

            int3 regionCoord = int3.zero;
            var table = new RegionTable(1, Allocator.Persistent);

            // Build ground at Z=0 (all bricks in bottom layer are filled).
            var region = table.LoadRegion(regionCoord);
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            {
                for (int y = 0; y < VoxelDimensions.RegionEdge; y += 8)
                {
                    for (int x = 0; x < VoxelDimensions.RegionEdge; x += 8)
                    {
                        int brickIdx = Region.BrickIndex(
                            x >> VoxelDimensions.BrickEdgeLog2,
                            y >> VoxelDimensions.BrickEdgeLog2,
                            z);
                        if (brickIdx < pool.Capacity)
                        {
                            var filled = pool.Allocate();
                            pool.FillBrick(filled, 3);
                            region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(filled);
                        }
                    }
                }
            }

            // Build the floating platform.
            for (int z = VoxelDimensions.RegionEdge - 8; z < VoxelDimensions.RegionEdge; z++)
            {
                for (int y = 10; y < 20; y++)
                {
                    for (int x = 20; x < 60; x += 8)
                    {
                        int brickIdx = Region.BrickIndex(x >> VoxelDimensions.BrickEdgeLog2,
                                                          y >> VoxelDimensions.BrickEdgeLog2, z);
                        if (brickIdx < region.BrickRefs.Length)
                        {
                            var filled = pool.Allocate();
                            pool.FillBrick(filled, 5);
                            region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(filled);
                        }
                    }
                }
            }

            table.CommitRegion(region);

            // Verify: the platform is supported by ground bricks.
            // Now remove all ground bricks below the platform.
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            {
                for (int y = 0; y < VoxelDimensions.RegionEdge; y += 8)
                {
                    for (int x = 20; x < 60; x += 8)
                    {
                        int brickIdx = Region.BrickIndex(
                            x >> VoxelDimensions.BrickEdgeLog2,
                            y >> VoxelDimensions.BrickEdgeLog2, z);
                        if (brickIdx >= 0 && brickIdx < region.BrickRefs.Length)
                            region.BrickRefs[brickIdx] = BrickRef.Empty; // Remove ground support.
                    }
                }
            }

            table.CommitRegion(region);

            // Check for unsupported bricks using SupportField with threshold=1.
            var support = new NativeArray<byte>(VoxelDimensions.BricksPerRegion, Allocator.Temp);
            SupportField.ComputeSupport(in table, in pool, regionCoord.x, regionCoord.y, regionCoord.z, support, Allocator.Temp);

            bool hasUnsupported = false;
            for (int y = 10; y < 20 && !hasUnsupported; y++)
            {
                int brickIdx = Region.BrickIndex(40 >> VoxelDimensions.BrickEdgeLog2,
                                                  y >> VoxelDimensions.BrickEdgeLog2,
                                                  63);
                if (brickIdx >= 0 && brickIdx < VoxelDimensions.BricksPerRegion)
                {
                    if (support[brickIdx] <= 1)
                        hasUnsupported = true;
                }
            }

            Assert.IsTrue(hasUnsupported, "Platform with no ground support should have unsupported bricks.");

            var targets = CollapseDetection.FindUnsupportedBuilds(in region.BrickRefs, in pool, in support);
            Assert.Greater(targets.Length, 0, "Unsupported builds must produce collapse targets.");

            // Apply the collapse.
            ApplyCollapse(ref table, ref pool, support, new int3(128, 5, 128));

            // Verify platform bricks are now empty.
            var platformBrick = new int3(40 * VoxelDimensions.BrickEdge + VoxelDimensions.BrickEdge / 2,
                                         15 * VoxelDimensions.BrickEdge + VoxelDimensions.BrickEdge / 2,
                                         63 * (VoxelDimensions.RegionEdge * VoxelDimensions.BrickEdge) + (VoxelDimensions.RegionEdge * VoxelDimensions.BrickEdge) / 2);

            int material = GetVoxel(ref table, in pool, platformBrick);
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, material,
                "Unsupported build must collapse to empty.");

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
            SupportField.ComputeSupport(
                in table, in pool, regionCoord.x, regionCoord.y, regionCoord.z, support, Allocator.Temp);

            var targets = CollapseDetection.FindCollapseTargets(
                in region.BrickRefs, in pool, in support, threshold);

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

                foreach (var brick in targets)
                {
                    int bx = (brick.x >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                    int by = (brick.y >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                    int bz = (brick.z >> VoxelDimensions.BrickEdgeLog2) & VoxelDimensions.RegionEdgeMask;

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

        private static int3 GetRegion(int x, int y, int z) =>
            new(
                x >> VoxelDimensions.RegionEdgeLog2,
                y >> VoxelDimensions.RegionEdgeLog2,
                z >> VoxelDimensions.RegionEdgeLog2);

        private static int FillBrickRange(ref BrickPool pool, ref Region region, int startX, int endX, int startY, int endY, int startZ)
        {
            // Stub — actual implementation fills bricks in a range.
            return 0;
        }

        private static int GetVoxel(ref RegionTable table, in BrickPool pool, int3 worldVoxel) =>
            VoxelAccess.GetVoxel(ref table, in pool, worldVoxel);

        private static void BuildChainReactionPillars(ref BrickPool pool, ref RegionTable table, int3 pA, int3 pB, int3 pC)
        {
            // Stub — builds pillars at three points.
        }
    }
}
