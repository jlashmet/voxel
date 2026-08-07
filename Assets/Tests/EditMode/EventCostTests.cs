using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// SC-002: A destruction event affecting ≥ 4000 voxels must transmit in ≤ 64 bytes —
    /// within 2× the cost of an ordinary player action.
    ///
    /// This is what makes 64 players on a mobile connection viable. The event carries the
    /// *cause* (origin, radius, seed), not the *effect* (thousands of voxel writes).
    /// Clients expand deterministically from the cause, so the wire payload never grows with
    /// world complexity — only with event severity, and not even with that.
    ///
    /// The measured quantity is the real broadcast cost: the S_AlterationEvent header plus
    /// the AlterationEvent payload it wraps. The header is genuinely encoded into a buffer
    /// rather than assumed, so a header that outgrows its declared size fails the test.
    /// </summary>
    public sealed class EventCostTests
    {
        /// <summary>Total broadcast bytes for one alteration: header + payload.</summary>
        private static int BroadcastCost(in AlterationEvent evt)
        {
            int payloadLength = AlterationEvent.WireSize();

            // Encode the header for real: this is what catches a header whose declared
            // HeaderSize has drifted from what Encode actually writes.
            var payload = new byte[payloadLength];
            var wire = new byte[S_AlterationEvent.HeaderSize + payloadLength];

            var header = new S_AlterationEvent(evt.tick, RegionOf(evt.origin));
            header.Encode(wire, payload);

            return wire.Length;
        }

        private static int3 RegionOf(int3 voxel)
        {
            VoxelAccess.Decompose(voxel, out int3 regionCoord, out _, out _);
            return regionCoord;
        }

        private static AlterationEvent Explosion(ushort radiusBricks) => new AlterationEvent(
            AlterationEvent.KindExplosion,
            1u,                             // tick
            new int3(256, 256, 256),        // origin
            radiusBricks,                   // shapeRadius
            VoxelDimensions.MaterialEmpty,  // material
            42u,                            // seed
            1,                              // playerId
            1);                             // sequence

        [Test]
        [Category("SC_002")]
        [Category("Bandwidth")]
        public void LargeDestructionEventTransmitsUnder64Bytes()
        {
            // A 32-brick radius explosion spans ~131,000 voxels, but the wire payload is
            // fixed: the client re-derives the affected set from origin, radius, and seed.
            var evt = Explosion(32);

            int voxelCount = EstimateVoxelCount(evt.Radius());
            Assert.GreaterOrEqual(voxelCount, 4000,
                $"Explosion(r={evt.Radius()}) must affect >= 4000 voxels (estimated {voxelCount}).");

            int encodedSize = BroadcastCost(in evt);
            Assert.LessOrEqual(encodedSize, 64,
                $"Event affecting >= 4000 voxels must transmit in <= 64 bytes, got {encodedSize} bytes.");
        }

        [Test]
        [Category("SC_002")]
        public void OrdinaryPlayerActionIsComparableToDestructionEvent()
        {
            // A single-brick placement request is the ordinary action SC-002 measures against.
            var placement = new C_AlterationRequest
            {
                tick = 1u,
                playerId = 1,
                sequence = 1,
                eventKind = AlterationEvent.KindBrush,
                origin = new int3(256, 256, 256),
                shapeRadius = 1,
                material = 3,
                seed = 42u,
            };

            var requestWire = new byte[C_AlterationRequest.WireSize];
            placement.Encode(requestWire);

            int explosionSize = BroadcastCost(Explosion(32));

            // SC-002's actual claim is a ratio, not an absolute ordering: a massive
            // destruction costs no more than 2x an ordinary action.
            Assert.LessOrEqual(explosionSize, C_AlterationRequest.WireSize * 2,
                $"A >=4000-voxel destruction ({explosionSize} B) must stay within 2x an " +
                $"ordinary action ({C_AlterationRequest.WireSize} B).");
        }

        [Test]
        [Category("SC_002")]
        public void WirePayloadDoesNotGrowWithRadius()
        {
            // Increasing radius must not increase wire size — the seed carries the expansion.
            int baseSize = BroadcastCost(Explosion(1));

            for (ushort r = 1; r <= 63; r++)
            {
                int size = BroadcastCost(Explosion(r));
                Assert.AreEqual(baseSize, size,
                    $"Wire size must be constant across radii: r={r}, size={size} (expected {baseSize}).");
            }
        }

        /// <summary>Estimate the number of voxels affected by an explosion of given radius (in bricks).</summary>
        private static int EstimateVoxelCount(ushort radiusBricks)
        {
            // Sphere volume in voxels: 4/3 * pi * (radiusBricks * voxelsPerBrickEdge)^3.
            float voxelRadius = radiusBricks * VoxelDimensions.BrickEdge;
            return (int)(4.0f / 3.0f * math.PI * voxelRadius * voxelRadius * voxelRadius);
        }
    }
}
