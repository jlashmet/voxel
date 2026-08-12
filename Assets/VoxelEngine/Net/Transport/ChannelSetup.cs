using System.Runtime.CompilerServices;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Configures Unity Transport with three quality-of-service pipelines tuned to the
    /// bandwidth budgets in device-matrix.md and contracts/wire-protocol.md.
    ///
    /// Channel contract:
    ///   EVENT  — reliable ordered live authoritative events.
    ///   REPAIR — reliable state correction after detected drift.
    ///   BULK   — reliable fragmented region/snapshot transfer, rate-limited so it cannot
    ///            starve EVENT.
    ///
    /// Unity Transport has no notion of pipeline priority or per-pipeline bandwidth caps,
    /// so the reservation policy is enforced on our side by BulkThrottle rather than by
    /// the driver. What this type owns is pipeline construction and sizing.
    /// </summary>
    public struct ChannelSetup
    {
        // -- pipelines ------------------------------------------------------------

        /// <summary>Reliable sequenced delivery for durable authoritative events.</summary>
        public NetworkPipeline Event;

        /// <summary>Reliable sequenced delivery for authoritative state repair.</summary>
        public NetworkPipeline Repair;

        /// <summary>
        /// Fragmented reliable delivery for large region/snapshot payloads.
        /// Fragmentation must precede reliability so a lost fragment retransmits only that
        /// fragment rather than the full logical message.
        /// </summary>
        public NetworkPipeline Bulk;

        // -- bandwidth budget constants ------------------------------------------

        /// <summary>Minimum EVENT channel share on wired/Wi-Fi (device-matrix.md: ≥ 60%).</summary>
        public const float k_EventShareWired = 0.60f;

        /// <summary>Minimum EVENT channel share on mobile-HE cellular (device-matrix.md: ≥ 70%).</summary>
        public const float k_EventShareMobile = 0.70f;

        /// <summary>REPAIR channel share of the non-EVENT remainder.</summary>
        public const float k_RepairShare = 0.20f;

        /// <summary>BULK channel share of the non-EVENT remainder.</summary>
        public const float k_BulkShare = 0.20f;

        /// <summary>Sustained downstream budget on wired/Wi-Fi in KB/s (device-matrix.md).</summary>
        public const uint k_SustainedDownstreamWiredKb = 256;

        /// <summary>Sustained downstream budget on mobile-HE cellular in KB/s (device-matrix.md).</summary>
        public const uint k_SustainedDownstreamMobileKb = 96;

        // -- per-channel packet sizing -------------------------------------------

        /// <summary>
        /// Conservative ceiling for one non-fragmented live EVENT packet. The compact
        /// alteration batch currently tops out at 1172 bytes including its protocol envelope.
        /// </summary>
        public const int k_MaxEventPacketBytes = 1200;

        /// <summary>REPAIR payload ceiling for a single brick-repair message.</summary>
        public const int k_MaxRepairPacketBytes = 1024;

        /// <summary>BULK payload ceiling. Exceeds MTU, hence fragmentation.</summary>
        public const int k_MaxBulkPacketBytes = 16384;

        // -- configuration --------------------------------------------------------

        /// <summary>
        /// Creates the three pipelines on an existing driver.
        /// Must be called after the driver is created and before any connection is opened.
        /// Server and client must call this same method so pipeline IDs/stage order match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChannelSetup Create(ref NetworkDriver driver)
        {
            return new ChannelSetup
            {
                Event = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)),
                Repair = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)),
                Bulk = driver.CreatePipeline(
                    typeof(FragmentationPipelineStage),
                    typeof(ReliableSequencedPipelineStage)),
            };
        }

        /// <summary>
        /// Builds driver settings carrying the fragmentation capacity BULK needs.
        /// Pass the result to NetworkDriver.Create(settings).
        /// </summary>
        public static NetworkSettings DefaultSettings()
        {
            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(payloadCapacity: k_MaxBulkPacketBytes);
            return settings;
        }

        /// <summary>
        /// Computes the per-channel bandwidth budget for a given total capacity and device class.
        /// Device class affects bandwidth scheduling only; it never changes simulation interest.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (uint eventKb, uint repairKb, uint bulkKb) ComputeBudgets(
            uint totalCapacityKbPerSecond, bool isMobileCellular)
        {
            float eventShare = isMobileCellular ? k_EventShareMobile : k_EventShareWired;

            uint eventBudget  = (uint)(totalCapacityKbPerSecond * eventShare);
            uint remaining    = totalCapacityKbPerSecond - eventBudget;
            uint repairBudget = (uint)(remaining * k_RepairShare / (k_RepairShare + k_BulkShare));
            uint bulkBudget   = remaining - repairBudget;

            return (eventBudget, repairBudget, bulkBudget);
        }
    }
}
