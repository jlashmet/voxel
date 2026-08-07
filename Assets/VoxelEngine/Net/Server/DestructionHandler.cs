using VoxelEngine.Net.Interest;
using VoxelEngine.Core.Storage;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Implements server adjudication and S_AlterationEvent broadcast for destruction.
    ///
    /// This is the authoritative choke point (Constitution Principle III) where every
    /// client destruction request is validated, expanded, and broadcast. No voxel on any
    /// client changes without this component's approval.
    ///
    /// The flow:
    ///   1. Client sends C_AlterationRequest via EVENT channel
    ///   2. Server validates against all predicates in Validation.cs
    ///   3. Server expands the event deterministically (integer Burst jobs)
    ///   4. Server broadcasts S_AlterationEvent to all nearby players
    ///   5. Clients apply broadcast events to their local brickmaps
    /// </summary>
    public static class DestructionHandler
    {
        /// <summary>
        /// Radius in voxels within which a player is considered a recipient of an event.
        /// Server-side constant: interest radius must never vary by device class
        /// (Constitution Principle IV).
        /// </summary>
        private const float k_EventInterestRadius = 500f;

        /// <summary>
        /// Adjudicate a client's destruction request: validate, expand, and produce
        /// an S_AlterationEvent for broadcast. Returns null if the request is rejected.
        /// </summary>
        public static AdjudicationResult Adjudicate(
            in C_AlterationRequest request,
            ref RegionTable table,
            ref BrickPool pool,
            Validation.AllocationBudget budget,
            Validation.DensityCap densityCap)
        {
            // The request already carries every field an AlterationEvent needs; building it
            // once here means validation, expansion, and broadcast all see the same struct.
            var evt = new AlterationEvent
            {
                kind = request.eventKind,
                tick = request.tick,
                origin = request.origin,
                shapeKind = request.eventKind,
                shapeData = request.shapeRadius,
                material = request.material,
                seed = request.seed,
                playerId = request.playerId,
                sequence = request.sequence,
            };

            // Step 1: Validate against all predicates.
            var validation = Validation.Validate(
                request.playerId, evt, ref table, in pool, budget, densityCap);

            if (validation != Validation.ValidationResult.Success)
                return AdjudicationResult.Reject(request, ToReason(validation));

            // Step 2: Expand deterministically (same code the client will run).
            NativeList<int3> affectedBricks;
            bool expanded;

            switch (request.eventKind)
            {
                case (byte)AlterationEventKind.Explosion:
                    expanded = Core.Edits.ExplosionExpansion.TryExpand(
                        ref pool, request.tick, request.origin,
                        (byte)request.shapeRadius, request.seed, out affectedBricks);
                    break;
                case (byte)AlterationEventKind.Brush:
                    affectedBricks = Core.Edits.BrushExpansion.Expand(in pool, in table, evt);
                    expanded = affectedBricks.Length > 0;
                    break;
                default:
                    expanded = false;
                    affectedBricks = default;
                    break;
            }

            if (!expanded)
            {
                if (affectedBricks.IsCreated) affectedBricks.Dispose();
                return AdjudicationResult.Reject(
                    request, S_AlterationRejected.Reason.InvalidTarget);
            }

            // Step 3: Apply to server world state (this is the authoritative write).
            ApplyToServerWorld(ref table, in pool, affectedBricks, request.tick);

            // Step 4: Build the broadcast header. S_AlterationEvent carries only tick,
            // regionCoord, and payload length — the AlterationEvent itself is the payload,
            // which is what keeps a 4000-voxel edit inside the SC-002 64-byte budget.
            var broadcast = new S_AlterationEvent(
                request.tick,
                table.GetRegionCoordFor(request.origin));

            affectedBricks.Dispose();
            return AdjudicationResult.Accept(broadcast, evt);
        }

        /// <summary>
        /// Maps a validation failure onto the wire-level rejection reason (FR-009).
        /// The two enums are deliberately parallel, but the mapping is explicit so that
        /// changing one does not silently corrupt the other's wire values.
        /// </summary>
        private static S_AlterationRejected.Reason ToReason(Validation.ValidationResult r) => r switch
        {
            Validation.ValidationResult.TooFast        => S_AlterationRejected.Reason.TooFast,
            Validation.ValidationResult.OverBudget     => S_AlterationRejected.Reason.OverBudget,
            Validation.ValidationResult.OverDensity    => S_AlterationRejected.Reason.OverDensity,
            Validation.ValidationResult.NotAttached    => S_AlterationRejected.Reason.NotAttached,
            Validation.ValidationResult.InPlayerVolume => S_AlterationRejected.Reason.InPlayerVolume,
            Validation.ValidationResult.OutOfReach     => S_AlterationRejected.Reason.OutOfReach,
            Validation.ValidationResult.ProtectedZone  => S_AlterationRejected.Reason.ProtectedZone,
            _                                          => S_AlterationRejected.Reason.InvalidTarget,
        };

        /// <summary>
        /// Broadcast the event to all interested players via the EVENT channel.
        /// Uses spatial interest management to determine recipients.
        /// </summary>
        public static void Broadcast(
            in S_AlterationEvent evt,
            ref InterestFilter filter,
            in NativeArray<int3> playerPositions,
            Span<int> playerConnections)
        {
            // The event header carries the region it lands in; that is the interest key.
            int3 affectedRegion = evt.regionCoord;

            // InterestFilter is region-oriented (GetInterestedRegions), so recipients are
            // resolved by testing each connected player's position against this region
            // rather than by asking the filter for a player list it does not maintain.
            var interestedPlayers = new NativeList<int>(playerConnections.Length, Allocator.Temp);
            for (int p = 0; p < playerPositions.Length; p++)
            {
                if (InterestFilter.IsRegionInInterest(playerPositions[p], affectedRegion, k_EventInterestRadius))
                    interestedPlayers.Add(p);
            }

            for (int i = 0; i < interestedPlayers.Length; i++)
            {
                int playerId = interestedPlayers[i];
                if (playerId >= 0 && playerId < playerConnections.Length)
                {
                    // Send via EVENT channel (reliable).
                    var connectionId = playerConnections[playerId];
                    SendToPlayer(connectionId, in evt);
                }
            }

            interestedPlayers.Dispose();
        }

        /// <summary>
        /// Apply the expansion result to the server's authoritative world state.
        /// This is the only place on the server where SetVoxel can be called — the single
        /// choke point that guarantees all changes go through validation.
        /// </summary>
        private static void ApplyToServerWorld(ref RegionTable table, in BrickPool pool,
                                               in NativeList<int3> affectedBricks, uint tick)
        {
            // Mark each affected region as dirty (needs mip rebuild and potential sync).
            foreach (var brickCoord in affectedBricks)
            {
                var regionCoord = GetRegionCoord(brickCoord);
                if (!table.IsResident(regionCoord))
                {
                    table.LoadRegion(regionCoord); // Materialise if not resident.
                }

                // Mark dirty for later processing (mip rebuild, compaction).
                // In production this would update the region's Dirty flag.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetRegionCoord(int x, int y, int z) =>
            new int3(
                x >> VoxelDimensions.RegionVoxelEdgeLog2,
                y >> VoxelDimensions.RegionVoxelEdgeLog2,
                z >> VoxelDimensions.RegionVoxelEdgeLog2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetRegionCoord(int3 worldVoxel) => GetRegionCoord(worldVoxel.x, worldVoxel.y, worldVoxel.z);

        /// <summary>Get the region coordinate for a given voxel origin.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 GetAffectedRegion(int originX, int originY, int originZ) =>
            GetRegionCoord(originX, originY, originZ);

        /// <summary>Send event to a specific player connection. Stub for the transport layer.</summary>
        private static void SendToPlayer(int connectionId, in S_AlterationEvent evt)
        {
            // In production: network.Send(connectionId, evt.Encode(), channel: ChannelType.Reliable);
        }
    }

    /// <summary>
    /// Helper extension to get region coord for a world position.
    /// </summary>
    public static class RegionTableExtensions
    {
        public static int3 GetRegionCoordFor(this ref RegionTable table, int3 origin) =>
            new int3(
                origin.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                origin.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                origin.z >> VoxelDimensions.RegionVoxelEdgeLog2);
    }

    /// <summary>
    /// Outcome of server adjudication: either an accepted event to broadcast, or a
    /// rejection to return to the requesting client alone.
    ///
    /// Adjudicate cannot simply return S_AlterationEvent — an accepted edit and a
    /// rejected one go to different recipients over different paths, and conflating
    /// them is how a rejected edit leaks to other players.
    /// </summary>
    public struct AdjudicationResult
    {
        /// <summary>True when the request passed validation and expanded successfully.</summary>
        public bool Accepted;

        /// <summary>Broadcast header. Valid only when <see cref="Accepted"/>.</summary>
        public S_AlterationEvent Broadcast;

        /// <summary>The payload clients decode and expand. Valid only when <see cref="Accepted"/>.</summary>
        public AlterationEvent Event;

        /// <summary>Rejection to send to the requester. Valid only when not <see cref="Accepted"/>.</summary>
        public S_AlterationRejected Rejection;

        public static AdjudicationResult Accept(S_AlterationEvent broadcast, AlterationEvent evt) =>
            new AdjudicationResult { Accepted = true, Broadcast = broadcast, Event = evt };

        public static AdjudicationResult Reject(in C_AlterationRequest request, S_AlterationRejected.Reason reason) =>
            new AdjudicationResult
            {
                Accepted = false,
                Rejection = new S_AlterationRejected(request.tick, request.playerId, reason),
            };
    }
}
