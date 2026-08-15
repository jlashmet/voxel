using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Tracks asynchronous region-load completion and publishes residency within a bounded
    /// main-thread budget.
    ///
    /// Terrain generation data is deliberately not represented here as Storage brick handles.
    /// Streaming owns demand and completion timing; Terrain owns generation; Storage owns the
    /// resident world and its physical allocation. The current worker is still a placeholder for
    /// the Terrain handoff, so a completion carries only logical region identity and target mip.
    /// </summary>
    public static class RegionLoader
    {
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

        private struct CompletedRegion
        {
            public int3 RegionCoord;
            public byte MipLevel;
        }

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
                return;

            _queue[tail % _queue.Length] = new LoadRequest(regionCoord, terrainSeed, requestedMipLevel);
        }

        /// <summary>
        /// Placeholder worker until Terrain generation is wired through its own subsystem API.
        /// A worker completion means only that Streaming may now request region residency.
        /// </summary>
        private static void WorkerLoop()
        {
            while (_running)
            {
                int head = _queueHead;
                if (head >= _queueTail)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (Interlocked.CompareExchange(ref _queueHead, head + 1, head) != head)
                    continue;

                LoadRequest request = _queue[head % _queue.Length];
                _ = request.TerrainSeed;
                PushCompletion(new CompletedRegion
                {
                    RegionCoord = request.RegionCoord,
                    MipLevel = request.RequestedMipLevel,
                });
            }
        }

        private static CompletedRegion[] _completions = new CompletedRegion[64];
        private static int _completionCount;

        private static void PushCompletion(CompletedRegion completion)
        {
            // Interlocked.Increment returns the new count; array slots are zero-based.
            int index = Interlocked.Increment(ref _completionCount) - 1;
            if (index >= _completions.Length)
            {
                Interlocked.Decrement(ref _completionCount);
                return;
            }
            _completions[index] = completion;
        }

        private static bool _running = true;

        /// <summary>
        /// Publishes completed load requests into Storage within the main-thread budget.
        /// Returns the number of regions made resident this frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PublishLoaded(IRegionResidencyStore storage, float mainThreadBudgetMs)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (_completionCount == 0) return 0;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int published = 0;
            while (_completionCount > 0)
            {
                if (stopwatch.ElapsedMilliseconds >= mainThreadBudgetMs)
                    break;

                int index = Interlocked.Decrement(ref _completionCount);
                if (index < 0)
                {
                    Interlocked.Exchange(ref _completionCount, 0);
                    break;
                }

                CompletedRegion completion = _completions[index % _completions.Length];
                storage.EnsureRegionResident(completion.RegionCoord);
                published++;
            }

            return published;
        }

        /// <summary>
        /// Provide mip-level occupancy approximation for a region that has not finished loading.
        /// The renderer uses this to show a low-resolution silhouette before full detail arrives.
        /// </summary>
        public static void ProvideMipApproximation(int3 regionCoord, in NativeArray<ulong> mipData)
        {
            // Production: write to a Storage/Terrain-owned approximation cache through its API.
        }

        /// <summary>Retrieve the mip approximation for a region, if available.</summary>
        public static bool TryGetMipApproximation(int3 regionCoord, out NativeArray<ulong> mipData)
        {
            mipData = default;
            return false;
        }
    }
}
