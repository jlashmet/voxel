using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleApproachFrameTests
    {
        [Test]
        public void LegacyFrontGateProducesPositiveXTangentAndNegativeZOutward()
        {
            var gate = new CastleGatePlacementSpec
            {
                EdgeIndex = 0,
                Centre = new int2(0, -250),
                Outward = new float2(0f, -4f),
            };

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);

            Assert.AreEqual(new float2(0f, -1f), frame.Outward);
            Assert.AreEqual(new float2(1f, 0f), frame.Tangent);
            Assert.AreEqual(new int2(37, -366), frame.LocalPoint(37f, 116f));
        }

        [Test]
        public void RotatedGateMapsDistancesInItsOwnBasis()
        {
            var gate = new CastleGatePlacementSpec
            {
                EdgeIndex = 2,
                Centre = new int2(250, 10),
                Outward = new float2(3f, 0f),
            };

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);

            Assert.AreEqual(new float2(1f, 0f), frame.Outward);
            Assert.AreEqual(new float2(0f, 1f), frame.Tangent);
            Assert.AreEqual(new int2(290, 30), frame.LocalPoint(20f, 40f));
        }
    }
}
