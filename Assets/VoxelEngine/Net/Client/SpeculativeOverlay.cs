using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Pending voxel modification keyed by brick coordinate in the real grid.
    ///
    /// Each entry represents a speculative change that has not yet been confirmed by the
    /// server. When confirmed, the pending data is promoted into the real grid; when
    /// rejected, the pending brick is dissolved with an animation cue (FR-009).
    ///
    /// This struct holds only the overlay metadata — the actual voxel writes happen into
    /// the caller's BrickPool during promotion. The overlay itself is keyed by brick
    /// coordinate so that multiple changes to the same brick are always resolved via
    /// last-write-wins per tick (data-model.md: SpeculativeOverlay state transitions).
    /// </summary>
    public struct PendingVoxel
    {
        /// <summary>Material index for this speculative voxel. 0 = empty (demolish).</summary>
        public byte material;

        /// <summary>Server tick at which this change was made, for ordering and reconciliation.</summary>
        public uint tick;

        /// <summary>True when the server has confirmed this change. Pending overlays promote to true on ConfirmTick.</summary>
        public bool confirmed;
    }

    /// <summary>
    /// Client-local speculative overlay keyed by brick coordinate in the real grid.
    ///
    /// Implements the SpeculativeOverlay entity from data-model.md: a parallel view over
    /// the authoritative world that lets the client show immediate visual feedback for
    /// player actions without waiting for server confirmation (FR-008, local alteration
    /// feedback within 1 frame).
    ///
    /// State transitions (data-model.md §SpeculativeOverlay):
    ///   Pending -> Confirmed: promote into the real grid, discard overlay entry.
    ///   Pending -> Rejected: dissolve with animation cue, surface reason (FR-009).
    ///
    /// Invariant: rendered visibly distinct so provisionality is legible (rendering layer
    /// must use a different shader pass or tint for pending voxels — handled by the render
    /// feature, not this type). Collision resolves against one side deterministically, never
    /// a blend (Constitution Principle I).
    /// </summary>
    public sealed class SpeculativeOverlay : IDisposable
    {
        // -- state ----------------------------------------------------------------

        /// <summary>
        /// Pending modifications keyed by brick coordinate. Parallel to the real grid:
        /// the key is an int3 brick coordinate in the world's brick grid, and the value
        /// holds the speculative voxel data.
        /// </summary>
        private NativeHashMap<int3, PendingVoxel> _pending;

        /// <summary>Highest tick seen by ApplyPending. Drives the "promote up to tick" semantics.</summary>
        private uint _highestTick;

        /// <summary>Reconciliation result state, set after a reconciliation pass completes.</summary>
        private ReconciliationResult _reconResult;

        // -- construction ---------------------------------------------------------

        public SpeculativeOverlay()
        {
            _pending = new NativeHashMap<int3, PendingVoxel>(64, Allocator.Persistent);
            _highestTick = 0;
            _reconResult = default;
        }

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Add a pending change to the overlay from an AlterationEvent.
        ///
        /// If there is already a pending entry for this brick coordinate, it is overwritten
        /// — later events always win (data-model.md: arbitration via (tick, playerId, sequence)
        /// order, with material priority breaking same-tick ties).
        /// </summary>
        /// <param name="evt">The server-confirmed alteration event to apply to the overlay.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyPending(in AlterationEvent evt)
        {
            // Each brick affected by the event gets a pending entry.
            int3 startBrick = new int3(
                evt.origin.x >> VoxelDimensions.BrickEdgeLog2,
                evt.origin.y >> VoxelDimensions.BrickEdgeLog2,
                evt.origin.z >> VoxelDimensions.BrickEdgeLog2);

            // Expand based on event shape — use the radius for explosions, 1 for simple placements.
            ushort radius = evt.kind == AlterationEvent.KindExplosion ? evt.Radius() : (ushort)1;

            for (int bx = -radius; bx <= radius; bx++)
            {
                for (int by = -radius; by <= radius; by++)
                {
                    for (int bz = -radius; bz <= radius; bz++)
                    {
                        int3 brickCoord = startBrick + new int3(bx, by, bz);

                        // Skip bricks outside the event's spherical/box volume.
                        int dist2 = bx * bx + by * by + bz * bz;
                        if (evt.kind == AlterationEvent.KindExplosion && dist2 > radius * radius)
                            continue;

                        PendingVoxel entry;
                        entry.material = evt.material;
                        entry.tick = evt.tick;
                        entry.confirmed = false;

                        _pending[brickCoord] = entry;
                    }
                }
            }

            _highestTick = math.max(_highestTick, evt.tick);
        }

        /// <summary>
        /// Promote overlay voxels to the real grid at the given tick.
        ///
        /// All pending entries with tick <= the confirmed tick are promoted into the provided
        /// region's brick data and then removed from the overlay. Unconfirmed entries (tick >
        /// the confirmed tick) remain in place for future confirmation rounds.
        /// </summary>
        /// <param name="tick">The server tick being confirmed. Entries with tick <= this value are promoted.</param>
        /// <param name="regionTable">Reference to the region table containing the affected regions.</param>
        /// <param name="pool">Reference to the brick pool where mixed bricks will be materialised.</param>
        public void ConfirmTick(uint tick, ref RegionTable regionTable, ref BrickPool pool)
        {
            // Keys array for iteration — safe because we remove keys inside the loop.
            var keys = _pending.GetKeyArray(Allocator.Temp);

            foreach (int3 brickCoord in keys)
            {
                if (!_pending.TryGetValue(brickCoord, out var entry))
                    continue;

                if (entry.tick > tick)
                    continue; // Not yet confirmed by server.

                // Promote: write the material into the real grid.
                int regionX = brickCoord.x >> VoxelDimensions.RegionEdgeLog2;
                int regionY = brickCoord.y >> VoxelDimensions.RegionEdgeLog2;
                int regionZ = brickCoord.z >> VoxelDimensions.RegionEdgeLog2;
                int3 regionCoord = new int3(regionX, regionY, regionZ);

                // Only promote if the region is resident (non-resident reads as empty).
                // The entry is dropped either way: the server has confirmed it, so holding it
                // pending forever would leak the overlay. On re-entry the client re-fetches
                // the region from the server, which already has the change.
                if (!regionTable.TryGetRegion(regionCoord, out var region))
                {
                    _pending.Remove(brickCoord);
                    continue;
                }

                // _pending is keyed by brick coordinates, so the in-region index is a mask
                // only. Shifting by BrickEdgeLog2 again here would treat a brick coord as a
                // voxel coord and land on the wrong brick.
                int localX = brickCoord.x & VoxelDimensions.RegionEdgeMask;
                int localY = brickCoord.y & VoxelDimensions.RegionEdgeMask;
                int localZ = brickCoord.z & VoxelDimensions.RegionEdgeMask;

                int brickInRegion = Region.BrickIndex(localX, localY, localZ);

                // Write material: for uniform writes, use the uniform reference directly;
                // for mixed, allocate a pool slot and fill it.
                if (entry.material == VoxelDimensions.MaterialEmpty)
                {
                    region.BrickRefs[brickInRegion] = BrickRef.Empty;
                }
                else
                {
                    var existing = region.GetBrick(localX, localY, localZ);

                    if (existing.IsUniform && existing.UniformMaterial == entry.material)
                    {
                        // Already uniform with the target material — no write needed.
                        _pending.Remove(brickCoord);
                        continue;
                    }

                    if (!existing.IsMixed)
                    {
                        // Uniform -> mixed: allocate a pool slot.
                        int newIndex = pool.Allocate();
                        pool.FillBrick(newIndex, entry.material);
                        region.SetBrick(localX, localY, localZ, BrickRef.FromPoolIndex(newIndex));
                    }
                    else
                    {
                        // Mixed: set the material in the pool.
                        int poolIdx = existing.PoolIndex;
                        for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                            pool.SetVoxel(poolIdx, v, entry.material);

                        // Check if it collapsed to uniform after this write.
                        if (pool.TryGetUniformMaterial(poolIdx, out var unified))
                        {
                            pool.Free(poolIdx);
                            region.SetBrick(localX, localY, localZ, BrickRef.Uniform(unified));
                        }
                    }

                    region.Dirty = true;
                }

                // Region is a value type — the writes above landed on a local copy, so the
                // commit is what actually publishes them.
                regionTable.CommitRegion(region);

                // Promote: remove from overlay.
                _pending.Remove(brickCoord);
            }

            keys.Dispose();
        }

        /// <summary>
        /// Mark all pending entries up to and including the given tick as rejected.
        ///
        /// Rejected changes dissolve (FR-009) — they are removed from the overlay without
        /// writing to the real grid. The reason is recorded for animation/feedback purposes
        /// but not stored in this struct (the render layer reads rejection state separately).
        /// </summary>
        /// <param name="tick">Reject all pending entries with tick <= this value.</param>
        /// <param name="reason">Optional span describing the rejection reason (e.g. moderation denial).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RejectTick(uint tick, Span<byte> reason)
        {
            // Rejected entries are simply removed from the overlay without promoting to the grid.
            var keys = _pending.GetKeyArray(Allocator.Temp);

            foreach (int3 brickCoord in keys)
            {
                if (!_pending.TryGetValue(brickCoord, out var entry))
                    continue;

                if (entry.tick <= tick)
                {
                    // Dissolve pending change — do NOT write to the real grid.
                    _pending.Remove(brickCoord);
                }
            }

            keys.Dispose();
        }

        /// <summary>
        /// Get a NativeArray of all brick coordinates that are currently in the overlay,
        /// for rendering the speculative state visually distinct from the real grid.
        ///
        /// The returned array includes both pending and confirmed-but-unpromoted entries.
        /// Each coordinate is in world brick space (not region-local).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<int3> GetRenderedState(Allocator allocator)
        {
            return _pending.GetKeyArray(allocator);
        }

        /// <summary>True when there are unconfirmed pending entries in the overlay.</summary>
        public bool HasPending => _pending.Count > 0;

        /// <summary>Number of pending entries awaiting server confirmation.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// Try to get the material of a pending voxel at a given brick coordinate.
        /// Used by collision systems to check overlay solidity (C-003: one side only).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPendingMaterial(int3 brickCoord, out byte material)
        {
            if (_pending.TryGetValue(brickCoord, out var entry))
            {
                material = entry.material;
                return true;
            }
            material = 0;
            return false;
        }

        /// <summary>Clear all pending entries. Called on disconnection or world reset.</summary>
        public void Clear() => _pending.Clear();

        /// <summary>Apply a reconciliation result to reconcile overlay state with server truth.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyReconciliationResult(in ReconciliationResult result)
        {
            // Merge the reconciliation's brick-by-brick comparison results back into the overlay.
            foreach (var kvp in result.ModifiedBricks)
            {
                if (!kvp.Value.MatchesServer)
                {
                    // Server disagrees — remove this brick from the speculative overlay.
                    _pending.Remove(kvp.Key);
                }
            }
        }

        /// <summary>Dispose native resources.</summary>
        public void Dispose()
        {
            if (_pending.IsCreated) _pending.Dispose();
        }

        /// <summary>Advance the tick counter for all pending entries (used during forward simulation).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTick(uint tick)
        {
            // Advance tracks the highest tick — no mutation needed, just bookkeeping.
            _highestTick = math.max(_highestTick, tick);
        }

        /// <summary>
        /// Get the reconciliation result for inspection by external systems (tests, logging).
        /// </summary>
        public ReconciliationResult GetResult() => _reconResult;
    }

    /// <summary>
    /// Result of a reconciliation pass: per-brick comparison between client speculative state
    /// and server authoritative state. Used by the caller to drive overlay promotion and rollback.
    /// </summary>
    public struct ReconciliationResult
    {
        /// <summary>Per-brick result from the reconciliation pass. Keyed by brick coordinate.</summary>
        public NativeHashMap<int3, BrickReconResult> ModifiedBricks;

        /// <summary>True when any bricks were rolled back during this pass.</summary>
        public bool HadRollback { get; set; }
    }

    /// <summary>Per-brick result from a single reconciliation tick comparison.</summary>
    public struct BrickReconResult
    {
        /// <summary>True when the client's speculative state matches the server's state for this brick.</summary>
        public bool MatchesServer;

        /// <summary>The server's authoritative material for this brick at the reconciliation tick.</summary>
        public byte ServerMaterial;

        /// <summary>The client's speculative material for this brick before reconciliation.</summary>
        public byte ClientMaterial;
    }
}
