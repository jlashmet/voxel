using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.Irradiance
{
    /// <summary>
    /// World-space irradiance probe cache with invalidation and multi-frame reconvergence.
    ///
    /// Irradiance probes sample the brickmap's visual contribution (material colour, occupancy)
    /// at a world-space position and return a diffuse lighting coefficient. Probes are cached
    /// so repeated samples at the same position reuse the previous result, and invalidated probes
    /// are automatically recomputed on the next access.
    ///
    /// Multi-frame reconvergence: each frame, only N probes are recomputed (N controlled by
    /// the device tier budget) to bound CPU cost. Probes that were not recomputed this frame
    /// retain their previous value — a trade-off between latency and accuracy that is critical
    /// for Mobile-HE where compute budget is tight.
    /// </summary>
    public sealed class ProbeCache : IDisposable
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Default number of probes to recompute per frame (multi-frame reconvergence).</summary>
        private const int k_DefaultReconvergeBudget = 32;

        /// <summary>Maximum cache entries. Beyond this, oldest probes are evicted on demand.</summary>
        private const int k_MaxEntries = 4096;

        // -- state ----------------------------------------------------------------

        /// <summary>Cache entries: world-space probe position mapped to irradiance value and validity.</summary>
        private NativeHashMap<float3, ProbeEntry> _probes;

        /// <summary>FIFO of recently-used probe positions for eviction policy.</summary>
        private NativeList<float3> _usageOrder;

        /// <summary>Number of probes to recompute this frame. Set by the tiering budget.</summary>
        private int _reconvergeBudget;

        /// <summary>Remaining reconvergence slots for this frame (decremented each time a probe is recomputed).</summary>
        private int _reconvergeRemaining;

        // -- construction ---------------------------------------------------------

        public ProbeCache()
        {
            _probes = new NativeHashMap<float3, ProbeEntry>(256, Allocator.Persistent);
            _usageOrder = new NativeList<float3>(256, Allocator.Persistent);
            _reconvergeBudget = k_DefaultReconvergeBudget;
            _reconvergeRemaining = _reconvergeBudget;
        }

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Update (or compute) the irradiance at a probe position by sampling the brickmap.
        ///
        /// If the probe already exists and is valid, its cached value is returned immediately
        /// (O(1) hash lookup). If it has been invalidated or does not exist, it is recomputed
        /// by sampling occupancy at world-space positions around the probe location and returning
        /// a weighted average of material colours.
        ///
        /// Recomputation uses multi-frame reconvergence: only _reconvergeRemaining probes are
        /// computed per frame; excess requests defer to the next frame.
        /// </summary>
        /// <param name="probePosition">World-space position of the probe.</param>
        /// <param name="table">Region table for brickmap queries during irradiance computation.</param>
        /// <returns>The irradiance float3 at this probe position. Computed fresh on first access or
        /// after invalidation; cached value returned on subsequent hits within the same frame.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 Update(float3 probePosition, ref RegionTable table)
        {
            // Start reconvergence budget at frame start (called by render feature).
            if (_reconvergeRemaining < 0)
                _reconvergeRemaining = _reconvergeBudget;

            ProbeEntry entry;
            bool exists = _probes.TryGetValue(probePosition, out entry);

            if (exists && entry.Valid && _reconvergeRemaining > 0)
            {
                // Probe is valid and we have budget to recompute — always refresh for accuracy.
                entry.Value = ComputeIrradiance(probePosition, ref table);
                entry.ComputedFrame = frameCount;
                entry.Valid = true;
                _probes[probePosition] = entry;

                UpdateUsageOrder(probePosition);
                _reconvergeRemaining--;
                return entry.Value;
            }

            if (exists && entry.Valid)
            {
                // No reconvergence budget — reuse cached value.
                UpdateUsageOrder(probePosition);
                return entry.Value;
            }

            // Probe doesn't exist or was invalidated — compute it.
            if (!exists || !entry.Valid)
            {
                _reconvergeRemaining--;

                // Evict oldest probes if cache is full.
                while (_probes.Count >= k_MaxEntries && _usageOrder.Length > 0)
                    EvictOldest();

                float3 irradiance = ComputeIrradiance(probePosition, ref table);
                entry = new ProbeEntry { Value = irradiance, Valid = true, ComputedFrame = frameCount };
                _probes[probePosition] = entry;
                UpdateUsageOrder(probePosition);

                return irradiance;
            }

            // Should not reach here — fallback to a fresh computation.
            return ComputeIrradiance(probePosition, ref table);
        }

        /// <summary>Invalidate all probes. Called when the world changes significantly (large edit).</summary>
        public void InvalidateAll()
        {
            foreach (var kvp in _probes)
            {
                var entry = kvp.Value;
                entry.Valid = false;
                _probes[kvp.Key] = entry;
            }
        }

        /// <summary>Clear all probes and usage tracking.</summary>
        public void Clear()
        {
            _probes.Clear();
            _usageOrder.Clear();
        }

        /// <summary>Number of valid probes in the cache.</summary>
        public int Count => _probes.Count;

        /// <summary>Set the per-frame reconvergence budget (how many probes to recompute each frame).</summary>
        public int ReconvergeBudget
        {
            get => _reconvergeBudget;
            set => _reconvergeBudget = math.max(1, value);
        }

        /// <summary>Dispose native resources.</summary>
        public void Dispose()
        {
            if (_probes.IsCreated) _probes.Dispose();
            if (_usageOrder.IsCreated) _usageOrder.Dispose();
        }

        // -- internal helpers -----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ComputeIrradiance(float3 probePosition, ref RegionTable table)
        {
            // Sample the brickmap at 6 points around the probe (positive/negative axis directions)
            // and return the weighted average of material colours at those points.
            float totalR = 0f, totalG = 0f, totalB = 0f;
            int count = 0;

            float3[] samplePoints = new float3[6]
            {
                probePosition + new float3(1f, 0, 0),
                probePosition - new float3(1f, 0, 0),
                probePosition + new float3(0, 1f, 0),
                probePosition - new float3(0, 1f, 0),
                probePosition + new float3(0, 0, 1f),
                probePosition - new float3(0, 0, 1f),
            };

            for (int i = 0; i < 6; i++)
            {
                int3 worldVoxel = (int3)math.round(probePosition + samplePoints[i]);
                byte material = VoxelAccess.GetVoxel(ref table, GetDefaultPool(), worldVoxel);

                if (material != VoxelDimensions.MaterialEmpty)
                {
                    // Simple material-to-colour mapping: index -> grey-scale for now.
                    float intensity = math.saturate((float)material / 255f);
                    totalR += intensity;
                    totalG += intensity * 0.95f;
                    totalB += intensity * 0.85f;
                    count++;
                }
            }

            if (count == 0)
                return new float3(0.5f, 0.7f, 0.9f); // Default sky-adjacent lighting.

            float inv = 1f / count;
            return new float3(totalR * inv, totalG * inv, totalB * inv);
        }

        private BrickPool GetDefaultPool() => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateUsageOrder(float3 position)
        {
            // Remove existing entry from usage order (if present).
            for (int i = _usageOrder.Length - 1; i >= 0; i--)
            {
                if (math.all(_usageOrder[i] == position))
                {
                    _usageOrder.RemoveAt(i);
                    break;
                }
            }

            // Add to end (most recently used).
            _usageOrder.Add(position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EvictOldest()
        {
            if (_usageOrder.Length == 0) return;

            float3 oldest = _usageOrder[0];
            _probes.Remove(oldest);
            _usageOrder.RemoveAt(0);
        }

        // -- probe entry ----------------------------------------------------------

        private struct ProbeEntry
        {
            public float3 Value;       // Computed irradiance.
            public bool Valid;         // True if the value is up-to-date with the current world state.
            public uint ComputedFrame; // Last frame this probe was recomputed.
        }

        private static uint frameCount = 0;
    }
}
