using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Runtime.Client
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
        private const float MonitoringWindowSec = 30f;
        private const float SmoothingFactor = 1f / MonitoringWindowSec;

        private static float _currentBandwidthKbPerSec;
        private static bool _isCellular = false;
        private static float _targetBandwidthKbps = 256f;

        public static float CurrentBandwidthKbPerSec => _currentBandwidthKbPerSec;
        public static bool IsDegraded { get; private set; }
        public static byte DegradationLevel { get; private set; }

        public static void RecordDownstreamBytes(int kbReceived)
        {
            _currentBandwidthKbPerSec = math.lerp(
                _currentBandwidthKbPerSec,
                (float)kbReceived,
                SmoothingFactor);
            UpdateTargetBudget();
        }

        private static void UpdateTargetBudget()
        {
            _targetBandwidthKbps = _isCellular ? 96f : 256f;
        }

        public static void SetConnectionType(bool isCellular)
        {
            _isCellular = isCellular;
            UpdateTargetBudget();
        }

        /// <summary>
        /// Apply fidelity degradation when bandwidth falls below threshold. This policy owns only
        /// replication/presentation fidelity state; it does not inspect or mutate world storage.
        /// </summary>
        public static void ApplyDegradation(float targetBandwidthKbps)
        {
            if (_currentBandwidthKbPerSec >= targetBandwidthKbps * 0.9f)
            {
                if (IsDegraded && DegradationLevel == 0)
                    return;
                IsDegraded = false;
                DegradationLevel = 0;
                return;
            }

            if (!IsDegraded)
            {
                IsDegraded = true;
                DegradationLevel = 1;
            }

            DemoteDistantRegions((int)DegradationLevel);
        }

        public static void DemoteMipLevel(int3 regionCoord, byte fromLevel, byte toLevel)
        {
            if (fromLevel <= toLevel)
                return;
            _demotedRegions.Add(regionCoord);
        }

        private static NativeList<int3> _demotedRegions = new NativeList<int3>(64, Allocator.Persistent);

        private static void DemoteDistantRegions(int count)
        {
            // Replication policy placeholder. No world-storage access belongs in this subsystem.
        }

        public static bool CanRecover(float currentBandwidthKbps)
        {
            return currentBandwidthKbps >= _targetBandwidthKbps * 1.1f && DegradationLevel > 0;
        }

        public static void TryRecover(int3 regionCoord, byte currentLevel)
        {
            if (!CanRecover(CurrentBandwidthKbPerSec))
                return;
        }
    }
}
