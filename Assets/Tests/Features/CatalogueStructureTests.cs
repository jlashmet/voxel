using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Tests.Features.Fixtures;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    /// <summary>
    /// The invariants a catalogue must satisfy before anything generates.
    ///
    /// These are the checks whose failure produces a world that is *wrong* rather than a world
    /// that fails: an oversized footprint silently taxes every region in the world, and a hash
    /// that misses part of the catalogue lets two clients agree they share a world they do not.
    /// </summary>
    public sealed class CatalogueStructureTests
    {
        [Test]
        public void FixtureCatalogueLoads()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);

            var result = FeatureCatalogueBuilder.Finalise(ref catalogue);

            Assert.AreEqual(CatalogueLoadResult.Ok, result);
            Assert.AreNotEqual(0ul, catalogue.Hash, "a loaded catalogue must have an identity hash");

            catalogue.Dispose();
        }

        [Test]
        public void OversizedFootprintIsRefused()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);

            var definition = catalogue.Definitions[CottageFixture.CottageId];
            definition.Footprint = new int3(FeatureBudget.MaxFootprintVoxels + 1, 80, 96);
            catalogue.Definitions[CottageFixture.CottageId] = definition;

            Assert.AreEqual(CatalogueLoadResult.FootprintExceedsBudget,
                FeatureCatalogueBuilder.Finalise(ref catalogue));

            catalogue.Dispose();
        }

        [Test]
        public void SpacingLargerThanTheScannedNeighbourhoodIsRefused()
        {
            // Spacing is enforced against candidates in the cells a region already scans. A
            // spacing wider than that could only be enforced with knowledge the region does not
            // have, so accepting it would mean silently not enforcing it.
            var catalogue = CottageFixture.Build(Allocator.Temp);

            var rule = catalogue.Rules[0];
            rule.MinSpacing = rule.CellEdge + 1;
            catalogue.Rules[0] = rule;

            Assert.AreEqual(CatalogueLoadResult.SpacingNotEnforceable,
                FeatureCatalogueBuilder.Finalise(ref catalogue));

            catalogue.Dispose();
        }

        [Test]
        public void UnsupportedVersionIsRefused()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            catalogue.Version = FeatureCatalogueBuilder.SupportedVersion + 1;

            Assert.AreEqual(CatalogueLoadResult.UnsupportedVersion,
                FeatureCatalogueBuilder.Finalise(ref catalogue));

            catalogue.Dispose();
        }

        [Test]
        public void HashCoversTheProgramBody()
        {
            // A single changed opcode changes the world. A hash that missed the program would let
            // two clients compare catalogues, agree, and generate different worlds.
            var a = FeatureCatalogueBuilder.Allocate(1, 0, 0, 0, 0, 4, 0, 0, 0, Allocator.Temp);
            var b = FeatureCatalogueBuilder.Allocate(1, 0, 0, 0, 0, 4, 0, 0, 0, Allocator.Temp);

            a.Definitions[0] = new FeatureDefinition { Footprint = new int3(8, 8, 8), MaxPrimitives = 1 };
            b.Definitions[0] = a.Definitions[0];

            for (var i = 0; i < 4; i++) { a.Program[i] = i; b.Program[i] = i; }
            Assert.AreEqual(FeatureCatalogueBuilder.ComputeHash(in a), FeatureCatalogueBuilder.ComputeHash(in b));

            b.Program[2] = 99;
            Assert.AreNotEqual(FeatureCatalogueBuilder.ComputeHash(in a), FeatureCatalogueBuilder.ComputeHash(in b),
                "changing an opcode did not change the catalogue hash");

            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void ParameterClampSnapsInsideTheDeclaredRange()
        {
            var spec = new ParameterSpec { Min = 48, Max = 88, Quantum = 8 };

            Assert.AreEqual(48, spec.Clamp(0));
            Assert.AreEqual(88, spec.Clamp(1000));
            Assert.AreEqual(56, spec.Clamp(59));

            // Snapping must never leave the range: rounding to nearest would produce 96 here.
            var tight = new ParameterSpec { Min = 0, Max = 10, Quantum = 4 };
            Assert.LessOrEqual(tight.Clamp(10), 10);
        }
    }

    /// <summary>
    /// Placement hashing decides where everything in the world is, so its mixing quality is a
    /// correctness property rather than an aesthetic one.
    /// </summary>
    public sealed class FeatureHashTests
    {
        [Test]
        public void CellHashIsPureAndSeparatesNeighbours()
        {
            Assert.AreEqual(FeatureHash.Cell(7u, 3, new int3(1, 2, 3)),
                            FeatureHash.Cell(7u, 3, new int3(1, 2, 3)));

            Assert.AreNotEqual(FeatureHash.Cell(7u, 3, new int3(1, 2, 3)),
                               FeatureHash.Cell(7u, 3, new int3(2, 2, 3)));

            Assert.AreNotEqual(FeatureHash.Cell(7u, 3, new int3(1, 2, 3)),
                               FeatureHash.Cell(7u, 4, new int3(1, 2, 3)));

            Assert.AreNotEqual(FeatureHash.Cell(7u, 3, new int3(1, 2, 3)),
                               FeatureHash.Cell(8u, 3, new int3(1, 2, 3)));
        }

        [Test]
        public void CellPairHashIsOrderIndependent()
        {
            // Both sides of a cave portal must derive the same portal without talking. If this
            // is order-dependent, tunnels meet from one side and not the other.
            var a = new int3(4, 0, 9);
            var b = new int3(5, 0, 9);

            Assert.AreEqual(FeatureHash.CellPair(3u, a, b), FeatureHash.CellPair(3u, b, a));
            Assert.AreNotEqual(FeatureHash.CellPair(3u, a, b),
                               FeatureHash.CellPair(3u, a, new int3(6, 0, 9)));
        }

        [Test]
        public void RangeStaysInBoundsAndUsesTheWholeRange()
        {
            ulong state = FeatureHash.Cell(1u, 0, int3.zero);

            var seen = new bool[5];
            for (var i = 0; i < 400; i++)
            {
                int v = FeatureHash.Range(ref state, 10, 14);
                Assert.GreaterOrEqual(v, 10);
                Assert.LessOrEqual(v, 14);
                seen[v - 10] = true;
            }

            foreach (var hit in seen)
                Assert.IsTrue(hit, "draws do not cover the whole range — the reduction is biased");
        }

        [Test]
        public void ChanceHonoursItsBounds()
        {
            ulong state = 12345ul;

            Assert.IsFalse(FeatureHash.Chance(ref state, 0));
            Assert.IsTrue(FeatureHash.Chance(ref state, 65536));

            int hits = 0;
            for (var i = 0; i < 4000; i++)
                if (FeatureHash.Chance(ref state, 16384)) hits++;

            // A quarter of 4000, with generous slack for a finite sample.
            Assert.Greater(hits, 700);
            Assert.Less(hits, 1300);
        }
    }
}
