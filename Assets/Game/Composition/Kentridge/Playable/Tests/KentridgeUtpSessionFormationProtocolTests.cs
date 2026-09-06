using Game.Composition.Kentridge.Playable;
using Game.Sessions.Api;
using NUnit.Framework;
using VoxelEngine.Net.Runtime.Protocol;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeUtpSessionFormationProtocolTests
    {
        [Test]
        public void JoinRoundTripPreservesSemanticAdmissionFieldsWithinEventBudget()
        {
            var request = new JoinRequest(new GameSessionId("kentridge-live"), "client-a", "v7", "content-42", true);
            var buffer = new byte[SessionAdmissionPacket.MaxPayloadBytes];
            Assert.That(KentridgeSessionAdmissionCodec.TryEncodeJoin(in request, buffer, out int written), Is.True);
            Assert.That(written, Is.GreaterThan(0).And.LessThanOrEqualTo(SessionAdmissionPacket.MaxPayloadBytes));
            Assert.That(KentridgeSessionAdmissionCodec.TryDecodeJoin(buffer.AsSpan(0, written), out JoinRequest decoded), Is.True);
            Assert.That(decoded.SessionId, Is.EqualTo(request.SessionId));
            Assert.That(decoded.ApplicantKey, Is.EqualTo(request.ApplicantKey));
            Assert.That(decoded.ProtocolVersion, Is.EqualTo(request.ProtocolVersion));
            Assert.That(decoded.ContentCompatibilityKey, Is.EqualTo(request.ContentCompatibilityKey));
            Assert.That(decoded.IsJoinInProgress, Is.True);
        }

        [Test]
        public void SuccessReplyCarriesOnlyDurableIdentityAndTransientNetworkPlayerKey()
        {
            SessionFormationResult success = SessionFormationResult.Success(
                new GameSessionId("kentridge-live"), new PartyMemberId("kentridge-live:member:2"));
            var buffer = new byte[SessionAdmissionPacket.MaxPayloadBytes];
            Assert.That(KentridgeSessionAdmissionCodec.TryEncodeReply(success, 2, buffer, out int written), Is.True);
            Assert.That(KentridgeSessionAdmissionCodec.TryDecodeReply(buffer.AsSpan(0, written), out SessionFormationResult decoded, out ushort playerId), Is.True);
            Assert.That(decoded.Succeeded, Is.True);
            Assert.That(decoded.SessionId, Is.EqualTo(success.SessionId));
            Assert.That(decoded.LocalMemberId, Is.EqualTo(success.LocalMemberId));
            Assert.That(playerId, Is.EqualTo(2));
        }

        [Test]
        public void MalformedOrOversizedAdmissionFailsClosed()
        {
            var malformed = new byte[] { 1, 0, 255, 255 };
            Assert.That(KentridgeSessionAdmissionCodec.TryDecodeJoin(malformed, out _), Is.False);
            var huge = new string('x', 300);
            var request = new JoinRequest(new GameSessionId("kentridge-live"), huge, "v7", "content-42");
            var buffer = new byte[SessionAdmissionPacket.MaxPayloadBytes];
            Assert.That(KentridgeSessionAdmissionCodec.TryEncodeJoin(in request, buffer, out _), Is.False);
        }

        [Test]
        public void FailureReplyDoesNotManufactureIdentity()
        {
            SessionFormationResult failure = SessionFormationResult.Reject(SessionFormationFailure.SessionFull, "SessionFull");
            var buffer = new byte[SessionAdmissionPacket.MaxPayloadBytes];
            Assert.That(KentridgeSessionAdmissionCodec.TryEncodeReply(failure, 0, buffer, out int written), Is.True);
            Assert.That(KentridgeSessionAdmissionCodec.TryDecodeReply(buffer.AsSpan(0, written), out SessionFormationResult decoded, out ushort playerId), Is.True);
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.Failure, Is.EqualTo(SessionFormationFailure.SessionFull));
            Assert.That(decoded.LocalMemberId.IsValid, Is.False);
            Assert.That(playerId, Is.Zero);
        }
    }
}
