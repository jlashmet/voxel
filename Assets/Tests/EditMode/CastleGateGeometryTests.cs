using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGateGeometryTests
    {
        [Test]
        public void LegacyFrontGateMatchesHistoricalAxisAlignedCoordinates()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(
                    new int3((int)seed * 3, 48, -(int)seed * 2), seed);
                CastleGateGeometry gate = CastleGateGeometryResolver.LegacyFront(in plan);
                int3 legacyMin = CastleLayout.FrontGateMinimum(in plan);

                Assert.AreEqual(legacyMin, gate.Origin, $"seed {seed}: origin");
                Assert.AreEqual(new float2(1f, 0f), gate.Tangent, $"seed {seed}: tangent");
                Assert.AreEqual(new float2(0f, -1f), gate.Outward, $"seed {seed}: outward");
                Assert.AreEqual(legacyMin, gate.WorldVoxel(0, 0, 0), $"seed {seed}: first voxel");
                Assert.AreEqual(
                    new int3(
                        legacyMin.x + CastleLayout.FrontGateWidth - 1,
                        legacyMin.y + CastleLayout.FrontGateHeight - 1,
                        legacyMin.z + CastleLayout.FrontGateDepth - 1),
                    gate.WorldVoxel(
                        CastleLayout.FrontGateWidth - 1,
                        CastleLayout.FrontGateHeight - 1,
                        CastleLayout.FrontGateDepth - 1),
                    $"seed {seed}: last voxel");

                float3 interaction = gate.InteractionPointVoxels;
                Assert.AreEqual(legacyMin.x + CastleLayout.FrontGateWidth * 0.5f,
                    interaction.x, $"seed {seed}: interaction x");
                Assert.AreEqual(legacyMin.y, interaction.y, $"seed {seed}: interaction y");
                Assert.AreEqual(legacyMin.z - 8f, interaction.z,
                    $"seed {seed}: interaction z");
            }
        }

        [Test]
        public void RotatedGateUsesOneSharedLocalToWorldBasis()
        {
            var plan = new CastlePlan
            {
                Centre = new int3(100, 20, 200),
                PlateauHeight = 12,
                WallThickness = 10,
            };
            var placement = new CastleGatePlacementSpec
            {
                Centre = new int2(30, -40),
                Outward = new float2(1f, 0f),
            };

            CastleGateGeometry gate = CastleGateGeometryResolver.Resolve(in plan, in placement);

            Assert.AreEqual(new float2(0f, 1f), gate.Tangent);
            Assert.AreEqual(new float2(1f, 0f), gate.Outward);
            Assert.AreEqual(new float2(130f, 160f), gate.PerimeterCentre);

            int3 first = gate.WorldVoxel(0, 0, 0);
            int3 along = gate.WorldVoxel(1, 0, 0);
            int3 inward = gate.WorldVoxel(0, 0, 1);
            Assert.AreEqual(new int3(0, 0, 1), along - first,
                "Width must advance along the planned gate tangent.");
            Assert.AreEqual(new int3(-1, 0, 0), inward - first,
                "Gate depth must extend inward, opposite the outward normal.");

            float3 interaction = gate.InteractionPointVoxels;
            Assert.Greater(interaction.x, gate.PerimeterCentre.x,
                "Interaction point must remain outside the fortification.");
        }

        [Test]
        public void ArchMaskMatchesHistoricalSemicircularHead()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 7u);
            CastleGateGeometry gate = CastleGateGeometryResolver.LegacyFront(in plan);
            int half = gate.Width / 2;
            int archTop = gate.Height - half;

            Assert.IsTrue(gate.ContainsArchVoxel(0, archTop));
            Assert.IsTrue(gate.ContainsArchVoxel(half, gate.Height - 1));
            Assert.IsFalse(gate.ContainsArchVoxel(0, gate.Height - 1));
            Assert.IsFalse(gate.ContainsArchVoxel(-1, 0));
            Assert.IsFalse(gate.ContainsArchVoxel(0, gate.Height));
        }
    }
}
