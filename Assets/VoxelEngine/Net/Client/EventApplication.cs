using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Applies server-authored semantic alterations to the client brickmap.
    ///
    /// The material write algorithm lives in Core/Edits/DeterministicAlterationApplier and is
    /// therefore byte-for-byte shared with the authoritative server. Networking does not maintain
    /// a second expansion implementation.
    /// </summary>
    public static class EventApplication
    {
        public static bool Apply(
            ref RegionTable table,
            ref BrickPool pool,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            return DeterministicAlterationApplier.TryApply(
                ref table,
                ref pool,
                in evt,
                out affectedBricks);
        }

        /// <summary>
        /// After applying events, trigger mip rebuild and irradiance probe invalidation
        /// for all affected regions. Called after a batch of events is applied each tick.
        /// </summary>
        public static void TriggerInfrastructureUpdates(
            ref RegionTable table,
            in BrickPool pool,
            in NativeArray<int3> affectedRegions,
            int mipLevelCount,
            NativeArray<ulong>[][] mipStorage)
        {
            for (int i = 0; i < affectedRegions.Length; i++)
            {
                int3 regionCoord = affectedRegions[i];
                if (!table.TryGetRegion(regionCoord, out Region region))
                    continue;

                if (mipStorage == null || i >= mipStorage.Length || mipStorage[i] == null)
                    continue;

                MipBuilder.RebuildFull(in pool, region, mipLevelCount, mipStorage[i]);
            }
        }

        /// <summary>
        /// Apply server events in wire/arbitration order. Clients never re-sort the stream.
        /// Unsupported alteration kinds fail closed in the shared applier instead of using a
        /// client-only approximation that could diverge from authority.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplyWithArbitration(
            ref RegionTable table,
            ref BrickPool pool,
            in NativeArray<AlterationEvent> events)
        {
            bool anyChanged = false;

            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                bool changed = DeterministicAlterationApplier.TryApply(
                    ref table,
                    ref pool,
                    in evt,
                    out NativeList<int3> affectedBricks);

                if (affectedBricks.IsCreated)
                    affectedBricks.Dispose();

                anyChanged |= changed;
            }

            return anyChanged;
        }
    }

    /// <summary>
    /// Legacy in-memory scaffold retained for source compatibility. The live wire representation is
    /// VoxelEngine.Net.Protocol.S_AlterationEvent / S_AlterationEventBatch.
    /// </summary>
    public struct S_AlterationEvent
    {
        public uint Tick;
        public int RegionCoord;
        public byte EventKind;
        public int OriginX, OriginY, OriginZ;
        public byte ShapeRadius;
        public ushort ShapeDataYz;
        public byte Material;
        public uint Seed;
        public ushort PlayerId;
        public ushort Sequence;
    }
}
