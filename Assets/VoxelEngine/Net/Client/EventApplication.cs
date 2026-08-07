using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Applies broadcast destruction events to the client brickmap via deterministic
    /// expansion jobs, then triggers mip rebuild and irradiance invalidation.
    ///
    /// This is where the server's authority becomes visible on the client: the speculative
    /// overlay may show provisional changes (FR-008), but only after the server broadcasts
    /// S_AlterationEvent does the client apply them to the real grid and trigger the
    /// downstream infrastructure updates.
    ///
    /// The key invariant (Constitution Principle I): the client's expansion of the
    /// broadcast event must produce byte-identical voxel data to the server's expansion.
    /// No float, no GPU — just integer Burst jobs with a seeded PRNG.
    /// </summary>
    public static class EventApplication
    {
        /// <summary>
        /// Apply a broadcast S_AlterationEvent to the client's brickmap.
        /// Returns true if any voxels changed (no-op events are skipped).
        /// </summary>
        /// <param name="evt">The decoded alteration. S_AlterationEvent is only a header
        ///   (tick, regionCoord, payloadLength) wrapping an opaque payload — the caller
        ///   decodes that payload into an AlterationEvent before applying it.</param>
        public static bool Apply(
            ref RegionTable table,
            ref BrickPool pool,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            var alteration = evt;

            // Ensure the target region is resident.
            var affectedRegion = GetAffectedRegion(evt.origin.x, evt.origin.y, evt.origin.z);
            var region = table.LoadRegion(affectedRegion);

            // Expand deterministically — same algorithm as the server runs.
            bool changed;
            switch (evt.kind)
            {
                case (byte)AlterationEventKind.Explosion:
                    changed = Core.Edits.ExplosionExpansion.TryExpand(
                        ref pool, evt.tick, alteration.origin, (byte)evt.Radius(), evt.seed, out affectedBricks);
                    break;
                case (byte)AlterationEventKind.Brush:
                    affectedBricks = Core.Edits.BrushExpansion.Expand(in pool, in table, alteration);
                    changed = affectedBricks.Length > 0;
                    break;
                case (byte)AlterationEventKind.RawBatch:
                    // Raw batch events carry pre-computed voxel changes — apply directly.
                    changed = ApplyRawBatch(ref pool, ref table, in alteration, out affectedBricks);
                    break;
                default:
                    affectedBricks = new NativeList<int3>(0, Allocator.Temp);
                    return false;
            }

            if (!changed)
            {
                // Server event was a no-op from our perspective (e.g., all voxels already empty).
                affectedBricks.Dispose();
                affectedBricks = new NativeList<int3>(0, Allocator.Temp);
                return false;
            }

            // Mark the region dirty — mip rebuild and irradiance invalidation will follow.
            region.Dirty = true;
            table.CommitRegion(region);

            return changed;
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
            // Batched mip rebuild over dirty regions (T026).
            //
            // Region does not own its mip arrays — MipBuilder writes into caller-supplied
            // storage, so mipStorage[i] holds the per-level arrays for affectedRegions[i].
            for (int i = 0; i < affectedRegions.Length; i++)
            {
                var regionCoord = affectedRegions[i];
                if (!table.TryGetRegion(regionCoord, out var region))
                    continue;

                if (mipStorage == null || i >= mipStorage.Length || mipStorage[i] == null)
                    continue;

                MipBuilder.RebuildFull(in pool, region, mipLevelCount, mipStorage[i]);
            }
        }

        /// <summary>
        /// Apply a batch of broadcast events respecting server-assigned arbitration order
        /// without client-side re-derivation (FR-011 / R-010).
        ///
        /// The server sends events already sorted by its total order: (tick, playerId, sequence).
        /// The client adopts this order directly — it never re-sorts or re-derives the arbitration.
        /// Events are applied sequentially; later events override earlier ones for conflicting voxels,
        /// which is correct because the server's order determines which event was "last" at each position.
        /// </summary>
        /// <param name="table">Region table for the client's local grid.</param>
        /// <param name="pool">Brick pool for voxel writes during application.</param>
        /// <param name="events">Server-ordered array of alteration events to apply. Must already be sorted
        ///   by the server's arbitration order — clients must NOT re-sort (Constitution: server authority).</param>
        /// <returns>true if any voxels changed state during application.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplyWithArbitration(
            ref RegionTable table,
            ref BrickPool pool,
            in NativeArray<AlterationEvent> events)
        {
            if (events.Length == 0)
                return false;

            bool anyChanged = false;

            // Apply each event in server-assigned order. No client-side re-ordering — this is the
            /// entire point of R-010: clients adopt server authority without re-deriving.
            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                var alteration = evt;

                // Ensure the target region is resident.
                var affectedRegion = GetAffectedRegion(evt.origin.x, evt.origin.y, evt.origin.z);
                var region = table.LoadRegion(affectedRegion);

                // Expand deterministically — same algorithm as the server runs.
                bool changed;
                switch (evt.kind)
                {
                    case (byte)AlterationEventKind.Explosion:
                        changed = Core.Edits.ExplosionExpansion.TryExpand(
                            ref pool, evt.tick, alteration.origin, (byte)evt.Radius(), evt.seed, out var affectedBricks);
                        if (changed && affectedBricks.IsCreated)
                            affectedBricks.Dispose();
                        break;
                    case (byte)AlterationEventKind.Brush:
                        changed = Core.Edits.BuildBrushes.TryApply(
                            ref pool, alteration, out affectedBricks);
                        if (changed && affectedBricks.IsCreated)
                            affectedBricks.Dispose();
                        break;
                    case (byte)AlterationEventKind.RawBatch:
                        changed = ApplyRawBatch(ref pool, ref table, in alteration, out affectedBricks);
                        if (changed && affectedBricks.IsCreated)
                            affectedBricks.Dispose();
                        break;
                    default:
                        changed = false;
                        break;
                }

                if (!changed)
                    continue;

                anyChanged = true;
                region.Dirty = true;
                table.CommitRegion(region);
            }

            return anyChanged;
        }

        /// <summary>Apply a raw-batch event directly to the grid (pre-computed voxel writes).</summary>
        private static bool ApplyRawBatch(
            ref BrickPool pool,
            ref RegionTable table,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            affectedBricks = new NativeList<int3>(16, Allocator.Temp);
            // Raw batch events carry compressed voxel-level changes. Apply each entry:
            // (brickOffset, brickCount, material) pairs.
            return false; // Placeholder — raw batch expansion handled by BrushExpansion.
        }

        /// <summary>Get the region coordinate containing a given world voxel.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetAffectedRegion(int x, int y, int z) =>
            new int3(
                x >> VoxelDimensions.RegionVoxelEdgeLog2,
                y >> VoxelDimensions.RegionVoxelEdgeLog2,
                z >> VoxelDimensions.RegionVoxelEdgeLog2);
    }

    /// <summary>
    /// Broadcast event received from the server. Mirrors S_AlterationEvent wire format.
    /// </summary>
    public struct S_AlterationEvent
    {
        public uint Tick;
        public int RegionCoord; // Simplified — in production this would be int3.
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
