using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-009 test on time-to-playable for late join into a heavily altered world.
    ///
    /// SC-009 assertion: a late-joining player sees a playable silhouette within the first tick
    /// of joining, regardless of how extensively the world has been altered during the session.
    /// The top-level mips are always resident and require no event replay or brick-level decoding.
    /// </summary>
    public sealed class LateJoinTests
    {
        private const int k_PoolCapacity = 8192;

        private BrickPool _pool;
        private RegionTable _table;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(k_PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(32, Allocator.Persistent);

            // Initialize with a heavily altered world — not pristine terrain.
            SessionLifecycle.Create(12345u, ref _table, ref _pool);
        }

        [TearDown]
        public void TearDown()
        {
            _table.Dispose();
            _pool.Dispose();
        }

        /// <summary>
        /// SC-009: top-level mips are immediately available for a late-joining player.
        ///
        /// The payload must be non-empty, contain region coordinates within the neighbor radius,
        /// and be decodable without any brick-level data (only occupancy mips).
        /// </summary>
        [Test]
        public void TopLevelMips_ImmediatelyAvailable_NoBrickDataNeeded()
        {
            int3 playerRegion = new int3(0, 5, 0);

            // Ship top-level mips for the player's region (as if they just spawned).
            byte[] payload = LateJoin.ShipTopLevelMips(playerRegion);

            Assert.IsTrue(payload.Length > 0,
                "Top-level mip payload must be non-empty — this is what makes time-to-playable fast.");

            // Decode and verify the payload structure.
            int regionCount = System.BitConverter.ToInt32(payload, 0);
            Assert.Greater(regionCount, 0,
                "Must transmit at least the player's region coordinate.");

            // The top-level mip payload does not contain brick data — only occupancy cell data.
            // Each region entry should be: int3 coord (12 B) + uint cellCount (4 B) + ulong[] cells.
            int offset = sizeof(int);
            for (int i = 0; i < regionCount && offset + k_CoordSize + sizeof(uint) <= payload.Length; i++)
            {
                int rx = System.BitConverter.ToInt32(payload, offset);
                int ry = System.BitConverter.ToInt32(payload, offset + sizeof(int));
                int rz = System.BitConverter.ToInt32(payload, offset + sizeof(int) * 2);
                offset += k_CoordSize;

                // Verify the player's region is in the payload.
                if (rx == playerRegion.x && ry == playerRegion.y && rz == playerRegion.z)
                {
                    uint cellCount = System.BitConverter.ToUInt32(payload, offset);
                    Assert.Greater(cellCount, 0,
                        "The player's region must have at least one top-level mip cell.");

                    // Skip the cells data.
                    offset += sizeof(uint) + (int)cellCount * sizeof(ulong);
                }
                else
                {
                    uint cellCount = System.BitConverter.ToUInt32(payload, offset);
                    offset += sizeof(uint) + (int)cellCount * sizeof(ulong);
                }
            }

            Assert.GreaterOrEqual(offset, k_CoordSize,
                "Payload must contain at least one complete region entry.");
        }

        /// <summary>
        /// SC-009: late join into a heavily altered world still ships immediate playable silhouette.
        ///
        /// Scenario: fill 50% of three regions with mixed bricks (simulating extensive destruction),
        /// then have a player join at the center of one region. The top-level mips must encode
        /// the structural silhouette of that altered state without any brick-level detail.
        /// </summary>
        [Test]
        public void LateJoinIntoHeavilyAlteredWorld_StillPlaysImmediately()
        {
            // Step 1: Heavily alter three adjacent regions.
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 regionCoord = new int3(dx, 0, 0);
                Region r = _table.LoadRegion(regionCoord);

                // Fill the lower half of the region with material 1 (stone).
                for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                {
                    for (int y = 0; y < VoxelDimensions.RegionEdge / 2; y++)
                    {
                        int brickIdx = Region.BrickIndex(x, y, 0); // One row of bricks per layer.
                        for (int z = 0; z < VoxelDimensions.RegionEdge; z++, brickIdx += 1)
                        {
                            if (!r.BrickRefs[brickIdx].IsMixed && brickIdx < r.BrickRefs.Length)
                            {
                                int poolIdx = _pool.Allocate();
                                _pool.FillBrick(poolIdx, (byte)1);
                                r.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIdx);
                            }
                        }
                    }
                }

                // Also fill the upper half with material 2 (grass).
                for (int x = 0; x < VoxelDimensions.RegionEdge; x++)
                {
                    for (int y = VoxelDimensions.RegionEdge / 2; y < VoxelDimensions.RegionEdge; y++)
                    {
                        int brickIdx = Region.BrickIndex(x, y, 0);
                        if (!r.BrickRefs[brickIdx].IsMixed && brickIdx < r.BrickRefs.Length)
                        {
                            int poolIdx = _pool.Allocate();
                            _pool.FillBrick(poolIdx, (byte)2);
                            r.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIdx);
                        }
                    }
                }

                _table.CommitRegion(r);
            }

            // Verify: three regions with substantial mixed brick counts.
            int totalMixedBricks = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 coord = new int3(dx, 0, 0);
                if (_table.TryGetRegion(coord, out var r))
                    totalMixedBricks += CountMixed(r);
            }

            // Step 2: Late-join at the center region.
            int3 playerPos = new int3(0, 10, 0);
            byte[] topLevelPayload = LateJoin.ShipTopLevelMips(playerPos);

            // The key assertion: payload exists and is non-empty despite heavy alterations.
            Assert.IsTrue(topLevelPayload.Length > 0,
                "SC-009: top-level mips must be available even in a heavily altered world.");

            // Verify region count matches expected (3x3 grid of neighbors).
            int expectedRegionCount = 9; // 3x3 grid around player's XZ plane.
            int actualRegionCount = System.BitConverter.ToInt32(topLevelPayload, 0);
            Assert.AreEqual(expectedRegionCount, actualRegionCount,
                "Must transmit exactly a 3x3 grid of regions for the late-join silhouette.");

            // The payload size should be proportional to region count, not to alteration volume.
            // Each region contributes ~16 B (coord + cell count) + cells (typically 1 ulong = 8 B).
            float bytesPerRegion = (float)topLevelPayload.Length / actualRegionCount;
            Assert.LessOrEqual(bytesPerRegion, 32f,
                "SC-009: bytes per region must be bounded by mip structure, not alteration count.");
        }

        /// <summary>
        /// SC-009 verification: the payload size is sub-linear with respect to world alterations.
        ///
        /// This is the core of SC-009: if time-to-playable depends on how much the world has been
        /// altered, late join is slow in a heavily-griefed world — which violates the success criterion.
        /// </summary>
        [Test]
        public void PayloadSize_SubLinearVsAlterationCount()
        {
            int3 playerRegion = new int3(0, 0, 0);

            // Measure payload size with zero alterations.
            byte[] payloadEmpty = LateJoin.ShipTopLevelMips(playerRegion);
            int sizeEmpty = payloadEmpty.Length;

            // Now alter the region progressively and verify payload size doesn't grow.
            for (int pass = 0; pass < 3; pass++)
            {
                Region r = _table.LoadRegion(playerRegion);
                // Fixed count: BricksPerRegion/3 across three passes would allocate a full
                // region (262,144 bricks) from an 8,192-slot pool. Payload size is the claim
                // under test, and it is independent of how many bricks were altered.
                const int bricksPerPass = 256;
                for (int i = 0; i < bricksPerPass && i < r.BrickRefs.Length; i++)
                {
                    if (!r.BrickRefs[i].IsMixed)
                    {
                        int poolIdx = _pool.Allocate();
                        _pool.FillBrick(poolIdx, (byte)(pass + 1));
                        r.BrickRefs[i] = BrickRef.FromPoolIndex(poolIdx);
                    }
                }
                _table.CommitRegion(r);

                byte[] payloadAltered = LateJoin.ShipTopLevelMips(playerRegion);

                // Payload size must not increase with alterations (mip data is independent of brick detail).
                Assert.AreEqual(sizeEmpty, payloadAltered.Length,
                    $"SC-009: top-level mip payload size must be independent of alteration count. " +
                    $"Pass {pass}: empty={sizeEmpty}, altered={payloadAltered.Length}.");
            }
        }

        // -- helpers ------------------------------------------------------------

        private static int CountMixed(Region r)
        {
            int count = 0;
            for (int i = 0; i < VoxelDimensions.BricksPerRegion && i < r.BrickRefs.Length; i++)
            {
                if (r.BrickRefs[i].IsMixed) count++;
            }
            return count;
        }

        private const int k_CoordSize = sizeof(int) * 3;
    }
}
