using System.Runtime.CompilerServices;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Configures Unity Transport quality-of-service pipelines for the custom replication stack.
    ///
    /// Channel contract:
    ///   EVENT     — reliable ordered durable authoritative events/confirmations.
    ///   EPHEMERAL — unreliable sequenced input/motion; newer samples supersede older ones.
    ///   REPAIR    — reliable authoritative state correction after detected drift.
    ///   BULK      — reliable fragmented region/snapshot transfer, rate-limited so it cannot
    ///               starve latency-sensitive traffic.
    ///
    /// Unity Transport has no pipeline priority/bandwidth scheduler, so BULK reservation remains
    /// an application-side policy (BulkThrottle). Pipeline construction only establishes delivery
    /// and ordering semantics.
    /// </summary>
    public struct ChannelSetup
    {
        public NetworkPipeline Event;
        public NetworkPipeline Ephemeral;
        public NetworkPipeline Repair;
        public NetworkPipeline Bulk;

        /// <summary>Minimum latency-sensitive share on wired/Wi-Fi.</summary>
        public const float k_EventShareWired = 0.60f;

        /// <summary>Minimum latency-sensitive share on constrained mobile cellular.</summary>
        public const float k_EventShareMobile = 0.70f;

        public const float k_RepairShare = 0.20f;
        public const float k_BulkShare = 0.20f;

        public const uint k_SustainedDownstreamWiredKb = 256;
        public const uint k_SustainedDownstreamMobileKb = 96;

        /// <summary>Conservative non-fragmented durable EVENT ceiling.</summary>
        public const int k_MaxEventPacketBytes = 1200;

        /// <summary>
        /// Ephemeral command ceiling. Current C_PlayerInput frame is only 18 bytes including the
        /// protocol envelope; the extra room permits a small redundant-history bundle later.
        /// </summary>
        public const int k_MaxEphemeralPacketBytes = 256;

        public const int k_MaxRepairPacketBytes = 1024;
        public const int k_MaxBulkPacketBytes = 16384;

        /// <summary>
        /// Create every pipeline before any connection is established. Both peers call this method
        /// in the same order so their pipeline IDs and stage layouts agree.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChannelSetup Create(ref NetworkDriver driver)
        {
            return new ChannelSetup
            {
                Event = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)),
                Ephemeral = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage)),
                Repair = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)),
                Bulk = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(ReliableSequencedPipelineStage)),
            };
        }

        public static NetworkSettings DefaultSettings()
        {
            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(payloadCapacity: k_MaxBulkPacketBytes);
            return settings;
        }

        /// <summary>
        /// Compute the durable/repair/bulk reservation. EPHEMERAL is tiny and latency-sensitive;
        /// it consumes the same reserved headroom as EVENT rather than borrowing from BULK.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (uint eventKb, uint repairKb, uint bulkKb) ComputeBudgets(
            uint totalCapacityKbPerSecond, bool isMobileCellular)
        {
            float eventShare = isMobileCellular ? k_EventShareMobile : k_EventShareWired;

            uint eventBudget = (uint)(totalCapacityKbPerSecond * eventShare);
            uint remaining = totalCapacityKbPerSecond - eventBudget;
            uint repairBudget = (uint)(remaining * k_RepairShare / (k_RepairShare + k_BulkShare));
            uint bulkBudget = remaining - repairBudget;

            return (eventBudget, repairBudget, bulkBudget);
        }
    }
}
