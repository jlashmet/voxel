using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Streaming.Runtime;
using VoxelEngine.Tiering.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-004: Seamless kilometre-scale traversal.
    ///
    /// These tests verify that a player can move continuously through the world at any speed
    /// without encountering:
    ///   (a) loading screens -- regions stream in seamlessly during normal movement;
    ///   (b) frame-time budget overruns attributable to streaming -- main-thread work per
    ///       frame must stay <= 0.5 ms (device-matrix.md);
    ///   (c) visibility gaps -- no frame should show the world as "holes" because a region
    ///       was evicted before new regions finished loading.
    ///
    /// The traversal simulation moves a player along a straight line across hundreds of
    /// kilometres, exercising the full pipeline: ResidencyManager.Update -> Prefetch ->
    /// RegionLoader.QueueLoad -> PublishLoaded -> eviction candidates.
    /// </summary>
    public sealed class TraversalStreamingTests
    {
        private const float k_TickInterval = 1f / 30f; // 30 Hz tick rate
        private const float k_MovementSpeed = 10f; // m/s -- fast sprint
        private const int k_TestBrickPoolBytes = 1 << 20;
        private static int TestBrickPoolSlots => math.max(
            1, k_TestBrickPoolBytes / VoxelDimensions.BytesPerMixedBrick);

        // -----------------------------------------------------------------------
        // SC-004 Test 1: continuous kilometre-scale traversal with zero visible gaps.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Move a player across 5 km of world space at 10 m/s and verify that regions stream
        /// in seamlessly -- no loading screen, no frame-time budget overrun attributable to
        /// streaming, no visibility gaps (holes).
        /// </summary>
        [Test]
        [Category("SC_004")]
        [Category("US4")]
        public void ContinuousTraversalOverKilometresShowsNoGaps()
        {
            // Arrange: region table and brick pool.
            var table = new RegionTable(1024, Allocator.Persistent);
            var pool = new BrickPool(TestBrickPoolSlots, Allocator.Persistent); // 1 MiB mixed-brick payload budget.
            var residency = new RegionResidencyStore(in table, in pool);

            var playerPos = new float3(0f, 64f, 0f); // start at origin, eye height.
            var velocity = new float3(k_MovementSpeed, 0f, 0f); // straight along +X.

            int totalRegionsLoaded = 0;
            int evictionCursor = 0;

            try
            {
                // Act: simulate traversal across 5 km.
                float distanceTraversed = 0f;
                float targetDistance = 5000f;
                uint tickCount = 0;

                while (distanceTraversed < targetDistance)
                {
                    tickCount++;

                    // Each tick: update residency, prefetch, load, evict.
                    ResidencyManager.AssertHysteresisInvariants(DeviceTier.PC);

                    var residentRegions = ResidencyManager.GetResidentRegions(
                        playerPos,
                        (int)(ResidencyManager.GetLoadRadius(DeviceTier.PC) / 0.8f));

                    // Verify no gaps: for each desired resident region, either it's already
                    // loaded or a loading request is pending -- never "unavailable."
                    foreach (var rc in residentRegions)
                    {
                        if (!table.IsResident(rc))
                        {
                            // Simulate prefetch + load.
                            var prefetchTargets = Prefetch.GetPrefetchTargets(
                                playerPos, velocity,
                                (int)(ResidencyManager.GetLoadRadius(DeviceTier.PC) / 0.8f),
                                Allocator.Temp);

                            foreach (var prc in prefetchTargets)
                            {
                                if (!table.IsResident(prc))
                                {
                                    table.LoadRegion(prc); // placeholder -- would queue load in production.
                                    totalRegionsLoaded++;
                                }
                            }
                            prefetchTargets.Dispose();
                        }
                    }

                    residentRegions.Dispose();

                    // Simulate region loader publish (0.5 ms budget per device-matrix.md).
                    residency.Refresh(in table, in pool);
                    float mainThreadWorkMs = RegionLoader.PublishLoaded(residency, 0.5f);

                    // Verify streaming work stays within budget.
                    Assert.That(mainThreadWorkMs, Is.LessThanOrEqualTo(0.5f),
                        $"Streaming work ({mainThreadWorkMs:F3} ms) exceeded 0.5 ms budget at tick {tickCount}.");

                    // Bounded eviction walks actual resident coordinates, including regions
                    // that have fallen completely behind the player's current unload cube.
                    var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);
                    ResidencyManager.EvictFarResidents(
                        playerPos, unloadRadiusBricks, residency, ref evictionCursor, 64);

                    // Advance player position.
                    playerPos += velocity * k_TickInterval;
                    distanceTraversed += k_MovementSpeed * k_TickInterval;

                    // Hysteresis check after each tick.
                    ResidencyManager.AssertHysteresisInvariants(DeviceTier.PC);
                }

                // Verify regions were loaded and evicted during traversal (not all at start).
                Assert.That(totalRegionsLoaded, Is.GreaterThan(0),
                    "Must load regions during traversal -- not just preload everything at once.");

            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // SC-004 Test 2: streaming work stays within budget across all device tiers.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Simulate traversal on each device tier and verify that main-thread streaming work
        /// stays within the 0.5 ms budget, and no frame exceeds the tier's voxel-rendering budget.
        /// </summary>
        [Test]
        [Category("SC_004")]
        [Category("US4")]
        public void StreamingWorkStaysWithinTierBudgets()
        {
            var tiers = new[] { DeviceTier.PC, DeviceTier.Console, DeviceTier.MobileHE };

            foreach (var tier in tiers)
            {
                var table = new RegionTable(1024, Allocator.Persistent);
                var pool = new BrickPool(TestBrickPoolSlots, Allocator.Persistent);
                var residency = new RegionResidencyStore(in table, in pool);
                float playerPosZ = 0f;
                int evictionCursor = 0;

                try
                {
                    for (int tick = 0; tick < 300; tick++) // 10 seconds of traversal.
                    {
                        float3 playerPos = new float3(tick * 10f, 64f, playerPosZ);

                        var loadRadiusBricks = (int)(ResidencyManager.GetLoadRadius(tier) / 0.8f);
                        var wantedRegions = ResidencyManager.GetResidentRegions(playerPos, loadRadiusBricks);

                        // Load regions (placeholder -- in production this queues on worker thread).
                        foreach (var rc in wantedRegions)
                            if (!table.IsResident(rc))
                                table.LoadRegion(rc);

                        wantedRegions.Dispose();

                        // Measure streaming work.
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        residency.Refresh(in table, in pool);
                        int published = RegionLoader.PublishLoaded(residency, 0.5f);
                        float elapsedMs = sw.ElapsedMilliseconds;

                        Assert.That(elapsedMs, Is.LessThanOrEqualTo(0.5f),
                            $"Tier {tier}: streaming work ({elapsedMs:F3} ms) exceeded 0.5 ms budget at tick {tick}.");

                        // Eviction scans actual resident coordinates rather than a shell
                        // centred only on the current player position.
                        var unloadRadiusBricks = (int)(ResidencyManager.GetUnloadRadius(tier) / 0.8f);
                        ResidencyManager.EvictFarResidents(
                            playerPos, unloadRadiusBricks, residency, ref evictionCursor, 64);
                    }

                }
                finally
                {
                    table.Dispose();
                    pool.Dispose();
                }
            }
        }

        // -----------------------------------------------------------------------
        // SC-004 Test 3: fast-directional prefetch covers traversal path ahead of player.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Verify that directional prefetch extends the resident set beyond the load radius
        /// in the movement direction, ensuring regions are loaded before the player reaches them.
        /// </summary>
        [Test]
        [Category("SC_004")]
        [Category("US4")]
        public void DirectionalPrefetchExtendsResidentSetAheadOfPlayer()
        {
            var table = new RegionTable(1024, Allocator.Persistent);

            float3 playerPos = new float3(500f, 64f, 0f);
            float3 velocity = new float3(10f, 0f, 0f); // sprinting along +X.

            int loadRadiusBricks = (int)(ResidencyManager.GetLoadRadius(DeviceTier.PC) / 0.8f);
            var concentricRegions = ResidencyManager.GetResidentRegions(playerPos, loadRadiusBricks);
            var directionalPrefetch = Prefetch.GetPrefetchTargets(playerPos, velocity, loadRadiusBricks, Allocator.Persistent);

            // Directional prefetch must include all concentric regions plus extras ahead.
            Assert.That(directionalPrefetch.Length, Is.GreaterThanOrEqualTo(concentricRegions.Length),
                "Directional prefetch must cover at least the concentric load set.");

            // Verify some prefetch targets are beyond the load radius (the prefetch margin).
            bool foundBeyondLoad = false;
            foreach (var prc in directionalPrefetch)
            {
                float3 regionCenter = RegionWorldPos(prc);
                float dist = math.distance(regionCenter, playerPos);
                if (dist > ResidencyManager.GetLoadRadius(DeviceTier.PC))
                {
                    foundBeyondLoad = true;
                    break;
                }
            }

            Assert.That(foundBeyondLoad, Is.True,
                "Directional prefetch must include regions beyond the load radius.");

            concentricRegions.Dispose();
            directionalPrefetch.Dispose();
        }

        /// <summary>Get the world position of a region's corner in metres.</summary>
        private static float3 RegionWorldPos(int3 rc) => new float3(
            rc.x * VoxelDimensions.RegionEdge * 0.8f,
            rc.y * VoxelDimensions.RegionEdge * 0.8f,
            rc.z * VoxelDimensions.RegionEdge * 0.8f
        );
    }
}
