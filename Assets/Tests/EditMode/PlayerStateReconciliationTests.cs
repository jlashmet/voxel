using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Interest;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Net.Runtime.Transport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlayerStateReconciliationTests
    {
        [Test]
        public void CanonicalPlayerStateRoundTripsAbsoluteStateAndAck()
        {
            S_PlayerState source = S_PlayerState.Create(
                playerId: 7,
                serverTick: 100,
                stateSequence: 9,
                positionVoxels: new float3(-123.125f, 42.5f, 900.75f),
                velocityVoxelsPerSecond: new float3(12.25f, -3.5f, 0.125f),
                viewYaw: 50000,
                stateFlags: S_PlayerState.StateFlags.Grounded,
                hasInputAck: true,
                ackInputSequence: 65535);

            Span<byte> bytes = stackalloc byte[S_PlayerState.WireSize];
            source.Encode(bytes);

            Assert.That(S_PlayerState.TryDecode(bytes, out S_PlayerState decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(source));
            Assert.That(decoded.HasInputAck, Is.True);
            Assert.That(decoded.ackInputSequence, Is.EqualTo(65535));
            Assert.That(math.distance(decoded.PositionVoxels(), new float3(-123.125f, 42.5f, 900.75f)), Is.LessThan(0.0002f));
            Assert.That(math.distance(decoded.VelocityVoxelsPerSecond(), new float3(12.25f, -3.5f, 0.125f)), Is.LessThan(0.00001f));
        }

        [Test]
        public void SixStateBundleFitsEphemeralCeilingAndRejectsTrailingBytes()
        {
            Span<S_PlayerState> states = stackalloc S_PlayerState[PlayerStateBundlePacket.MaxStates];
            for (ushort i = 0; i < states.Length; i++)
            {
                states[i] = S_PlayerState.Create(
                    (ushort)(i + 1), 200, (ushort)(20 + i),
                    new float3(i, i * 2, -i),
                    float3.zero,
                    (ushort)(1000 + i),
                    S_PlayerState.StateFlags.None,
                    false,
                    0);
            }

            Span<byte> packet = stackalloc byte[PlayerStateBundlePacket.MaxPacketSize];
            Assert.That(PlayerStateBundlePacket.TryEncode(packet, states, out int written), Is.True);
            Assert.That(written, Is.EqualTo(243));
            Assert.That(written, Is.LessThanOrEqualTo(ChannelSetup.k_MaxEphemeralPacketBytes));

            Span<S_PlayerState> decoded = stackalloc S_PlayerState[PlayerStateBundlePacket.MaxStates];
            Assert.That(PlayerStateBundlePacket.TryDecode(packet.Slice(0, written), decoded, out int count), Is.True);
            Assert.That(count, Is.EqualTo(states.Length));
            for (int i = 0; i < count; i++)
                Assert.That(decoded[i], Is.EqualTo(states[i]));

            byte[] trailing = new byte[written + 1];
            packet.Slice(0, written).CopyTo(trailing);
            Assert.That(PlayerStateBundlePacket.TryDecode(trailing, decoded, out _), Is.False);
        }

        [Test]
        public void ReconciliationDropsAcknowledgedInputsAcrossUshortWrapAndReplaysNewerOnes()
        {
            var reconciler = new ClientPredictionReconciler(16);
            reconciler.RecordSentInput(Input(10, 65534));
            reconciler.RecordSentInput(Input(11, 65535));
            reconciler.RecordSentInput(Input(12, 0));
            reconciler.RecordSentInput(Input(13, 1));

            S_PlayerState state = S_PlayerState.Create(
                1, 50, 1,
                new float3(10f, 2f, 3f),
                new float3(1f, 0f, 0f),
                123,
                S_PlayerState.StateFlags.Grounded,
                true,
                65535);

            var adapter = new RecordingPredictionAdapter();
            int replayed = reconciler.Reconcile(in state, adapter);

            Assert.That(replayed, Is.EqualTo(2));
            Assert.That(reconciler.Count, Is.EqualTo(2));
            Assert.That(adapter.Applied.playerId, Is.EqualTo(1));
            Assert.That(adapter.Replayed, Is.EqualTo(new ushort[] { 0, 1 }));
        }

        [Test]
        public void RemoteTimelineRejectsStaleSequenceAndInterpolatesShortestYawPath()
        {
            var timeline = new ClientPlayerStateTimeline();
            S_PlayerState a = S_PlayerState.Create(
                2, 100, 65535,
                float3.zero,
                new float3(2f, 0f, 0f),
                65000,
                S_PlayerState.StateFlags.None,
                false,
                0);
            S_PlayerState b = S_PlayerState.Create(
                2, 102, 0,
                new float3(10f, 0f, 0f),
                new float3(4f, 0f, 0f),
                500,
                S_PlayerState.StateFlags.None,
                false,
                0);

            Assert.That(timeline.TryAccept(in a), Is.True);
            Assert.That(timeline.TryAccept(in b), Is.True);
            Assert.That(timeline.TryAccept(in a), Is.False);
            Assert.That(timeline.TrySample(2, 0.5f, out RemotePlayerSample sample), Is.True);
            Assert.That(sample.PositionVoxels.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(sample.VelocityVoxelsPerSecond.x, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void ReplicatorBundlesInterestedRemotePlayersAndAlwaysIncludesOwner()
        {
            var players = new ServerPlayerRegistry();
            Assert.That(players.TryRegisterAuthenticated(10, 1, new int3(10, 10, 10), 256), Is.True);
            Assert.That(players.TryRegisterAuthenticated(20, 2, new int3(20, 10, 10), 256), Is.True);
            Assert.That(players.TryRegisterAuthenticated(30, 3, new int3(30, 10, 10), 256), Is.True);
            Assert.That(players.TryRegisterAuthenticated(40, 4, new int3(40, 10, 10), 256), Is.True);

            players.UpdateAuthoritativeKinematics(10, new float3(10.25f, 10, 10), new float3(1, 0, 0), 100, S_PlayerState.StateFlags.Grounded);

            var subscriptions = new RegionSubscriptionIndex();
            int3 region = SimulationInterest.WorldVoxelToRegion(new int3(10, 10, 10));
            subscriptions.SetSubscriptions(99, new[] { region });

            var acks = new RecordingAckSource();
            acks.Values[1] = 77;
            var sink = new RecordingBundleSink();
            var replicator = new ServerPlayerStateReplicator(players, acks, intervalTicks: 2);

            replicator.Emit(2, subscriptions, sink);

            Assert.That(sink.ByConnection.ContainsKey(99), Is.True);
            Assert.That(sink.ByConnection[99].Count, Is.EqualTo(4));
            Assert.That(sink.ByConnection[99][0].tick, Is.EqualTo(2));
            Assert.That(sink.ByConnection[99].Find(s => s.playerId == 1).ackInputSequence, Is.EqualTo(77));
            Assert.That(sink.ByConnection[99].Find(s => s.playerId == 1).HasInputAck, Is.True);

            Assert.That(sink.ByConnection.ContainsKey(10), Is.True, "Owner must receive reconciliation state even without an explicit test subscription.");
            Assert.That(sink.ByConnection[10].Exists(s => s.playerId == 1), Is.True);
        }

        private static C_PlayerInput Input(uint tick, ushort sequence) => new C_PlayerInput(
            tick,
            sequence,
            new float2(1f, 0f),
            new float3(0f, 0f, 1f),
            C_PlayerInput.ActionBits.Move,
            0);

        private sealed class RecordingPredictionAdapter : IClientPredictionAdapter
        {
            public S_PlayerState Applied;
            public readonly List<ushort> Replayed = new List<ushort>();

            public void ApplyAuthoritativeState(in S_PlayerState state) => Applied = state;
            public void ReplayInput(in C_PlayerInput input) => Replayed.Add(input.sequence);
        }

        private sealed class RecordingAckSource : IProcessedInputAckSource
        {
            public readonly Dictionary<ushort, ushort> Values = new Dictionary<ushort, ushort>();
            public bool TryGetLastProcessedInputSequence(ushort playerId, out ushort sequence) =>
                Values.TryGetValue(playerId, out sequence);
        }

        private sealed class RecordingBundleSink : IPlayerStateBundleSink
        {
            public readonly Dictionary<uint, List<S_PlayerState>> ByConnection =
                new Dictionary<uint, List<S_PlayerState>>();

            public bool SendPlayerStateBundle(uint connectionId, ReadOnlySpan<S_PlayerState> states)
            {
                if (!ByConnection.TryGetValue(connectionId, out List<S_PlayerState> list))
                {
                    list = new List<S_PlayerState>();
                    ByConnection.Add(connectionId, list);
                }

                for (int i = 0; i < states.Length; i++)
                    list.Add(states[i]);
                return true;
            }
        }
    }
}
