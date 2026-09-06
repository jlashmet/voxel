using System;
using System.Collections.Generic;
using NUnit.Framework;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ServerSessionAdmissionInboxTests
    {
        [Test]
        public void AdmissionWithoutAnInstalledConsumerFailsClosed()
        {
            var inbox = new ServerCommandInbox(1, 1);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.False);
            Assert.That(inbox.PendingTotal, Is.Zero);
            Assert.That(inbox.PendingSessionAdmissions, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(SessionAdmissionPacket.MaxPayloadBytes + 1)]
        public void InvalidPayloadCannotConsumeQueueCapacity(int length)
        {
            var inbox = new ServerCommandInbox(1, 1, acceptSessionAdmission: true);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[length]), Is.False);
            Assert.That(inbox.PendingTotal, Is.Zero);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 9 }), Is.True);
        }

        [Test]
        public void ReservedConnectionIdCannotBeQueued()
        {
            var inbox = new ServerCommandInbox(1, 1, acceptSessionAdmission: true);
            Assert.That(inbox.TryEnqueueSessionAdmission(0, new byte[] { 1 }), Is.False);
            Assert.That(inbox.PendingTotal, Is.Zero);
            Assert.That(inbox.DroppedCommands, Is.EqualTo(1));
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 9 }), Is.True);
        }

        [Test]
        public void BorrowedPayloadIsCopiedBeforeTransportScratchCanBeReused()
        {
            var inbox = new ServerCommandInbox(1, 1, acceptSessionAdmission: true);
            var payload = new byte[SessionAdmissionPacket.MaxPayloadBytes];
            payload[0] = 19;
            payload[payload.Length - 1] = 37;
            Assert.That(inbox.TryEnqueueSessionAdmission(7, payload), Is.True);
            Array.Clear(payload, 0, payload.Length);

            var received = new List<ServerCommandInbox.QueuedSessionAdmission>();
            Assert.That(inbox.DrainSessionAdmissions(received), Is.EqualTo(1));
            Assert.That(received[0].ConnectionId, Is.EqualTo(7));
            Assert.That(received[0].Payload.Length, Is.EqualTo(SessionAdmissionPacket.MaxPayloadBytes));
            Assert.That(received[0].Payload[0], Is.EqualTo(19));
            Assert.That(received[0].Payload[received[0].Payload.Length - 1], Is.EqualTo(37));
            Assert.That(inbox.PendingTotal, Is.Zero);
        }

        [Test]
        public void AdmissionSharesPerConnectionBudgetWithGameplayCommands()
        {
            var inbox = new ServerCommandInbox(2, 4, acceptSessionAdmission: true);
            C_PlayerInput input = default;
            C_AlterationRequest alteration = default;
            inbox.HandlePlayerInput(7, in input);
            inbox.HandleAlterationRequest(7, in alteration);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.False);
            Assert.That(inbox.PendingSessionAdmissions, Is.Zero);
            Assert.That(inbox.PendingTotal, Is.EqualTo(2));

            Assert.That(inbox.DrainInputs(new List<ServerCommandInbox.QueuedPlayerInput>()), Is.EqualTo(1));
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.True);
            inbox.HandlePlayerInput(7, in input);
            Assert.That(inbox.PendingInputs, Is.Zero, "Admission also reserves against later gameplay intake.");
            Assert.That(inbox.PendingTotal, Is.EqualTo(2));
            Assert.That(inbox.DroppedCommands, Is.EqualTo(2));
        }

        [Test]
        public void AdmissionSharesGlobalBudgetAcrossConnectionsAndCommandKinds()
        {
            var inbox = new ServerCommandInbox(2, 3, acceptSessionAdmission: true);
            C_PlayerInput input = default;
            C_AlterationRequest alteration = default;
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.True);
            inbox.HandlePlayerInput(7, in input);
            inbox.HandleAlterationRequest(8, in alteration);
            Assert.That(inbox.TryEnqueueSessionAdmission(8, new byte[] { 2 }), Is.False);
            Assert.That(inbox.PendingTotal, Is.EqualTo(3));
            Assert.That(inbox.DroppedCommands, Is.EqualTo(1));

            inbox.DrainInputs(new List<ServerCommandInbox.QueuedPlayerInput>());
            Assert.That(inbox.TryEnqueueSessionAdmission(8, new byte[] { 2 }), Is.True);
            Assert.That(inbox.PendingTotal, Is.EqualTo(3));
            Assert.That(inbox.PendingSessionAdmissions, Is.EqualTo(2));
        }

        [Test]
        public void DrainPreservesAttributionAndArrivalOrderAndReleasesOnlyItsReservations()
        {
            var inbox = new ServerCommandInbox(4, 4, acceptSessionAdmission: true);
            C_PlayerInput input = default;
            C_AlterationRequest alteration = default;
            inbox.HandlePlayerInput(7, in input);
            Assert.That(inbox.TryEnqueueSessionAdmission(8, new byte[] { 1 }), Is.True);
            inbox.HandleAlterationRequest(7, in alteration);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 2 }), Is.True);
            var admissions = new List<ServerCommandInbox.QueuedSessionAdmission>();
            var inputs = new List<ServerCommandInbox.QueuedPlayerInput>();
            var alterations = new List<ServerCommandInbox.QueuedAlterationRequest>();

            Assert.That(inbox.DrainSessionAdmissions(admissions), Is.EqualTo(2));
            Assert.That(inbox.PendingTotal, Is.EqualTo(2));
            Assert.That(inbox.PendingSessionAdmissions, Is.Zero);
            inbox.DrainInputs(inputs);
            inbox.DrainAlterations(alterations);
            Assert.That(admissions[0].ConnectionId, Is.EqualTo(8));
            Assert.That(admissions[1].ConnectionId, Is.EqualTo(7));
            Assert.That(inputs[0].ArrivalOrdinal, Is.LessThan(admissions[0].ArrivalOrdinal));
            Assert.That(admissions[0].ArrivalOrdinal, Is.LessThan(alterations[0].ArrivalOrdinal));
            Assert.That(alterations[0].ArrivalOrdinal, Is.LessThan(admissions[1].ArrivalOrdinal));
            Assert.That(inbox.PendingTotal, Is.Zero);
            Assert.That(inbox.DrainSessionAdmissions(admissions), Is.Zero);
            Assert.That(admissions.Count, Is.EqualTo(2), "Previously consumed payloads cannot be replayed.");
        }

        [Test]
        public void DisconnectDropsAllDeadSenderCommandsWithoutDisturbingAnotherSender()
        {
            var inbox = new ServerCommandInbox(3, 4, acceptSessionAdmission: true);
            C_PlayerInput input = default;
            C_AlterationRequest alteration = default;
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.True);
            inbox.HandlePlayerInput(7, in input);
            inbox.HandleAlterationRequest(7, in alteration);
            Assert.That(inbox.TryEnqueueSessionAdmission(8, new byte[] { 2 }), Is.True);

            Assert.That(inbox.RemoveConnection(7), Is.EqualTo(3));
            Assert.That(inbox.RemoveConnection(7), Is.Zero);
            Assert.That(inbox.PendingTotal, Is.EqualTo(1));
            Assert.That(inbox.PendingInputs, Is.Zero);
            Assert.That(inbox.PendingAlterations, Is.Zero);
            var received = new List<ServerCommandInbox.QueuedSessionAdmission>();
            Assert.That(inbox.DrainSessionAdmissions(received), Is.EqualTo(1));
            Assert.That(received[0].ConnectionId, Is.EqualTo(8));
            Assert.That(received[0].Payload[0], Is.EqualTo(2));
            Assert.That(inbox.PendingTotal, Is.Zero);
            Assert.That(inbox.TryEnqueueSessionAdmission(9, new byte[] { 3 }), Is.True);
        }

        [Test]
        public void ClearReleasesAdmissionCapacityAlongsideExistingCommands()
        {
            var inbox = new ServerCommandInbox(2, 2, acceptSessionAdmission: true);
            C_PlayerInput input = default;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.True);
                inbox.HandlePlayerInput(7, in input);
                Assert.That(inbox.PendingTotal, Is.EqualTo(2));
                inbox.Clear();
                Assert.That(inbox.PendingTotal, Is.Zero);
                Assert.That(inbox.PendingSessionAdmissions, Is.Zero);
                Assert.That(inbox.PendingInputs, Is.Zero);
            }
        }

        [Test]
        public void InvalidDrainDestinationDoesNotConsumePendingAdmission()
        {
            var inbox = new ServerCommandInbox(1, 1, acceptSessionAdmission: true);
            Assert.That(inbox.TryEnqueueSessionAdmission(7, new byte[] { 1 }), Is.True);
            Assert.Throws<ArgumentNullException>(() => inbox.DrainSessionAdmissions(null));
            Assert.That(inbox.PendingSessionAdmissions, Is.EqualTo(1));
            Assert.That(inbox.PendingTotal, Is.EqualTo(1));
        }
    }
}
