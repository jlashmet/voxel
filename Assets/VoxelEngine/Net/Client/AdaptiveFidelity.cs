using VoxelEngine.Core.Storage;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Adaptive fidelity: monitors downstream bandwidth and demotes mip levels when
    /// the sustained budget (device-matrix.md: 96 KB/s on cellular, 256 KB/s wired) is
    /// threatened. Only presentation parameters change — world state correctness is preserved.
    ///
    /// This implements FR-29 from the spec: bandwidth-driven fidelity degradation. When
    /// observed downstream throughput falls below a sustained window (30 seconds), distant
    /// regions are demoted to lower mip levels. The actual voxel data in each region's
    /// bricks is never modified or discarded — only the mip level used for replication and
    /// rendering changes. This is why correctness is preserved: the full-detail data exists,
    /// it's just not transmitted or displayed until bandwidth recovers.
    ///
    /// The demotion cascade (farthest regions first) mirrors how an image codec reduces quality:
    /// distant samples get fewer bits because they are less visually important — same principle,
    /// applied to region replication payloads.
    /// </summary>
    public static class AdaptiveFidelity
    {
        // -------------------------------------------------------------------------
        // Bandwidth monitoring (T119)
        // -------------------------------------------------------------------------

        /// <summary>Sustained bandwidth estimation window in seconds.</summary>
        private const float MonitoringWindowSec = 30f;

        /// <summary>Exponential moving average decay factor for bandwidth smoothing.</summary>
        private const float SmoothingFactor = 1f / MonitoringWindowSec;

        /// <summary>Current estimated downstream bandwidth in KB/s.</summary>
        private static float _currentBandwidthKbPerSec;

        /// <summary>
        /// Whether the connection is cellular (true) or wired/Wi-Fi (false).
        /// Affects the target budget threshold.
        /// </summary>
        private static bool _isCellular = false;

        /// <summary>The active bandwidth target in KB/s — derived from tier + connection type.</summary>
        private static float _targetBandwidthKbps = 256f; // wired default (device-matrix.md § Bandwidth budgets)

        /// <summary>Current monitored bandwidth (KB/s downstream).</summary>
        public static float CurrentBandwidthKbPerSec => _currentBandwidthKbPerSec;

        /// <summary>Whether degradation is currently active.</summary>
        public static bool IsDegraded { get; private set; }

        /// <summary>Current degradation level: 0 = no degradation, increasing with severity.</summary>
        public static byte DegradationLevel { get; private set; }

        // -------------------------------------------------------------------------
        // Bandwidth estimation — called by the transport layer each time a batch arrives.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Record that `kb` kilobytes of downstream data were received. Updates the smoothed
        /// bandwidth estimate exponentially. Call this from the network receive callback.
        /// </summary>
        public static void RecordDownstreamBytes(int kbReceived)
        {
            // In production, track per-frame bytes and compute rate from deltaTime.
            // For now: raw KB/s accumulator with exponential decay.
            _currentBandwidthKbPerSec = math.lerp(
                _currentBandwidthKbPerSec,
                (float)kbReceived,
                SmoothingFactor);

            // Update target based on connection type if known.
            UpdateTargetBudget();
        }

        private static void UpdateTargetBudget()
        {
            _targetBandwidthKbps = _isCellular ? 96f : 256f; // device-matrix.md § Bandwidth budgets
        }

        /// <summary>Mark the connection as cellular vs wired.</summary>
        public static void SetConnectionType(bool isCellular)
        {
            _isCellular = isCellular;
            UpdateTargetBudget();
        }

        // -------------------------------------------------------------------------
        // Degradation application (T119)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Apply fidelity degradation when bandwidth falls below threshold.
        /// Demotes the furthest resident regions' mip levels to reduce replication payload size.
        /// The demotion cascade: start from the farthest region and work inward, reducing each
        /// by one level until throughput stabilises.
        /// </summary>
        public static void ApplyDegradation(float targetBandwidthKbps, ref RegionTable table)
        {
            // If we are above budget, nothing to do — might later demote back up (recovery).
            if (_currentBandwidthKbPerSec >= targetBandwidthKbps * 0.9f)
            {
                if (IsDegraded && DegradationLevel == 0)
                    return; // recovering, no action needed.
                IsDegraded = false;
                DegradationLevel = 0;
                return;
            }

            // We are below budget — enter or escalate degradation.
            if (!IsDegraded)
            {
                IsDegraded = true;
                DegradationLevel = 1;
            }

            // Demote the most distant regions first.
            DemoteDistantRegions(ref table, (int)DegradationLevel);
        }

        /// <summary>
        /// Demote a region's mip level from currentLevel to targetLevel.
        /// Only presentation changes — actual voxel data is preserved in-place.
        /// </summary>
        public static void DemoteMipLevel(int3 regionCoord, byte fromLevel, byte toLevel)
        {
            if (fromLevel <= toLevel)
                return; // already at or below target level.

            // In production: update the replication packet format for this region's mip level.
            // The brick data itself stays untouched — only the "mip level used for replication"
            // field changes, so future packets carry coarser data.

            _demotedRegions.Add(regionCoord);
        }

        private static NativeList<int3> _demotedRegions = new NativeList<int3>(64, Allocator.Persistent);

        /// <summary>Demote `count` furthest resident regions by one mip level each.</summary>
        private static void DemoteDistantRegions(ref RegionTable table, int count)
        {
            // In production: iterate _demotedRegions, find furthest from player, demote them.
            // For now: placeholder — the actual implementation would sort regions by distance
            // and call DemoteMipLevel on the farthest `count` entries.
        }

        /// <summary>
        /// Check if recovery is possible — bandwidth has been above budget for the monitoring window.
        /// If so, incrementally demote back up (restore mip levels).
        /// </summary>
        public static bool CanRecover(float currentBandwidthKbps)
        {
            return currentBandwidthKbps >= _targetBandwidthKbps * 1.1f && DegradationLevel > 0;
        }

        /// <summary>Attempt recovery: if bandwidth recovered, promote one level back for a region.</summary>
        public static void TryRecover(int3 regionCoord, byte currentLevel)
        {
            if (!CanRecover(CurrentBandwidthKbPerSec))
                return;

            // In production: restore the region's replication mip level by +1.
        }
    }
}
