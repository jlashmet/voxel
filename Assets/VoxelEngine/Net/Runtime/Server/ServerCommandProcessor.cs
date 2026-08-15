using System;
using System.Collections.Generic;
using Unity.Collections;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Server
{
    public interface IAuthoritativePlayerInputSink
    {
        void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick);
    }

    public interface IProcessedInputAckSource
    {
        bool TryGetLastProcessedInputSequence(ushort playerId, out ushort sequence);
    }

    public interface IAuthoritativeAlterationPublisher
    {
        void PublishAlteration(in AlterationEvent evt);
    }

    public interface IAlterationRejectionSink
    {
        void SendAlterationRejected(uint connectionId, in S_AlterationRejected rejection);
    }

    /// <summary>
    /// Fixed-tick consumer of the frame-level ServerCommandInbox. Network arrival order is never
    /// authority; commands resolve through authenticated player identity and deterministic ordering.
    /// </summary>
    public sealed class ServerCommandProcessor : IProcessedInputAckSource
    {
        private const uint MaxFutureInputTicks = 2;

        private readonly ServerCommandInbox _inbox;
        private readonly ServerPlayerRegistry _players;
        private readonly AlterationRateLimiter _rateLimiter;
        private readonly uint _serverSeed;
        private readonly Validation.DensityCap _densityCap;

        private readonly List<ServerCommandInbox.QueuedAlterationRequest> _alterationDrain = new List<ServerCommandInbox.QueuedAlterationRequest>(128);
        private readonly List<ServerCommandInbox.QueuedPlayerInput> _inputDrain = new List<ServerCommandInbox.QueuedPlayerInput>(256);
        private readonly List<ResolvedAlteration> _resolvedAlterations = new List<ResolvedAlteration>(128);
        private readonly List<ResolvedInput> _resolvedInputs = new List<ResolvedInput>(256);
        private readonly Dictionary<ushort, ushort> _lastDurableSequence = new Dictionary<ushort, ushort>(64);
        private readonly Dictionary<ushort, ushort> _lastProcessedInputSequence = new Dictionary<ushort, ushort>(64);

        public long UnauthenticatedCommands { get; private set; }
        public long StaleOrDuplicateCommands { get; private set; }
        public long RejectedAlterations { get; private set; }
        public long AcceptedAlterations { get; private set; }

        public ServerCommandProcessor(
            ServerCommandInbox inbox,
            ServerPlayerRegistry players,
            AlterationRateLimiter rateLimiter,
            uint serverSeed,
            Validation.DensityCap densityCap)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _serverSeed = serverSeed;
            _densityCap = densityCap;
        }

        public void ProcessTick(
            uint serverTick,
            IRegionReadSource readStorage,
            IRegionMutationStore mutationStorage,
            in ProtectedZones zones,
            IAuthoritativePlayerInputSink inputSink,
            IAlterationApplier applier,
            IAuthoritativeAlterationPublisher publisher,
            IAlterationRejectionSink rejectionSink)
        {
            if (serverTick == 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (readStorage == null) throw new ArgumentNullException(nameof(readStorage));
            if (mutationStorage == null) throw new ArgumentNullException(nameof(mutationStorage));
            if (inputSink == null) throw new ArgumentNullException(nameof(inputSink));
            if (applier == null) throw new ArgumentNullException(nameof(applier));
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (rejectionSink == null) throw new ArgumentNullException(nameof(rejectionSink));

            DrainAndResolve(serverTick);
            ProcessInputs(serverTick, inputSink);
            ProcessAlterations(
                serverTick, readStorage, mutationStorage, in zones,
                applier, publisher, rejectionSink);
        }

        public bool TryGetLastProcessedInputSequence(ushort playerId, out ushort sequence) =>
            _lastProcessedInputSequence.TryGetValue(playerId, out sequence);

        public void RemovePlayer(ushort playerId)
        {
            if (playerId == 0) return;
            _lastDurableSequence.Remove(playerId);
            _lastProcessedInputSequence.Remove(playerId);
            _rateLimiter.RemovePlayer(playerId);
        }

        private void DrainAndResolve(uint serverTick)
        {
            _alterationDrain.Clear();
            _inputDrain.Clear();
            _resolvedAlterations.Clear();
            _resolvedInputs.Clear();

            _inbox.DrainAlterations(_alterationDrain);
            _inbox.DrainInputs(_inputDrain);

            for (int i = 0; i < _inputDrain.Count; i++)
            {
                ServerCommandInbox.QueuedPlayerInput queued = _inputDrain[i];
                if (!_players.TryGetByConnection(queued.ConnectionId, out var player))
                {
                    UnauthenticatedCommands++;
                    continue;
                }

                if (!IsClientTickPlausible(queued.Input.tick, serverTick))
                {
                    StaleOrDuplicateCommands++;
                    continue;
                }

                _resolvedInputs.Add(new ResolvedInput(player.PlayerId, queued));
            }

            for (int i = 0; i < _alterationDrain.Count; i++)
            {
                ServerCommandInbox.QueuedAlterationRequest queued = _alterationDrain[i];
                if (!_players.TryGetByConnection(queued.ConnectionId, out var player))
                {
                    UnauthenticatedCommands++;
                    continue;
                }

                if (!IsClientTickPlausible(queued.Request.tick, serverTick) ||
                    !IsNewDurableSequence(player.PlayerId, queued.Request.sequence))
                {
                    StaleOrDuplicateCommands++;
                    continue;
                }

                _lastDurableSequence[player.PlayerId] = queued.Request.sequence;
                _resolvedAlterations.Add(new ResolvedAlteration(player, queued));
            }

            _resolvedInputs.Sort(ResolvedInputComparer.Instance);
            _resolvedAlterations.Sort(ResolvedAlterationComparer.Instance);
        }

        private void ProcessInputs(uint serverTick, IAuthoritativePlayerInputSink inputSink)
        {
            for (int i = 0; i < _resolvedInputs.Count; i++)
            {
                ResolvedInput resolved = _resolvedInputs[i];
                C_PlayerInput input = resolved.Queued.Input;

                if (!IsNewInputSequence(resolved.PlayerId, input.sequence))
                {
                    StaleOrDuplicateCommands++;
                    continue;
                }

                inputSink.ApplyInput(resolved.PlayerId, in input, serverTick);
                _lastProcessedInputSequence[resolved.PlayerId] = input.sequence;
            }
        }

        private void ProcessAlterations(
            uint serverTick,
            IRegionReadSource readStorage,
            IRegionMutationStore mutationStorage,
            in ProtectedZones zones,
            IAlterationApplier applier,
            IAuthoritativeAlterationPublisher publisher,
            IAlterationRejectionSink rejectionSink)
        {
            ushort nextAuthoritativeSequence = 1;

            for (int i = 0; i < _resolvedAlterations.Count; i++)
            {
                ResolvedAlteration resolved = _resolvedAlterations[i];
                C_AlterationRequest request = resolved.Queued.Request;
                ServerPlayerRegistry.PlayerSession player = resolved.Player;
                int estimatedBricks = EstimateForBudget(in request);
                Validation.ValidationResult validation;

                if (_rateLimiter.WouldExceedRate(player.PlayerId, serverTick))
                {
                    validation = Validation.ValidationResult.TooFast;
                }
                else if (_rateLimiter.WouldExceedAllocation(player.PlayerId, serverTick, estimatedBricks))
                {
                    validation = Validation.ValidationResult.OverBudget;
                }
                else
                {
                    uint authoritativeSeed = DeriveSeed(serverTick, player.PlayerId, nextAuthoritativeSequence);
                    AlterationEvent evt = request.ToAuthoritativeEvent(
                        serverTick,
                        player.PlayerId,
                        nextAuthoritativeSequence,
                        authoritativeSeed);

                    validation = AuthoritativeAlterationValidator.Validate(
                        in evt,
                        in player,
                        _players,
                        readStorage,
                        mutationStorage,
                        applier,
                        _densityCap,
                        in zones);

                    if (validation == Validation.ValidationResult.Success)
                    {
                        bool changed = applier.TryApply(
                            mutationStorage, in evt, out NativeList<Unity.Mathematics.int3> affectedBlocks);
                        if (affectedBlocks.IsCreated) affectedBlocks.Dispose();
                        if (!changed) validation = Validation.ValidationResult.InvalidTarget;
                    }

                    if (validation == Validation.ValidationResult.Success)
                    {
                        _rateLimiter.CommitAccepted(player.PlayerId, serverTick, estimatedBricks);
                        SessionLifecycle.RecordAlteration();
                        publisher.PublishAlteration(in evt);
                        AcceptedAlterations++;
                        nextAuthoritativeSequence++;
                        continue;
                    }
                }

                RejectedAlterations++;
                var rejection = new S_AlterationRejected(serverTick, player.PlayerId, ToReason(validation));
                rejectionSink.SendAlterationRejected(resolved.Queued.ConnectionId, in rejection);
            }
        }

        private static int EstimateForBudget(in C_AlterationRequest request)
        {
            AlterationEvent evt = request.ToAuthoritativeEvent(1, 1, 1, 1);
            return AuthoritativeAlterationValidator.EstimateAffectedBricks(in evt);
        }

        private bool IsNewDurableSequence(ushort playerId, ushort sequence)
        {
            if (!_lastDurableSequence.TryGetValue(playerId, out ushort last)) return true;
            return IsNewerSequence(sequence, last);
        }

        private bool IsNewInputSequence(ushort playerId, ushort sequence)
        {
            if (!_lastProcessedInputSequence.TryGetValue(playerId, out ushort last)) return true;
            return IsNewerSequence(sequence, last);
        }

        private static bool IsNewerSequence(ushort candidate, ushort last)
        {
            ushort delta = unchecked((ushort)(candidate - last));
            return delta != 0 && delta < 0x8000;
        }

        private static bool IsClientTickPlausible(uint clientTick, uint serverTick)
        {
            uint oldest = serverTick > AuthoritativeTickConfig.RollbackWindowTicks
                ? serverTick - AuthoritativeTickConfig.RollbackWindowTicks
                : 0;
            uint newest = serverTick + MaxFutureInputTicks;
            return clientTick >= oldest && clientTick <= newest;
        }

        private uint DeriveSeed(uint tick, ushort playerId, ushort sequence)
        {
            uint x = _serverSeed ^ 0x9E3779B9u;
            x ^= tick * 0x85EBCA6Bu;
            x ^= (uint)playerId * 0xC2B2AE35u;
            x ^= (uint)sequence * 0x27D4EB2Fu;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x == 0 ? 1u : x;
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

        private readonly struct ResolvedInput
        {
            public readonly ushort PlayerId;
            public readonly ServerCommandInbox.QueuedPlayerInput Queued;
            public ResolvedInput(ushort playerId, ServerCommandInbox.QueuedPlayerInput queued) { PlayerId = playerId; Queued = queued; }
        }

        private readonly struct ResolvedAlteration
        {
            public readonly ServerPlayerRegistry.PlayerSession Player;
            public readonly ServerCommandInbox.QueuedAlterationRequest Queued;
            public ResolvedAlteration(ServerPlayerRegistry.PlayerSession player, ServerCommandInbox.QueuedAlterationRequest queued)
            { Player = player; Queued = queued; }
        }

        private sealed class ResolvedInputComparer : IComparer<ResolvedInput>
        {
            public static readonly ResolvedInputComparer Instance = new ResolvedInputComparer();
            public int Compare(ResolvedInput a, ResolvedInput b)
            {
                int tick = a.Queued.Input.tick.CompareTo(b.Queued.Input.tick);
                if (tick != 0) return tick;
                int player = a.PlayerId.CompareTo(b.PlayerId);
                return player != 0 ? player : CompareSequence(a.Queued.Input.sequence, b.Queued.Input.sequence);
            }
        }

        private sealed class ResolvedAlterationComparer : IComparer<ResolvedAlteration>
        {
            public static readonly ResolvedAlterationComparer Instance = new ResolvedAlterationComparer();
            public int Compare(ResolvedAlteration a, ResolvedAlteration b)
            {
                int player = a.Player.PlayerId.CompareTo(b.Player.PlayerId);
                return player != 0 ? player : CompareSequence(a.Queued.Request.sequence, b.Queued.Request.sequence);
            }
        }

        private static int CompareSequence(ushort a, ushort b)
        {
            if (a == b) return 0;
            ushort delta = unchecked((ushort)(a - b));
            return delta < 0x8000 ? 1 : -1;
        }
    }
}
