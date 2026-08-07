using System;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Round-trip encode/decode tests for all protocol message types (T042-T049).
    /// Tests edge cases: min/max values, zero-sized payloads, alignment boundaries.
    /// Pattern follows StorageAllocationTests.cs conventions.
    /// </summary>
    public class ProtocolRoundTripTests
    {
        // -- helper ---------------------------------------------------------------

        // Typed round-trip helpers, one per message type.
        //
        // These were previously a single reflection-based generic. That cannot work here:
        // Encode/Decode take Span<byte>, and a ref struct cannot be boxed into the object[]
        // that MethodInfo.Invoke requires. Typed overloads are also strictly better — they
        // fail at compile time if a signature changes, rather than at run time.

        private static void AssertRoundTrip(C_AlterationRequest item, int wireSize)
        {
            Span<byte> buf = stackalloc byte[wireSize];
            item.Encode(buf);
            var decoded = C_AlterationRequest.Decode(buf);
            Assert.That(decoded, Is.EqualTo(item), "Round-trip failed for C_AlterationRequest");
        }

        private static void AssertRoundTrip(C_PlayerInput item, int wireSize)
        {
            Span<byte> buf = stackalloc byte[wireSize];
            item.Encode(buf);
            var decoded = C_PlayerInput.Decode(buf);
            Assert.That(decoded, Is.EqualTo(item), "Round-trip failed for C_PlayerInput");
        }

        private static void AssertRoundTrip(C_RegionRequest item, int wireSize)
        {
            Span<byte> buf = stackalloc byte[wireSize];
            item.Encode(buf);
            var decoded = C_RegionRequest.Decode(buf);
            Assert.That(decoded, Is.EqualTo(item), "Round-trip failed for C_RegionRequest");
        }

        private static void AssertRoundTrip(S_RegionResponse item, int wireSize)
        {
            Span<byte> buf = stackalloc byte[wireSize];
            item.Encode(buf);
            var decoded = S_RegionResponse.Decode(buf);
            Assert.That(decoded, Is.EqualTo(item), "Round-trip failed for S_RegionResponse");
        }

        // -- T042: C_AlterationRequest ------------------------------------------

        [Test]
        public void T042_AlterationRequest_RoundTrip()
        {
            var msg = new C_AlterationRequest(
                tick: 12345u,
                origin: new int3(100, -50, 200),
                eventKind: 1, // KindExplosion
                shapeRadius: 32,
                shapeExtentsYz: 0,
                material: 42,
                seed: 0xDEADBEEFu,
                playerId: 7,
                sequence: 100);

            AssertRoundTrip(msg, C_AlterationRequest.WireSize);
        }

        [Test]
        public void T042_AlterationRequest_MinMaxValues()
        {
            var msg = new C_AlterationRequest(
                tick: uint.MaxValue,
                origin: new int3(int.MinValue, int.MinValue, int.MinValue),
                eventKind: 3, // KindRawBatch
                shapeRadius: ushort.MaxValue,
                shapeExtentsYz: ushort.MaxValue,
                material: byte.MaxValue,
                seed: uint.MaxValue,
                playerId: ushort.MaxValue,
                sequence: ushort.MaxValue);

            AssertRoundTrip(msg, C_AlterationRequest.WireSize);
        }

        [Test]
        public void T042_AlterationRequest_ZeroValues()
        {
            var msg = new C_AlterationRequest(
                tick: 1u,
                origin: int3.zero,
                eventKind: 1,
                shapeRadius: 0,
                shapeExtentsYz: 0,
                material: 0,
                seed: 0u,
                playerId: 1,
                sequence: 0);

            AssertRoundTrip(msg, C_AlterationRequest.WireSize);
        }

        [Test]
        public void T042_AlterationRequest_BrushShape()
        {
            var msg = new C_AlterationRequest(
                tick: 999u,
                origin: new int3(0, 64, 0),
                eventKind: 2, // KindBrush
                shapeRadius: 8,
                shapeExtentsYz: (ushort)((16 << 8) | 4), // extents y=16, z=4
                material: 10,
                seed: 42u,
                playerId: 3,
                sequence: 5);

            AssertRoundTrip(msg, C_AlterationRequest.WireSize);
        }

        // -- T043: C_PlayerInput ------------------------------------------------

        [Test]
        public void T043_PlayerInput_RoundTrip()
        {
            var msg = new C_PlayerInput(
                tick: 500u,
                playerId: 2,
                sequence: 1,
                position: new float3(10.5f, -3.2f, 7.8f),
                direction: new float3(0.0f, 0.707f, 0.707f),
                (C_PlayerInput.ActionType)3, // UseMain
                toolMaterial: 5);

            AssertRoundTrip(msg, C_PlayerInput.WireSize);
        }

        [Test]
        public void T043_PlayerInput_ZeroAction()
        {
            var msg = new C_PlayerInput(
                tick: 1u,
                playerId: 1,
                sequence: 0,
                position: float3.zero, // near-zero position
                direction: new float3(0, 1, 0),
                (C_PlayerInput.ActionType)0, // None — heartbeat
                toolMaterial: 0);

            AssertRoundTrip(msg, C_PlayerInput.WireSize);
        }

        [Test]
        public void T043_PlayerInput_MaxPosition()
        {
            var msg = new C_PlayerInput(
                tick: uint.MaxValue,
                playerId: ushort.MaxValue,
                sequence: ushort.MaxValue,
                position: new float3(1023f, 1023f, -1023f),
                direction: new float3(1f, 1f, 0f),
                (C_PlayerInput.ActionType)5, // Cancel
                toolMaterial: byte.MaxValue);

            AssertRoundTrip(msg, C_PlayerInput.WireSize);
        }

        // -- T044: C_RegionRequest / S_RegionResponse ---------------------------

        [Test]
        public void T044_RegionRequest_RoundTrip()
        {
            var msg = new C_RegionRequest(new int3(128, 0, -64), 2);
            AssertRoundTrip(msg, C_RegionRequest.WireSize);
        }

        [Test]
        public void T044_RegionRequest_ZeroMipLevel()
        {
            var msg = new C_RegionRequest(int3.zero, 0);
            AssertRoundTrip(msg, C_RegionRequest.WireSize);
        }

        [Test]
        public void T044_RegionResponse_RoundTrip()
        {
            var msg = new S_RegionResponse(true, 5);
            AssertRoundTrip(msg, S_RegionResponse.WireSize);
        }

        [Test]
        public void T044_RegionResponse_NoRegion()
        {
            var msg = new S_RegionResponse(false, 0);
            AssertRoundTrip(msg, S_RegionResponse.WireSize);
        }

        // -- T045: S_AlterationEvent --------------------------------------------

        [Test]
        public void T045_AlterationEventBroadcast_RoundTrip()
        {
            var msg = new S_AlterationEvent(42u, new int3(1, 2, 3));

            // Create a realistic AlterationEvent payload (32 bytes).
            Span<byte> eventPayload = stackalloc byte[32];
            for (int i = 0; i < 32; i++) eventPayload[i] = (byte)i;

            Span<byte> buf = new byte[S_AlterationEvent.HeaderSize + 32];
            msg.Encode(buf, eventPayload);

            // Verify header fields are correct.
            Assert.AreEqual(42u, (uint)buf[0]); // tick low byte
            Assert.AreEqual(1, buf[4]);  // origin.x low byte
            Assert.AreEqual(2, buf[8]);  // origin.y low byte
            Assert.AreEqual(3, buf[12]); // origin.z low byte

            // Verify payload is intact.
            S_AlterationEvent.Decode(buf, out ReadOnlySpan<byte> decodedBytes);
            for (int i = 0; i < 32; i++)
                Assert.AreEqual((byte)i, decodedBytes[i]);
        }

        [Test]
        public void T045_AlterationEvent_ZeroPayload()
        {
            var msg = new S_AlterationEvent(0u, int3.zero);
            Span<byte> buf = new byte[S_AlterationEvent.HeaderSize];
            msg.Encode(buf, ReadOnlySpan<byte>.Empty);

            var decodedMsg = S_AlterationEvent.Decode(buf, out ReadOnlySpan<byte> payload);
            Assert.AreEqual(0u, decodedMsg.tick);
            Assert.AreEqual(0, payload.Length);
        }

        [Test]
        public void T045_AlterationEvent_MaxTick()
        {
            var msg = new S_AlterationEvent(uint.MaxValue, new int3(int.MaxValue, int.MinValue, 0));
            Span<byte> buf = new byte[S_AlterationEvent.HeaderSize + 32];
            Span<byte> payload = stackalloc byte[32];

            msg.Encode(buf, payload);

            var decodedMsg = S_AlterationEvent.Decode(buf, out ReadOnlySpan<byte> _);
            Assert.AreEqual(uint.MaxValue, decodedMsg.tick);
        }

        // -- T046: S_AlterationRejected -----------------------------------------

        [Test]
        public void T046_Rejected_RoundTrip_AllReasons()
        {
            var reasons = new[]
            {
                S_AlterationRejected.Reason.TooFast,
                S_AlterationRejected.Reason.OverBudget,
                S_AlterationRejected.Reason.OverDensity,
                S_AlterationRejected.Reason.NotAttached,
                S_AlterationRejected.Reason.InPlayerVolume,
                S_AlterationRejected.Reason.OutOfReach,
                S_AlterationRejected.Reason.ProtectedZone,
                S_AlterationRejected.Reason.InvalidTarget,
            };

            foreach (var reason in reasons)
            {
                var msg = new S_AlterationRejected(100u, 5, reason);
                Span<byte> buf = stackalloc byte[S_AlterationRejected.WireSize];
                msg.Encode(buf);

                var decoded = S_AlterationRejected.Decode(buf);
                Assert.AreEqual(reason, decoded.ReasonEnum(), $"Failed for reason {reason}");
                Assert.AreEqual(100u, decoded.tick);
                Assert.AreEqual((ushort)5, decoded.playerId);
            }
        }

        [Test]
        public void T046_Rejected_MaxValues()
        {
            var msg = new S_AlterationRejected(uint.MaxValue, ushort.MaxValue, S_AlterationRejected.Reason.InvalidTarget);
            Span<byte> buf = stackalloc byte[S_AlterationRejected.WireSize];
            msg.Encode(buf);

            var decoded = S_AlterationRejected.Decode(buf);
            Assert.AreEqual(uint.MaxValue, decoded.tick);
            Assert.AreEqual(ushort.MaxValue, decoded.playerId);
        }

        // -- T047: S_RegionHash + S_RegionRepair -------------------------------

        [Test]
        public void T047_RegionHash_RoundTrip()
        {
            var msg = new S_RegionHash(new int3(255, -1, 0), 0x1234ABCDu);
            Span<byte> buf = stackalloc byte[S_RegionHash.WireSize];
            msg.Encode(buf);

            var decoded = S_RegionHash.Decode(buf);
            Assert.AreEqual(new int3(255, -1, 0), decoded.regionCoord);
            Assert.AreEqual(0x1234ABCDu, decoded.mipHash);
        }

        [Test]
        public void T047_RegionHash_ZeroValues()
        {
            var msg = new S_RegionHash(int3.zero, 0u);
            Span<byte> buf = stackalloc byte[S_RegionHash.WireSize];
            msg.Encode(buf);

            var decoded = S_RegionHash.Decode(buf);
            Assert.AreEqual(int3.zero, decoded.regionCoord);
            Assert.AreEqual(0u, decoded.mipHash);
        }

        [Test]
        public void T047_RegionRepair_RoundTrip()
        {
            var msg = new S_RegionRepair(new int3(10, 20, 30), 500u);
            Span<byte> data = new byte[64];
            for (int i = 0; i < 64; i++) data[i] = (byte)(i & 0xFF);

            Span<byte> buf = new byte[S_RegionRepair.HeaderSize + 64];
            msg.Encode(buf, data);

            var decodedMsg = S_RegionRepair.Decode(buf, out ReadOnlySpan<byte> decodedData);
            Assert.AreEqual(new int3(10, 20, 30), decodedMsg.regionCoord);
            Assert.AreEqual(500u, decodedMsg.repairStartTick);
            Assert.AreEqual(64, decodedData.Length);

            for (int i = 0; i < 64; i++)
                Assert.AreEqual((byte)(i & 0xFF), decodedData[i]);
        }

        [Test]
        public void T047_RegionRepair_ZeroData()
        {
            var msg = new S_RegionRepair(int3.zero, 0u);
            Span<byte> buf = stackalloc byte[S_RegionRepair.HeaderSize];
            msg.Encode(buf, ReadOnlySpan<byte>.Empty);

            var decodedMsg = S_RegionRepair.Decode(buf, out ReadOnlySpan<byte> decodedData);
            Assert.AreEqual(int3.zero, decodedMsg.regionCoord);
            Assert.AreEqual(0u, decodedMsg.repairStartTick);
            Assert.AreEqual(0, decodedData.Length);
        }

        // -- T048: S_RegionData -------------------------------------------------

        [Test]
        public void T048_RegionData_RoundTrip()
        {
            var msg = new S_RegionData(0xCAFEBABEu);
            msg.brickCount = 1234;

            Span<byte> mipCounts = new byte[10];
            for (int i = 0; i < 10; i++) mipCounts[i] = (byte)(i * 25);

            Span<byte> overlay = new byte[128];
            for (int i = 0; i < 128; i++) overlay[i] = (byte)i;

            Span<byte> buf = new byte[S_RegionData.HeaderSize + 128];
            msg.Encode(buf, mipCounts, overlay);

            var decodedMsg = S_RegionData.Decode(buf, out ReadOnlySpan<byte> decodedMip, out ReadOnlySpan<byte> decodedOverlay);
            Assert.AreEqual(0xCAFEBABEu, decodedMsg.seed);
            Assert.AreEqual(1234, decodedMsg.brickCount);
            Assert.AreEqual(128, decodedOverlay.Length);

            for (int i = 0; i < 128; i++)
                Assert.AreEqual((byte)i, decodedOverlay[i]);
        }

        [Test]
        public void T048_RegionData_ZeroOverlay()
        {
            var msg = new S_RegionData(0u);
            msg.brickCount = 0;

            Span<byte> mipCounts = stackalloc byte[1];
            mipCounts[0] = 0;

            Span<byte> buf = stackalloc byte[S_RegionData.HeaderSize];
            msg.Encode(buf, mipCounts, ReadOnlySpan<byte>.Empty);

            var decodedMsg = S_RegionData.Decode(buf, out ReadOnlySpan<byte> decodedMip, out ReadOnlySpan<byte> decodedOverlay);
            Assert.AreEqual(0u, decodedMsg.seed);
            Assert.AreEqual(0, decodedOverlay.Length);
        }

        [Test]
        public void T048_RegionData_AlignmentBoundary()
        {
            // Test with payload that lands on a 4-byte boundary.
            var msg = new S_RegionData(1u);
            msg.brickCount = 65535;

            Span<byte> mipCounts = stackalloc byte[4];
            Span<byte> overlay = stackalloc byte[12]; // aligned to 4 bytes

            Span<byte> buf = new byte[S_RegionData.HeaderSize + 12];
            msg.Encode(buf, mipCounts, overlay);

            var decodedMsg = S_RegionData.Decode(buf, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> decodedOverlay);
            Assert.AreEqual(65535, decodedMsg.brickCount);
            Assert.AreEqual(12, decodedOverlay.Length);
        }

        // -- T049: S_PlayerState ------------------------------------------------

        [Test]
        public void T049_PlayerState_RoundTrip()
        {
            var msg = new S_PlayerState(5, 77u);
            msg.sequence = 42;

            Span<byte> buf = stackalloc byte[S_PlayerState.WireSize];
            msg.Encode(buf, new float3(1.5f, -0.3f, 0.8f), new float3(0.1f, 0.05f, -0.02f));

            var (decodedMsg, posDelta, velDelta) = S_PlayerState.Decode(buf);
            Assert.AreEqual((ushort)5, decodedMsg.playerId);
            Assert.AreEqual(77u, decodedMsg.tick);
            Assert.AreEqual((ushort)42, decodedMsg.sequence);

            // Verify quantisation accuracy — should be within ~1 ulp.
            Assert.AreEqual(1.5f, posDelta.x, 0.0001f);
            Assert.AreEqual(-0.3f, posDelta.y, 0.0001f);
            Assert.AreEqual(0.8f, posDelta.z, 0.0001f);

            Assert.AreEqual(0.1f, velDelta.x, 0.00001f);
            Assert.AreEqual(0.05f, velDelta.y, 0.00001f);
            Assert.AreEqual(-0.02f, velDelta.z, 0.00001f);
        }

        [Test]
        public void T049_PlayerState_ZeroDelta()
        {
            var msg = new S_PlayerState(1, 1u);
            Span<byte> buf = stackalloc byte[S_PlayerState.WireSize];
            msg.Encode(buf, float3.zero, float3.zero);

            var (_, posDelta, velDelta) = S_PlayerState.Decode(buf);
            Assert.AreEqual(float3.zero, posDelta, "Position delta should be zero");
            Assert.AreEqual(float3.zero, velDelta, "Velocity delta should be zero");
        }

        [Test]
        public void T049_PlayerState_MaxDelta()
        {
            var msg = new S_PlayerState(ushort.MaxValue, uint.MaxValue);
            msg.sequence = ushort.MaxValue;
            Span<byte> buf = stackalloc byte[S_PlayerState.WireSize];
            msg.Encode(buf, new float3(100f, 100f, 100f), new float3(50f, -25f, 50f));

            var (decodedMsg, posDelta, velDelta) = S_PlayerState.Decode(buf);
            Assert.AreEqual(ushort.MaxValue, decodedMsg.playerId);
            Assert.AreEqual(uint.MaxValue, decodedMsg.tick);
        }

        [Test]
        public void T049_PlayerState_ShouldSendThreshold()
        {
            // Below threshold — should not send.
            Assert.IsFalse(S_PlayerState.ShouldSend(new float3(0.001f, 0.001f, 0.001f)));

            // At threshold — should send.
            Assert.IsTrue(S_PlayerState.ShouldSend(new float3(S_PlayerState.k_PositionThreshold, 0, 0)));

            // Above threshold — should send.
            Assert.IsTrue(S_PlayerState.ShouldSend(new float3(0.1f, 0.1f, 0.1f)));
        }

        [Test]
        public void T049_PlayerState_Edge_PartialPayload()
        {
            // Ensure decoding fails gracefully with undersized buffer.
            var msg = new S_PlayerState(1, 1u);
            Span<byte> fullBuf = stackalloc byte[S_PlayerState.WireSize];
            msg.Encode(fullBuf, float3.zero, float3.zero);

            // Truncate the buffer — decode should detect it. The buffer is copied to a
            // heap array because a Span local cannot be captured by the assertion lambda.
            var truncated = new byte[S_PlayerState.WireSize - 4];
            fullBuf.Slice(0, truncated.Length).CopyTo(truncated);

            // Decode logs an error before throwing; the test must expect it or the Unity
            // test runner fails on the unhandled log message.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("src too small"));
            Assert.Catch(() => S_PlayerState.Decode(truncated));
        }
    }
}
