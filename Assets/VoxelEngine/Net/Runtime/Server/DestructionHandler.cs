using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Interest;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Compatibility facade for callers that still think in terms of one destruction request.
    ///
    /// The canonical network path is ServerCommandInbox -> ServerCommandProcessor. This type no
    /// longer constructs authority from client payload fields: callers must supply the authenticated
    /// player session, authoritative tick/sequence/seed, Storage capabilities, and world applier.
    /// </summary>
    public static class DestructionHandler
    {
        private const float k_EventInterestRadius = 500f;

        public static AdjudicationResult Adjudicate(
            in C_AlterationRequest request,
            in ServerPlayerRegistry.PlayerSession player,
            ServerPlayerRegistry players,
            uint authoritativeTick,
            ushort authoritativeSequence,
            uint authoritativeSeed,
            IRegionReadSource readStorage,
            IRegionMutationStore mutationStorage,
            Validation.DensityCap densityCap,
            IAlterationApplier applier,
            in ProtectedZones zones = default)
        {
            if (readStorage == null)
                throw new ArgumentNullException(nameof(readStorage));
            if (mutationStorage == null)
                throw new ArgumentNullException(nameof(mutationStorage));
            if (applier == null)
                throw new ArgumentNullException(nameof(applier));

            AlterationEvent evt = request.ToAuthoritativeEvent(
                authoritativeTick,
                player.PlayerId,
                authoritativeSequence,
                authoritativeSeed);

            Validation.ValidationResult validation = AuthoritativeAlterationValidator.Validate(
                in evt,
                in player,
                players,
                readStorage,
                mutationStorage,
                applier,
                densityCap,
                in zones);

            if (validation != Validation.ValidationResult.Success)
                return AdjudicationResult.Reject(authoritativeTick, player.PlayerId, ToReason(validation));

            bool changed = applier.TryApply(
                mutationStorage, in evt, out NativeList<int3> affectedBlocks);
            if (affectedBlocks.IsCreated) affectedBlocks.Dispose();
            if (!changed)
                return AdjudicationResult.Reject(
                    authoritativeTick,
                    player.PlayerId,
                    S_AlterationRejected.Reason.InvalidTarget);

            var broadcast = new S_AlterationEvent(
                authoritativeTick,
                evt.origin >> VoxelGrid.RegionVoxelEdgeLog2);

            return AdjudicationResult.Accept(broadcast, evt);
        }

        private static S_AlterationRejected.Reason ToReason(Validation.ValidationResult result) => result switch
        {
            Validation.ValidationResult.TooFast => S_AlterationRejected.Reason.TooFast,
            Validation.ValidationResult.OverBudget => S_AlterationRejected.Reason.OverBudget,
            Validation.ValidationResult.OverDensity => S_AlterationRejected.Reason.OverDensity,
            Validation.ValidationResult.NotAttached => S_AlterationRejected.Reason.NotAttached,
            Validation.ValidationResult.InPlayerVolume => S_AlterationRejected.Reason.InPlayerVolume,
            Validation.ValidationResult.OutOfReach => S_AlterationRejected.Reason.OutOfReach,
            Validation.ValidationResult.ProtectedZone => S_AlterationRejected.Reason.ProtectedZone,
            _ => S_AlterationRejected.Reason.InvalidTarget,
        };

        /// <summary>
        /// Legacy interest helper retained for scaffold callers. New replication uses
        /// RegionSubscriptionIndex + ReplicationRouter and does not use this method.
        /// </summary>
        public static void Broadcast(
            in S_AlterationEvent evt,
            ref InterestFilter filter,
            in NativeArray<int3> playerPositions,
            Span<int> playerConnections)
        {
            int3 affectedRegion = evt.regionCoord;
            var interestedPlayers = new NativeList<int>(playerConnections.Length, Allocator.Temp);

            for (int p = 0; p < playerPositions.Length; p++)
            {
                if (InterestFilter.IsRegionInInterest(playerPositions[p], affectedRegion, k_EventInterestRadius))
                    interestedPlayers.Add(p);
            }

            // Actual sends belong to UtpServerHost/ReplicationRouter. Walking the result here keeps
            // the old API behavior side-effect free instead of inventing a parallel transport path.
            interestedPlayers.Dispose();
        }
    }

    public struct AdjudicationResult
    {
        public bool Accepted;
        public S_AlterationEvent Broadcast;
        public AlterationEvent Event;
        public S_AlterationRejected Rejection;

        public static AdjudicationResult Accept(S_AlterationEvent broadcast, AlterationEvent evt) =>
            new AdjudicationResult { Accepted = true, Broadcast = broadcast, Event = evt };

        public static AdjudicationResult Reject(
            uint serverTick,
            ushort playerId,
            S_AlterationRejected.Reason reason) =>
            new AdjudicationResult
            {
                Accepted = false,
                Rejection = new S_AlterationRejected(serverTick, playerId, reason),
            };
    }
}
