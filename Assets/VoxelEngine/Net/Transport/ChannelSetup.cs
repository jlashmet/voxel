using System.Runtime.CompilerServices;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Configures Unity Transport with three quality-of-service pipelines tuned to the
    /// bandwidth budgets in device-matrix.md.
    ///
    /// Channel allocation by device-matrix.md §Bandwidth budgets:
    ///   EVENT ≥ 60% reserved on wired/Wi-Fi, ≥ 70% on mobile-HE cellular.
    ///   REPAIR sits between EVENT and BULK with sequenced but unreliable delivery.
    ///   BULK consumes the remainder but yields to EVENT when the EVENT threshold
    ///   is approached — see BulkThrottle.cs for the throttling policy.
    ///
    /// Unity Transport has no notion of pipeline priority or per-pipeline bandwidth caps,
    /// so the reservation policy is enforced on our side by BulkThrottle rather than by
    /// the driver. What this type owns is pipeline *construction*: which stages each
    /// channel is built from, and the budget arithmetic BulkThrottle consumes.
    /// </summary>
    public struct ChannelSetup
    {
        // -- pipelines ------------------------------------------------------------

        /// <summary>Reliable sequenced delivery, for alteration events, inputs, and region sync.
        /// Guarantees delivery and ordering; this is the channel authoritative state rides on.</summary>
        public NetworkPipeline Event;

        /// <summary>Unreliable sequenced delivery, for drift-repair brick data. Stale repairs are
        /// worse than no repair, so out-of-order packets are dropped rather than reordered.</summary>
        public NetworkPipeline Repair;

        /// <summary>Fragmented unreliable delivery, for region downloads and other large payloads
        /// that exceed a single MTU and can tolerate loss (the client re-requests).</summary>
        public NetworkPipeline Bulk;

        // -- bandwidth budget constants --------------------------------------------

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

        // -- per-channel packet sizing ----------------------------------------------

        /// <summary>EVENT payload ceiling. SC-002 requires a ≥ 4000-voxel event to fit in 64 bytes.</summary>
        public const int k_MaxEventPacketBytes = 64;

        /// <summary>REPAIR payload ceiling for a single brick-repair message.</summary>
        public const int k_MaxRepairPacketBytes = 1024;

        /// <summary>BULK payload ceiling. Exceeds MTU, hence the fragmentation stage.</summary>
        public const int k_MaxBulkPacketBytes = 16384;

        // -- configuration --------------------------------------------------------

        /// <summary>
        /// Creates the three pipelines on an existing driver.
        ///
        /// Must be called after the driver is created and before any connection is opened —
        /// Unity Transport forbids adding pipelines once the driver is bound.
        /// </summary>
        /// <param name="driver">The driver to create pipelines on. Modified in place.</param>
        /// <returns>The three pipeline handles, to be held for the driver's lifetime.</returns>
        public static ChannelSetup Create(ref NetworkDriver driver)
        {
            return new ChannelSetup
            {
                Event = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)),
                Repair = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage)),

                // Fragmentation lets a single BULK send exceed the MTU; the driver
                // reassembles on the far side.
                Bulk = driver.CreatePipeline(typeof(FragmentationPipelineStage)),
            };
        }

        /// <summary>
        /// Builds driver settings carrying the fragmentation capacity BULK needs.
        /// Pass the result to <see cref="NetworkDriver.Create(NetworkSettings)"/>.
        /// </summary>
        public static NetworkSettings DefaultSettings()
        {
            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(payloadCapacity: k_MaxBulkPacketBytes);
            return settings;
        }

        /// <summary>
        /// Computes the per-channel bandwidth budget for a given total capacity and device class.
        /// Returns (eventKb, repairKb, bulkKb) matching device-matrix.md allocations.
        /// </summary>
        /// <param name="totalCapacityKbPerSecond">Total available downstream bandwidth in KB/s.</param>
        /// <param name="isMobileCellular">True for mobile-HE on cellular; uses the ≥ 70% EVENT reserve.</param>
        /// <returns>Tuple of (event, repair, bulk) KB/s budgets.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (uint eventKb, uint repairKb, uint bulkKb) ComputeBudgets(
            uint totalCapacityKbPerSecond, bool isMobileCellular)
        {
            float eventShare = isMobileCellular ? k_EventShareMobile : k_EventShareWired;

            // Reserve EVENT share first, then split remainder between REPAIR and BULK.
            uint eventBudget  = (uint)(totalCapacityKbPerSecond * eventShare);
            uint remaining    = totalCapacityKbPerSecond - eventBudget;
            uint repairBudget = (uint)(remaining * k_RepairShare / (k_RepairShare + k_BulkShare));
            uint bulkBudget   = remaining - repairBudget;

            return (eventBudget, repairBudget, bulkBudget);
        }
    }
}
