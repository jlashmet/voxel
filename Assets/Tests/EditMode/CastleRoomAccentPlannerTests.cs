using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRoomAccentPlannerTests
    {
        [Test]
        public void PlannerPreservesHistoricalAccentDrawOrder()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in dimensions);

                for (int floor = 0; floor < interior.FloorCount; floor++)
                {
                    CastleKeepFloorPlan floorPlan = interior.Floor(floor);
                    CastleRoomAccentPlan actual = CastleRoomAccentPlanner.Create(
                        in dimensions, in floorPlan);
                    CastleRoomAccentSpec[] expected = LegacyAccents(
                        in dimensions, floorPlan.SemanticSeed);

                    Assert.AreEqual(expected.Length, actual.Count,
                        $"seed {seed}, floor {floor}: accent count drifted");
                    for (int i = 0; i < expected.Length; i++)
                    {
                        CastleRoomAccentSpec observed = actual.AccentAt(i);
                        Assert.AreEqual(expected[i].Id, observed.Id);
                        Assert.AreEqual(expected[i].LocalX, observed.LocalX);
                        Assert.AreEqual(expected[i].LocalZ, observed.LocalZ);
                        Assert.AreEqual(expected[i].Radius, observed.Radius);
                        Assert.AreEqual(expected[i].Height, observed.Height);
                    }
                }
            }
        }

        [Test]
        public void PlannedAccentsValidateAndSnapshotDefensively()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 41u);
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in dimensions);
            CastleKeepFloorPlan floorPlan = interior.Floor(0);
            CastleRoomAccentPlan accents = CastleRoomAccentPlanner.Create(
                in dimensions, in floorPlan);

            Assert.IsTrue(
                CastleRoomAccentPlanValidator.TryValidate(
                    in dimensions, accents, out CastleRoomAccentPlanIssue issue),
                issue.ToString());
            Assert.GreaterOrEqual(accents.Count, 2);
            Assert.LessOrEqual(accents.Count, 4);

            CastleRoomAccentSpec first = accents.AccentAt(0);
            CastleRoomAccentSpec[] snapshot = accents.Snapshot();
            snapshot[0] = new CastleRoomAccentSpec(99, -1, -1, 0, 0);

            Assert.AreEqual(first.Id, accents.AccentAt(0).Id);
            Assert.AreEqual(first.LocalX, accents.AccentAt(0).LocalX);
            Assert.AreEqual(first.LocalZ, accents.AccentAt(0).LocalZ);
        }

        private static CastleRoomAccentSpec[] LegacyAccents(
            in CastlePlan dimensions,
            uint semanticSeed)
        {
            const int inner = 8;
            int width = dimensions.KeepHalfX * 2;
            int depth = dimensions.KeepHalfZ * 2;
            var rng = new Random(semanticSeed);
            var result = new List<CastleRoomAccentSpec>(4);

            // This intentionally mirrors the old Runtime loop condition exactly. It is a parity
            // oracle for the planner, not an alternate production implementation.
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int localX = leftWall ? inner + 22 : width - inner - 30;
                int localZ = rng.NextInt(inner + 8, depth - inner - 12);
                int radius = rng.NextInt(4, 7);
                int height = rng.NextInt(8, 14);
                result.Add(new CastleRoomAccentSpec(
                    result.Count, localX, localZ, radius, height));
            }

            return result.ToArray();
        }
    }
}
