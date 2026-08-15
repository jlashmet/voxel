using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-009 coverage for late-join time-to-playable.
    ///
    /// Net owns the protocol shape and the fact that coarse join cost is independent of edit
    /// history. Storage owns authoritative semantic region data; this suite therefore does not
    /// reach through Net into physical RegionTable/BrickPool storage.
    /// </summary>
    public sealed class LateJoinTests
    {
        private const int k_CoordSize = sizeof(int) * 3;
        private const int k_ExpectedRegionCount = 9;
        private const int k_ExpectedCellsPerRegion = 1;
        private const int k_ExpectedPayloadSize = sizeof(uint) +
            k_ExpectedRegionCount * (k_CoordSize + sizeof(uint) + sizeof(ulong));

        [SetUp]
        public void SetUp() => SessionLifecycle.Create(12345u);

        [Test]
        public void TopLevelMips_ImmediatelyAvailable_NoBrickDataNeeded()
        {
            int3 playerRegion = new int3(0, 5, 0);
            byte[] payload = LateJoin.ShipTopLevelMips(playerRegion);

            Assert.That(payload, Is.Not.Null.And.Not.Empty);
            Assert.That(payload.Length, Is.EqualTo(k_ExpectedPayloadSize));
            Assert.That(ReadU32(payload, 0), Is.EqualTo((uint)k_ExpectedRegionCount));

            int offset = sizeof(uint);
            var seen = new HashSet<string>();
            bool sawPlayerRegion = false;

            for (int i = 0; i < k_ExpectedRegionCount; i++)
            {
                int3 coord = ReadInt3(payload, offset);
                offset += k_CoordSize;

                uint cellCount = ReadU32(payload, offset);
                offset += sizeof(uint);
                Assert.That(cellCount, Is.EqualTo((uint)k_ExpectedCellsPerRegion));

                ulong topCell = ReadU64(payload, offset);
                offset += sizeof(ulong);
                Assert.That(topCell, Is.Not.EqualTo(0UL));

                Assert.That(coord.y, Is.EqualTo(playerRegion.y));
                Assert.That(math.abs(coord.x - playerRegion.x), Is.LessThanOrEqualTo(1));
                Assert.That(math.abs(coord.z - playerRegion.z), Is.LessThanOrEqualTo(1));
                Assert.That(seen.Add($"{coord.x}:{coord.y}:{coord.z}"), Is.True,
                    "The 3x3 join neighborhood must not contain duplicate region entries.");

                sawPlayerRegion |= math.all(coord == playerRegion);
            }

            Assert.That(offset, Is.EqualTo(payload.Length));
            Assert.That(sawPlayerRegion, Is.True);
        }

        [Test]
        public void NeighborGrid_IsCenteredOnRequestedRegion_AndHasBoundedWireSize()
        {
            int3 requested = new int3(-17, 3, 42);
            byte[] payload = LateJoin.ShipTopLevelMips(requested);

            Assert.That(ReadU32(payload, 0), Is.EqualTo((uint)k_ExpectedRegionCount));
            Assert.That(payload.Length, Is.EqualTo(k_ExpectedPayloadSize),
                "SC-009: coarse join cost is bounded by protocol shape, not world detail.");

            int offset = sizeof(uint);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;

            for (int i = 0; i < k_ExpectedRegionCount; i++)
            {
                int3 coord = ReadInt3(payload, offset);
                offset += k_CoordSize;
                uint cellCount = ReadU32(payload, offset);
                offset += sizeof(uint) + checked((int)cellCount) * sizeof(ulong);

                minX = math.min(minX, coord.x);
                maxX = math.max(maxX, coord.x);
                minZ = math.min(minZ, coord.z);
                maxZ = math.max(maxZ, coord.z);
                Assert.That(coord.y, Is.EqualTo(requested.y));
            }

            Assert.That(minX, Is.EqualTo(requested.x - 1));
            Assert.That(maxX, Is.EqualTo(requested.x + 1));
            Assert.That(minZ, Is.EqualTo(requested.z - 1));
            Assert.That(maxZ, Is.EqualTo(requested.z + 1));
        }

        [Test]
        public void PayloadSize_IsIndependentOfSessionAlterationHistory()
        {
            int3 playerRegion = new int3(2, 0, -3);
            byte[] before = LateJoin.ShipTopLevelMips(playerRegion);

            for (int i = 0; i < 10_000; i++)
                SessionLifecycle.RecordAlteration();

            byte[] after = LateJoin.ShipTopLevelMips(playerRegion);

            Assert.That(SessionLifecycle.TotalAlterations, Is.EqualTo(10_000));
            Assert.That(after.Length, Is.EqualTo(before.Length),
                "SC-009: time-to-playable payload size must not scale with edit history.");
            Assert.That(after, Is.EqualTo(before),
                "Late join ships current coarse state; Net must not replay alteration history.");
        }

        private static int3 ReadInt3(byte[] payload, int offset) => new int3(
            unchecked((int)ReadU32(payload, offset)),
            unchecked((int)ReadU32(payload, offset + sizeof(int))),
            unchecked((int)ReadU32(payload, offset + sizeof(int) * 2)));

        private static uint ReadU32(byte[] payload, int offset) =>
            (uint)(payload[offset] |
                   (payload[offset + 1] << 8) |
                   (payload[offset + 2] << 16) |
                   (payload[offset + 3] << 24));

        private static ulong ReadU64(byte[] payload, int offset) =>
            ReadU32(payload, offset) | ((ulong)ReadU32(payload, offset + sizeof(uint)) << 32);
    }
}
