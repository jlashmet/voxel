using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ServerCommandInboxTests
    {
        [Test]
        public void DrainPreservesConnectionAndArrivalMetadata()
        {
            var inbox = new ServerCommandInbox();
            var alterations = new List<ServerCommandInbox.QueuedAlterationRequest>();
            var inputs = new List<ServerCommandInbox.QueuedPlayerInput>();

            var input = Input(5);
            var request = Request(9);
            inbox.HandlePlayerInput(7, in input);
            inbox.HandleAlterationRequest(11, in request);

            Assert.That(inbox.PendingTotal, Is.EqualTo(2));
            Assert.That(inbox.DrainInputs(inputs), Is.EqualTo(1));
            Assert.That(inbox.DrainAlterations(alterations), Is.EqualTo(1));
            Assert.That(inbox.PendingTotal, Is.EqualTo(0));

            Assert.That(inputs[0].ConnectionId, Is.EqualTo(7));
            Assert.That(alterations[0].ConnectionId, Is.EqualTo(11));
            Assert.That(inputs[0].ArrivalOrdinal, Is.LessThan(alterations[0].ArrivalOrdinal));
            Assert.That(inputs[0].Input, Is.EqualTo(input));
            Assert.That(alterations[0].Request, Is.EqualTo(request));
        }

        [Test]
        public void PerConnectionAndGlobalCapsDropBeforeQueueGrowth()
        {
            var inbox = new ServerCommandInbox(maxPendingPerConnection: 2, maxPendingTotal: 3);

            var a = Input(1);
            var b = Input(2);
            var c = Input(3);
            var d = Input(4);

            inbox.HandlePlayerInput(1, in a);
            inbox.HandlePlayerInput(1, in b);
            inbox.HandlePlayerInput(1, in c); // per-connection drop
            inbox.HandlePlayerInput(2, in c); // reaches global total 3
            inbox.HandlePlayerInput(3, in d); // global drop

            Assert.That(inbox.PendingTotal, Is.EqualTo(3));
            Assert.That(inbox.DroppedCommands, Is.EqualTo(2));
        }

        [Test]
        public void RemoveConnectionDropsOnlyThatPeersUnvalidatedIntent()
        {
            var inbox = new ServerCommandInbox();
            var input1 = Input(1);
            var input2 = Input(2);
            var request1 = Request(1);
            var request2 = Request(2);

            inbox.HandlePlayerInput(1, in input1);
            inbox.HandleAlterationRequest(1, in request1);
            inbox.HandlePlayerInput(2, in input2);
            inbox.HandleAlterationRequest(2, in request2);

            Assert.That(inbox.RemoveConnection(1), Is.EqualTo(2));
            Assert.That(inbox.PendingTotal, Is.EqualTo(2));

            var inputs = new List<ServerCommandInbox.QueuedPlayerInput>();
            var alterations = new List<ServerCommandInbox.QueuedAlterationRequest>();
            inbox.DrainInputs(inputs);
            inbox.DrainAlterations(alterations);

            Assert.That(inputs.Count, Is.EqualTo(1));
            Assert.That(alterations.Count, Is.EqualTo(1));
            Assert.That(inputs[0].ConnectionId, Is.EqualTo(2));
            Assert.That(alterations[0].ConnectionId, Is.EqualTo(2));
        }

        private static C_PlayerInput Input(ushort sequence) =>
            new C_PlayerInput(
                tick: sequence,
                sequence: sequence,
                movement: new float2(0.5f, 0f),
                viewDirection: new float3(0f, 0f, 1f),
                actions: C_PlayerInput.ActionBits.Move,
                toolMaterial: 0);

        private static C_AlterationRequest Request(ushort sequence) =>
            new C_AlterationRequest(
                tick: sequence,
                origin: int3.zero,
                eventKind: AlterationEvent.KindExplosion,
                material: 0,
                shapeKind: AlterationEvent.KindExplosion,
                shapeData: 1,
                seed: sequence,
                sequence: sequence);
    }
}
