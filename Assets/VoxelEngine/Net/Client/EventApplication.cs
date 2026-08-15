using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Client
{
    public static class EventApplication
    {
        public static bool Apply(
            IAlterationApplier applier,
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            return applier.TryApply(storage, in evt, out affectedBricks);
        }

        /// <summary>
        /// After applying events, trigger mip rebuild and irradiance probe invalidation
        /// for all affected regions. Called after a batch of events is applied each tick.
        /// </summary>
        public static void TriggerInfrastructureUpdates(
            ref RegionTable table,
            in BrickPool pool,
            in NativeArray<int3> affectedRegions,
            int mipLevelCount)
        {
            // Batched mip rebuild over dirty regions (T026).
            //
            // Each region owns its own flattened pyramid, so an edit refreshes storage that
            // travels with the region and survives until the region itself is evicted. A
            // region that has never had mips allocated gets them here on first touch.
            for (int i = 0; i < affectedRegions.Length; i++)
            {
                var regionCoord = affectedRegions[i];
                if (!table.TryGetRegion(regionCoord, out var region))
                    continue;

                if (!region.HasMips)
                {
                    region.AllocateMips(mipLevelCount, Allocator.Persistent);
                    table.CommitRegion(in region);
                }

                MipBuilder.RebuildRegion(in pool, ref region);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplyWithArbitration(
            IAlterationApplier applier,
            IRegionMutationStore storage,
            in NativeArray<AlterationEvent> events)
        {
            bool anyChanged = false;
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                bool changed = applier.TryApply(
                    storage, in evt, out NativeList<int3> affectedBricks);
                if (affectedBricks.IsCreated) affectedBricks.Dispose();
                anyChanged |= changed;
            }
            return anyChanged;
        }
    }

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
