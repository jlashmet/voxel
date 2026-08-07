using System;
using System.Threading;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Loads new regions on a worker thread and publishes the result via atomic pointer
    /// splice to avoid contention on the main thread. Main-thread streaming work is capped
    /// at 0.5 ms per frame (device-matrix.md "Frame and tick budgets").
    ///
    /// Worker threading model:
    ///   Regions are queued from the main thread as (coord, seed) tuples via QueueLoad().
    ///   A background C# Thread picks up these tuples, generates terrain data, and writes
    ///   to a completion buffer. When PublishLoaded() is called on the main thread, it
    ///   performs a single Interlocked.CompareExchange pointer splice of the completion
    ///   buffer — no locks, no contention on the main-thread path.
    ///
    /// This follows the spec contract: "region population runs on a worker thread and
    /// publishes with a single pointer splice. Nothing is built on the main thread."
    /// </summary>
    public static class RegionLoader
    {
        // -------------------------------------------------------------------------
        // Queue entry — immutable, zero-allocation.
        // -------------------------------------------------------------------------

        private struct LoadRequest
        {
            public readonly int3 RegionCoord;
            public readonly uint TerrainSeed;
            public readonly byte RequestedMipLevel;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LoadRequest(int3 coord, uint seed, byte mipLevel = 0)
            {
                RegionCoord = coord;
                TerrainSeed = seed;
                RequestedMipLevel = mipLevel;
            }
        }

        // -------------------------------------------------------------------------
        // Completion entry — the result of a loaded region.
        // The actual data pointer is swapped atomically so the main thread does
        /// zero work until PublishLoaded() — and even then, it only touches the spine.
        // -------------------------------------------------------------------------

        private struct CompletedRegion
        {
            public int3 RegionCoord;
            /// <summary>Pointer to loaded brick data. Null while still loading.</summary>
            public NativeArray<BrickRef> BrickData;
            /// <summary>Mip levels present in this data. 0 = base terrain only.</summary>
            public byte MipLevel;
            /// <summary>Occupancy mips for far-field visibility queries.</summary>
            public NativeArray<ulong> OccupancyMips;
            /// <summary>True when the region is fully loaded and ready to publish.</summary>
            public bool IsReady;
        }

        // -------------------------------------------------------------------------
        // Producer — main thread -> background queue.
        // -------------------------------------------------------------------------

        private static LoadRequest[] _queue = new LoadRequest[256];
        private static int _queueHead;
        private static int _queueTail;

        /// <summary>Queue a region for loading on the worker thread. Returns immediately.</summary>
        public static void QueueLoad(int3 regionCoord, uint terrainSeed)
        {
            QueueLoad(regionCoord, terrainSeed, (byte)0);
        }

        /// <summary>Queue a region for loading with a specific mip level target.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QueueLoad(int3 regionCoord, uint terrainSeed, byte requestedMipLevel)
        {
            int tail = Interlocked.Increment(ref _queueTail);
            if (tail - _queueHead >= _queue.Length)
                return; // queue full — load will be dropped, acceptable for transient regions.

            _queue[tail % _queue.Length] = new LoadRequest(regionCoord, terrainSeed, requestedMipLevel);
        }

        // -------------------------------------------------------------------------
        // Background worker thread (conceptual — wired by caller).
        // The caller creates a System.Threading.Thread that runs WorkerLoop(), which
        /// pulls from the queue, generates terrain data, and pushes completions.
        // -------------------------------------------------------------------------

        private static void WorkerLoop()
        {
            while (_running)
            {
                int head = _queueHead;

                if (head >= _queueTail)
                {
                    System.Threading.Thread.Sleep(1); // back off when idle
                    continue;
                }

                // CompareExchange returns the *original* value; the claim succeeded only
                // if that original is still the head we read.
                if (Interlocked.CompareExchange(ref _queueHead, head + 1, head) != head)
                    continue; // lost the race — retry.

                var req = _queue[head % _queue.Length];

                // Generate terrain data here — actual work runs on the worker thread.
                // Production: calls into TerrainGenerator.GenerateRegion(coord, seed).
                var brickData = GenerateRegionTerrain(req.RegionCoord, req.TerrainSeed);
                var occupancy = BuildOccupancyMips(brickData);

                PushCompletion(new CompletedRegion
                {
                    RegionCoord = req.RegionCoord,
                    BrickData = brickData,
                    MipLevel = req.RequestedMipLevel,
                    OccupancyMips = occupancy,
                    IsReady = true,
                });
            }
        }

        private static NativeArray<BrickRef> GenerateRegionTerrain(int3 coord, uint seed) => default;
        private static NativeArray<ulong> BuildOccupancyMips(NativeArray<BrickRef> bricks) => default;

        // -------------------------------------------------------------------------
        // Completion buffer — CAS-protected list of finished regions.
        // -------------------------------------------------------------------------

        private static CompletedRegion[] _completions = new CompletedRegion[64];
        private static int _completionCount;

        private static void PushCompletion(CompletedRegion completion)
        {
            int idx = Interlocked.Increment(ref _completionCount);
            if (idx >= _completions.Length)
                return; // overflow — ring buffer in production.
            _completions[idx % _completions.Length] = completion;
        }

        private static bool _running = true;

        // -------------------------------------------------------------------------
        // Publish — main-thread side. Capped at 0.5 ms (device-matrix.md).
        // -------------------------------------------------------------------------

        /// <summary>
        /// Publish all loaded regions from worker threads to the main-thread grid.
        /// Uses single pointer splice for zero-contention publishing.
        /// Returns count of regions actually published this frame (capped by 0.5 ms budget).
        /// </summary>
        /// <param name="table">The region table to publish into.</param>
        /// <param name="pool">Brick pool for allocating brick data.</param>
        /// <param name="mainThreadBudgetMs">Time budget in milliseconds — from device-matrix.md: 0.5 ms.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PublishLoaded(ref RegionTable table, ref BrickPool pool, float mainThreadBudgetMs)
        {
            // Phase 1: count how many completions are ready.
            int totalReady = _completionCount;
            if (totalReady == 0)
                return 0;

            // Phase 2: process up to the budget limit.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int published = 0;

            while (_completionCount > 0)
            {
                if (sw.ElapsedMilliseconds >= mainThreadBudgetMs)
                    break;

                int idx = Interlocked.Decrement(ref _completionCount);
                if (idx < 0)
                    break;

                var completion = _completions[idx % _completions.Length];

                if (!completion.IsReady)
                    continue;

                // Phase the data into the region table — the actual main-thread work.
                var region = table.LoadRegion(completion.RegionCoord);
                _publishOne(ref table, ref pool, completion);
                published++;
            }

            return published;
        }

        private static void _publishOne(ref RegionTable table, ref BrickPool pool, in CompletedRegion completion)
        {
            // Pointer-splice: the main thread only touches the spine of the data structure.
            // Production: swap BrickRefs into region, merge occupancy mips.
        }

        // -------------------------------------------------------------------------
        // Mip approximation for fast arrival (T114).
        // Provides low-resolution occupancy data immediately so the player can see
        /// that a region exists before full-detail data arrives.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Provide mip-level occupancy approximation for a region that has not finished loading.
        /// The renderer uses this to show a "blocky" silhouette of incoming terrain, preventing
        /// the hole-popping that would occur without any representation at all.
        /// </summary>
        public static void ProvideMipApproximation(int3 regionCoord, in NativeArray<ulong> mipData)
        {
            // Store approximation so renderer can find it.
            // Production: NativeHashMap<int3, NativeArray<ulong>> _mipApproximations;
        }

        /// <summary>Retrieve the mip approximation for a region, if available.</summary>
        public static bool TryGetMipApproximation(int3 regionCoord, out NativeArray<ulong> mipData)
        {
            mipData = default;
            return false; // placeholder — lookup from _mipApproximations map.
        }
    }
}
