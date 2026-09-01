using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Interest;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Net.Runtime.Transport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class EventDrivenReplicationTests
    {
        [Test]
        public void AuthoritativeStreamSealsInServerArbitrationOrder()
        {
            var stream = new AuthoritativeEventStream();
            stream.BeginTick(100);
            stream.Publish(Explosion(100, new int3(10), 2, 1));
            stream.Publish(Explosion(100, new int3(20), 1, 5));
            stream.Publish(Explosion(100, new int3(30), 1, 2));

            var sealedEvents = stream.SealTick();

            Assert.That(sealedEvents.Count, Is.EqualTo(3));
            Assert.That(sealedEvents[0].playerId, Is.EqualTo(1));
            Assert.That(sealedEvents[0].sequence, Is.EqualTo(2));
            Assert.That(sealedEvents[1].playerId, Is.EqualTo(1));
            Assert.That(sealedEvents[1].sequence, Is.EqualTo(5));
            Assert.That(sealedEvents[2].playerId, Is.EqualTo(2));
        }

        [Test]
        public void SimulationInterestIsThreeDimensionalAndUsesFloorRegionCoordinates()
        {
            var regions = new List<int3>();
            SimulationInterest.CollectLoadRegions(new int3(10, 10, 10), regions);

            Assert.That(regions.Contains(new int3(0, 1, 0)), Is.True,
                "Vertical regions must participate in simulation interest for caves and mountains.");
            Assert.That(regions.Contains(new int3(0, -1, 0)), Is.True);
            Assert.That(SimulationInterest.WorldVoxelToRegion(new int3(-1, -1, -1)),
                Is.EqualTo(new int3(-1, -1, -1)));
        }

        [Test]
        public void CrossRegionEventFansOutOncePerConnection()
        {
            var pipeline = new EventDrivenReplicationPipeline();
            var sink = new RecordingSink();
            var region0 = new int3(0, 0, 0);
            var region1 = new int3(1, 0, 0);

            pipeline.SetSubscriptions(10, new[] { region0 });
            pipeline.SetSubscriptions(20, new[] { region1 });
            pipeline.SetSubscriptions(30, new[] { region0, region1 });

            pipeline.BeginTick(7);
            pipeline.PublishAlteration(Explosion(7, new int3(511, 64, 64), 1, 1, radiusBricks: 2));
            pipeline.Flush(sink);

            Assert.That(sink.BatchCountFor(10), Is.EqualTo(1));
            Assert.That(sink.BatchCountFor(20), Is.EqualTo(1));
            Assert.That(sink.BatchCountFor(30), Is.EqualTo(1),
                "A connection interested in both impacted regions must not receive the same event twice.");
        }

        [Test]
        public void RouterSplitsAtCompactBatchLimitWithoutChangingOrder()
        {
            var pipeline = new EventDrivenReplicationPipeline(64);
            var sink = new RecordingSink();
            pipeline.SetSubscriptions(42, new[] { new int3(0, 0, 0) });
            pipeline.BeginTick(9);

            for (ushort i = 0; i < 50; i++)
                pipeline.PublishAlteration(Explosion(9, new int3(32 + i, 64, 64), 1, i, radiusBricks: 1));

            pipeline.Flush(sink);

            var batches = sink.BatchesFor(42);
            Assert.That(batches.Count, Is.EqualTo(2));
            Assert.That(batches[0].Events.Length, Is.EqualTo(S_AlterationEventBatch.MaxEventsPerBatch));
            Assert.That(batches[1].Events.Length, Is.EqualTo(2));

            ushort expected = 0;
            for (int b = 0; b < batches.Count; b++)
            {
                for (int i = 0; i < batches[b].Events.Length; i++)
                    Assert.That(batches[b].Events[i].sequence, Is.EqualTo(expected++));
            }
        }

        [Test]
        public void ClientReceiverPreservesWireOrder()
        {
            const uint tick = 17;
            var region = new int3(2, 0, -1);
            int3 origin = region << VoxelDimensions.RegionVoxelEdgeLog2;
            var source = new[]
            {
                Explosion(tick, origin + new int3(10, 20, 30), 1, 3),
                Explosion(tick, origin + new int3(11, 20, 30), 1, 4),
            };

            Span<byte> payload = stackalloc byte[S_AlterationEventBatch.EncodedSize(source.Length)];
            Assert.That(S_AlterationEventBatch.TryEncode(payload, region, tick, source, out _), Is.True);
            Assert.That(AlterationBatchReceiver.TryDecode(payload, Allocator.Temp, out var decoded), Is.True);

            try
            {
                Assert.That(decoded.Length, Is.EqualTo(2));
                Assert.That(decoded[0].sequence, Is.EqualTo(3));
                Assert.That(decoded[1].sequence, Is.EqualTo(4));
            }
            finally
            {
                decoded.Dispose();
            }
        }

        [Test]
        [Category("Bandwidth")]
        public void PacketSinkFramesMaxBatchBelowLiveEventCeiling()
        {
            const uint tick = 21;
            var region = new int3(4, 1, -2);
            int3 origin = region << VoxelDimensions.RegionVoxelEdgeLog2;
            var events = new AlterationEvent[S_AlterationEventBatch.MaxEventsPerBatch];
            for (ushort i = 0; i < events.Length; i++)
                events[i] = Explosion(tick, origin + new int3(i, 10, 20), 1, i);

            var sender = new RecordingPacketSender();
            var sink = new AlterationBatchPacketSink(sender);
            sink.SendBatch(77, region, tick, events);

            Assert.That(sender.ConnectionId, Is.EqualTo(77));
            Assert.That(sender.Packet.Length, Is.EqualTo(AlterationBatchPacketSink.MaxPacketBytes));
            Assert.That(sender.Packet.Length, Is.LessThanOrEqualTo(ChannelSetup.k_MaxEventPacketBytes));
            Assert.That(ProtocolEnvelope.TryReadHeader(sender.Packet, out var kind, out int payloadOffset), Is.True);
            Assert.That(kind, Is.EqualTo(ProtocolMessageKind.S_AlterationEventBatch));
            Assert.That(S_AlterationEventBatch.TryDecodeHeader(
                sender.Packet.AsSpan(payloadOffset), out var header), Is.True);
            Assert.That(header.count, Is.EqualTo(S_AlterationEventBatch.MaxEventsPerBatch));
        }

        [Test]
        public void ProtocolEnvelopeRejectsUnknownVersionAndKind()
        {
            byte[] wrongVersion = { (byte)(ProtocolEnvelope.CurrentVersion + 1), (byte)ProtocolMessageKind.S_AlterationEventBatch };
            Assert.That(ProtocolEnvelope.TryReadHeader(wrongVersion, out _, out _), Is.False);

            byte[] unknownKind = { ProtocolEnvelope.CurrentVersion, 255 };
            Assert.That(ProtocolEnvelope.TryReadHeader(unknownKind, out _, out _), Is.False);
        }

        private static AlterationEvent Explosion(
            uint tick,
            int3 origin,
            ushort playerId,
            ushort sequence,
            ushort radiusBricks = 1)
        {
            return new AlterationEvent(
                AlterationEvent.KindExplosion,
                tick,
                origin,
                radiusBricks,
                0,
                0x1000u + sequence,
                playerId,
                sequence);
        }

        private sealed class RecordingSink : IAlterationReplicationSink
        {
            private readonly List<SentBatch> _batches = new List<SentBatch>();

            public void SendBatch(uint connectionId, int3 encodingRegion, uint tick, ReadOnlySpan<AlterationEvent> events)
            {
                var copy = new AlterationEvent[events.Length];
                for (int i = 0; i < events.Length; i++)
                    copy[i] = events[i];

                _batches.Add(new SentBatch(connectionId, encodingRegion, tick, copy));
            }

            public int BatchCountFor(uint connectionId)
            {
                int count = 0;
                for (int i = 0; i < _batches.Count; i++)
                {
                    if (_batches[i].ConnectionId == connectionId)
                        count++;
                }

                return count;
            }

            public List<SentBatch> BatchesFor(uint connectionId)
            {
                var result = new List<SentBatch>();
                for (int i = 0; i < _batches.Count; i++)
                {
                    if (_batches[i].ConnectionId == connectionId)
                        result.Add(_batches[i]);
                }

                return result;
            }
        }

        private sealed class RecordingPacketSender : IEventPacketSender
        {
            public uint ConnectionId { get; private set; }
            public byte[] Packet { get; private set; }

            public void SendEventPacket(uint connectionId, ReadOnlySpan<byte> packet)
            {
                ConnectionId = connectionId;
                Packet = packet.ToArray();
            }
        }

        private readonly struct SentBatch
        {
            public readonly uint ConnectionId;
            public readonly int3 EncodingRegion;
            public readonly uint Tick;
            public readonly AlterationEvent[] Events;

            public SentBatch(uint connectionId, int3 encodingRegion, uint tick, AlterationEvent[] events)
            {
                ConnectionId = connectionId;
                EncodingRegion = encodingRegion;
                Tick = tick;
                Events = events;
            }
        }
    }
}
