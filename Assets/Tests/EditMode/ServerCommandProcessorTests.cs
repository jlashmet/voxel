using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ServerCommandProcessorTests
    {
        [Test]
        public void CrossPlayerArbitrationUsesAuthenticatedPlayerThenClientSequenceNotArrival()
        {
            var harness = new Harness();
            Assert.That(harness.Players.TryRegisterAuthenticated(10, 2, int3.zero, 256), Is.True);
            Assert.That(harness.Players.TryRegisterAuthenticated(20, 1, int3.zero, 256), Is.True);

            // Player 2 arrives first on the socket; player 1 must still arbitrate first.
            harness.Inbox.HandleAlterationRequest(10, Request(100, 1, 0xAAAAAAAA));
            harness.Inbox.HandleAlterationRequest(20, Request(100, 9, 0xBBBBBBBB));
            harness.Process(100);

            Assert.That(harness.Publisher.Events.Count, Is.EqualTo(2));
            Assert.That(harness.Publisher.Events[0].playerId, Is.EqualTo(1));
            Assert.That(harness.Publisher.Events[0].sequence, Is.EqualTo(1));
            Assert.That(harness.Publisher.Events[1].playerId, Is.EqualTo(2));
            Assert.That(harness.Publisher.Events[1].sequence, Is.EqualTo(2));

            // Client requested seeds are never authoritative.
            Assert.That(harness.Publisher.Events[0].seed, Is.Not.EqualTo(0xBBBBBBBBu));
            Assert.That(harness.Publisher.Events[1].seed, Is.Not.EqualTo(0xAAAAAAAAu));
            Assert.That(harness.Publisher.Events[0].tick, Is.EqualTo(100));
            Assert.That(harness.Applier.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void UnauthenticatedConnectionNeverReachesWorldApplier()
        {
            var harness = new Harness();
            harness.Inbox.HandleAlterationRequest(999, Request(10, 1, 7));
            harness.Process(10);

            Assert.That(harness.Applier.ApplyCount, Is.Zero);
            Assert.That(harness.Publisher.Events, Is.Empty);
            Assert.That(harness.Processor.UnauthenticatedCommands, Is.EqualTo(1));
        }

        [Test]
        public void AuthoritativeReachRejectsClientTarget()
        {
            var harness = new Harness();
            Assert.That(harness.Players.TryRegisterAuthenticated(1, 1, int3.zero, reachVoxels: 8), Is.True);
            harness.Inbox.HandleAlterationRequest(1, Request(20, 1, 123, new int3(100, 0, 0)));
            harness.Process(20);

            Assert.That(harness.Applier.ApplyCount, Is.Zero);
            Assert.That(harness.Rejections.Items.Count, Is.EqualTo(1));
            Assert.That(harness.Rejections.Items[0].Rejection.ReasonEnum(),
                Is.EqualTo(S_AlterationRejected.Reason.OutOfReach));
        }

        [Test]
        public void ReplayedDurableSequenceIsNotAppliedTwice()
        {
            var harness = new Harness();
            Assert.That(harness.Players.TryRegisterAuthenticated(1, 1, int3.zero, 256), Is.True);

            var request = Request(30, 7, 123);
            harness.Inbox.HandleAlterationRequest(1, request);
            harness.Process(30);
            Assert.That(harness.Applier.ApplyCount, Is.EqualTo(1));

            // Same durable command sequence, later network frame/tick.
            request.tick = 31;
            harness.Inbox.HandleAlterationRequest(1, request);
            harness.Process(31);

            Assert.That(harness.Applier.ApplyCount, Is.EqualTo(1));
            Assert.That(harness.Processor.StaleOrDuplicateCommands, Is.EqualTo(1));
        }

        [Test]
        public void EleventhAcceptedAlterationInsideOneSecondIsRateLimited()
        {
            var harness = new Harness();
            Assert.That(harness.Players.TryRegisterAuthenticated(1, 1, int3.zero, 256), Is.True);

            for (ushort sequence = 1; sequence <= 11; sequence++)
                harness.Inbox.HandleAlterationRequest(1, Request(60, sequence, sequence));

            harness.Process(60);

            Assert.That(harness.Applier.ApplyCount, Is.EqualTo(10));
            Assert.That(harness.Publisher.Events.Count, Is.EqualTo(10));
            Assert.That(harness.Rejections.Items.Count, Is.EqualTo(1));
            Assert.That(harness.Rejections.Items[0].Rejection.ReasonEnum(),
                Is.EqualTo(S_AlterationRejected.Reason.TooFast));
        }

        [Test]
        public void EphemeralInputUsesConnectionOwnedIdentity()
        {
            var harness = new Harness();
            Assert.That(harness.Players.TryRegisterAuthenticated(44, 12, int3.zero, 256), Is.True);

            var input = new C_PlayerInput(
                tick: 70,
                sequence: 5,
                movement: new float2(1f, 0f),
                viewDirection: new float3(0f, 0f, 1f),
                actions: C_PlayerInput.ActionBits.Move,
                toolMaterial: 0);

            harness.Inbox.HandlePlayerInput(44, input);
            harness.Process(70);

            Assert.That(harness.Inputs.PlayerIds.Count, Is.EqualTo(1));
            Assert.That(harness.Inputs.PlayerIds[0], Is.EqualTo(12));
            Assert.That(harness.Inputs.Inputs[0], Is.EqualTo(input));
        }

        private static C_AlterationRequest Request(
            uint tick,
            ushort sequence,
            uint requestedSeed,
            int3 origin = default)
        {
            return new C_AlterationRequest(
                tick,
                origin,
                AlterationEvent.KindExplosion,
                material: 0,
                shapeKind: AlterationEvent.KindExplosion,
                shapeData: 1,
                seed: requestedSeed,
                sequence: sequence);
        }

        private sealed class Harness
        {
            public readonly ServerCommandInbox Inbox = new ServerCommandInbox();
            public readonly ServerPlayerRegistry Players = new ServerPlayerRegistry();
            public readonly AlterationRateLimiter Limiter = new AlterationRateLimiter();
            public readonly RecordingInputSink Inputs = new RecordingInputSink();
            public readonly RecordingApplier Applier = new RecordingApplier();
            public readonly RecordingPublisher Publisher = new RecordingPublisher();
            public readonly RecordingRejectionSink Rejections = new RecordingRejectionSink();
            public readonly ServerCommandProcessor Processor;

            private RegionTable _table;
            private BrickPool _pool;
            private ProtectedZones _zones;

            public Harness()
            {
                Processor = new ServerCommandProcessor(
                    Inbox,
                    Players,
                    Limiter,
                    serverSeed: 0xC0FFEEu,
                    densityCap: new Validation.DensityCap(1f, 0));
            }

            public void Process(uint tick)
            {
                Processor.ProcessTick(
                    tick,
                    ref _table,
                    ref _pool,
                    in _zones,
                    Inputs,
                    Applier,
                    Publisher,
                    Rejections);
            }
        }

        private sealed class RecordingInputSink : IAuthoritativePlayerInputSink
        {
            public readonly List<ushort> PlayerIds = new List<ushort>();
            public readonly List<C_PlayerInput> Inputs = new List<C_PlayerInput>();

            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick)
            {
                PlayerIds.Add(playerId);
                Inputs.Add(input);
            }
        }

        private sealed class RecordingApplier : IAuthoritativeAlterationApplier
        {
            public int ApplyCount { get; private set; }

            public bool TryApplyAlteration(ref RegionTable table, ref BrickPool pool, in AlterationEvent evt)
            {
                ApplyCount++;
                return true;
            }
        }

        private sealed class RecordingPublisher : IAuthoritativeAlterationPublisher
        {
            public readonly List<AlterationEvent> Events = new List<AlterationEvent>();
            public void PublishAlteration(in AlterationEvent evt) => Events.Add(evt);
        }

        private sealed class RecordingRejectionSink : IAlterationRejectionSink
        {
            public readonly List<Item> Items = new List<Item>();

            public void SendAlterationRejected(uint connectionId, in S_AlterationRejected rejection) =>
                Items.Add(new Item(connectionId, rejection));

            public readonly struct Item
            {
                public readonly uint ConnectionId;
                public readonly S_AlterationRejected Rejection;

                public Item(uint connectionId, S_AlterationRejected rejection)
                {
                    ConnectionId = connectionId;
                    Rejection = rejection;
                }
            }
        }
    }
}
