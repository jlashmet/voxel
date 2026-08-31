using System.Collections.Generic;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NaturalScatterVisibilityIndexTests
    {
        [Test]
        public void GenerateOrdinaryBoulders_IsDeterministicByWorldSector()
        {
            IReadOnlyList<NaturalScatterRecord> first =
                NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(1234u, -2, 3, 1000, 24);
            IReadOnlyList<NaturalScatterRecord> second =
                NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(1234u, -2, 3, 1000, 24);

            Assert.That(first, Has.Count.EqualTo(24));
            Assert.That(second, Is.EqualTo(first));
            for (int i = 1; i < first.Count; i++)
                Assert.That(first[i].StableId, Is.GreaterThan(first[i - 1].StableId));
        }

        [Test]
        public void Query_UsesFixedSectorsAndStableOrderIncludingNegativeCoordinates()
        {
            var records = new List<NaturalScatterRecord>();
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, -1, 0, 100, 5));
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, 0, 0, 100, 5));
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, 1, 0, 100, 5));
            records.Reverse();

            IReadOnlyList<NaturalScatterRecord> visible =
                NaturalScatterVisibilityIndex.Query(records, 100, -1, 0, 0, 0);

            Assert.That(visible, Has.Count.EqualTo(10));
            for (int i = 0; i < visible.Count; i++)
            {
                int x = visible[i].PositionDm.X;
                Assert.That(x, Is.GreaterThanOrEqualTo(-100).And.LessThan(100));
                if (i > 0) Assert.That(visible[i].StableId, Is.GreaterThan(visible[i - 1].StableId));
            }
        }

        [Test]
        public void ExceptionalRock_CanRemainExplicitLandmarkRecord()
        {
            var landmark = new NaturalScatterRecord(
                0x1234UL,
                new Int2(250, -350),
                120,
                800,
                NaturalScatterKind.RockSpire,
                NaturalScatterImportance.HorizonLandmark,
                9UL);

            IReadOnlyList<NaturalScatterRecord> visible = NaturalScatterVisibilityIndex.Query(
                new[] { landmark },
                1000,
                0,
                -1,
                0,
                -1);

            Assert.That(visible, Has.Count.EqualTo(1));
            Assert.That(visible[0].Importance, Is.EqualTo(NaturalScatterImportance.HorizonLandmark));
            Assert.That(visible[0].StableId, Is.EqualTo(0x1234UL));
        }
    }
}
