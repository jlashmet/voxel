using System.Collections.Generic;
using MountingForce.WorldGen;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NaturalScatterVisibilityTests
    {
        [Test]
        public void Query_IsDeterministicByWorldSector_AndPreservesSemanticLandmarks()
        {
            var placements = new[]
            {
                new NaturalScatterDescriptor(30UL, 7, 125, 0, 150, 8, 18, NaturalScatterPresentationClass.Ordinary),
                new NaturalScatterDescriptor(10UL, 7, 180, 0, 120, 10, 22, NaturalScatterPresentationClass.Ordinary),
                new NaturalScatterDescriptor(20UL, 99, 190, 0, 175, 45, 130, NaturalScatterPresentationClass.Landmark),
                new NaturalScatterDescriptor(40UL, 7, 260, 0, 150, 8, 18, NaturalScatterPresentationClass.Ordinary),
                new NaturalScatterDescriptor(5UL, 7, -1, 0, 150, 8, 18, NaturalScatterPresentationClass.Ordinary)
            };
            var output = new List<NaturalScatterVisibilityEntry>();
            var sector = new NaturalScatterSectorBounds(1, 1, 1, 1);

            NaturalScatterVisibility.Query(placements, 100, in sector, output);

            Assert.That(output.Count, Is.EqualTo(3));
            Assert.That(output[0].StableId, Is.EqualTo(10UL));
            Assert.That(output[1].StableId, Is.EqualTo(20UL));
            Assert.That(output[2].StableId, Is.EqualTo(30UL));
            Assert.That(output[1].IsLandmark, Is.True,
                "Exceptional natural features must remain independent semantic records for far presentation.");
            Assert.That(output[0].SectorX, Is.EqualTo(1));
            Assert.That(output[0].SectorZ, Is.EqualTo(1));
        }

        [Test]
        public void Query_HandlesNegativeWorldSectorsWithoutCameraDependentIdentity()
        {
            var placement = new NaturalScatterDescriptor(
                9001UL, 12, -1, 0, -101, 12, 24, NaturalScatterPresentationClass.Ordinary);
            var placements = new[] { placement };
            var output = new List<NaturalScatterVisibilityEntry>();
            var negativeSector = new NaturalScatterSectorBounds(-1, -2, -1, -2);

            NaturalScatterVisibility.Query(placements, 100, in negativeSector, output);

            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].SectorX, Is.EqualTo(-1));
            Assert.That(output[0].SectorZ, Is.EqualTo(-2));
            Assert.That(output[0].StableId, Is.EqualTo(placement.StableId));
        }

        [Test]
        public void SameVisibilityPath_ReusesIndependentNaturalScatterArchetypes_WithoutVoxelState()
        {
            var boulder = new NaturalScatterDescriptor(
                101UL, 1, 25, 0, 25, 6, 12, NaturalScatterPresentationClass.Ordinary);
            var crystalOutcrop = new NaturalScatterDescriptor(
                202UL, 2, 35, 0, 30, 14, 46, NaturalScatterPresentationClass.Landmark);
            var placements = new[] { crystalOutcrop, boulder };
            var output = new List<NaturalScatterVisibilityEntry>();
            var sector = new NaturalScatterSectorBounds(0, 0, 0, 0);

            NaturalScatterVisibility.Query(placements, 100, in sector, output);

            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].Descriptor.ArchetypeId, Is.EqualTo(1));
            Assert.That(output[1].Descriptor.ArchetypeId, Is.EqualTo(2));
            Assert.That(output[0].StableId, Is.EqualTo(101UL));
            Assert.That(output[1].StableId, Is.EqualTo(202UL));
            Assert.That(output[1].IsLandmark, Is.True);
        }
    }
}
