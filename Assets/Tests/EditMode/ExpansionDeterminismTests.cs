using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Asserts bit-identical expansion output for identical input across all expansion
    /// types.  Determinism is not optional — it is the foundation of SC-003 and SC-016,
    /// because cross-client divergence originates here if PRNG or geometry logic drifts.
    ///
    /// Each test expands the same event twice against independently constructed state. If
    /// expansion carried any hidden dependence on allocation order, pool identity, or
    /// floating-point rounding, the two runs would diverge.
    /// </summary>
    public sealed class ExpansionDeterminismTests
    {
        private const uint Seed = 12345u;

        /// <summary>An explosion event at a fixed origin; only the seed varies.</summary>
        private static AlterationEvent ExplosionEvent(uint seed, ushort radius = 5) =>
            new AlterationEvent(
                AlterationEvent.KindExplosion,
                1u,                          // tick
                new int3(256, 256, 256),     // origin
                radius,                      // shapeRadius
                VoxelDimensions.MaterialEmpty,
                seed,
                1,                           // playerId
                1);                          // sequence

        [Test]
        public void ExplosionProducesIdenticalBricksTwice()
        {
            var poolA = new BrickPool(256, Allocator.Temp);
            var poolB = new BrickPool(256, Allocator.Temp);
            var tableA = new RegionTable(4, Allocator.Temp);
            var tableB = new RegionTable(4, Allocator.Temp);

            var evt = ExplosionEvent(Seed);

            var resultA = ExplosionExpansion.Expand(in poolA, in tableA, in evt);
            var resultB = ExplosionExpansion.Expand(in poolB, in tableB, in evt);

            CompareExpansions(resultA, resultB, "explosion");
        }

        [Test]
        public void BrushCubeProducesIdenticalBricksTwice()
        {
            var poolA = new BrickPool(256, Allocator.Temp);
            var poolB = new BrickPool(256, Allocator.Temp);
            var tableA = new RegionTable(4, Allocator.Temp);
            var tableB = new RegionTable(4, Allocator.Temp);

            // Brush extents are packed across shapeKind (x, y) and shapeData (z).
            var evt = new AlterationEvent(
                AlterationEvent.KindBrush,
                2u,
                new int3(240, 240, 240),
                0,
                3,      // material
                Seed,
                2,      // playerId
                1);
            evt.shapeKind = 8u | (8u << 16); // extents x = 8, y = 8
            evt.shapeData = 8u;              // extents z = 8

            var resultA = BrushExpansion.Expand(in poolA, in tableA, evt);
            var resultB = BrushExpansion.Expand(in poolB, in tableB, evt);

            CompareExpansions(resultA, resultB, "brush cube");
        }

        [Test]
        public void MultipleSeedsAllDeterministic()
        {
            for (uint s = 0; s < 10; s++)
            {
                var poolA = new BrickPool(256, Allocator.Temp);
                var poolB = new BrickPool(256, Allocator.Temp);
                var tableA = new RegionTable(4, Allocator.Temp);
                var tableB = new RegionTable(4, Allocator.Temp);

                var evt = ExplosionEvent(s, radius: 4);

                var resultA = ExplosionExpansion.Expand(in poolA, in tableA, in evt);
                var resultB = ExplosionExpansion.Expand(in poolB, in tableB, in evt);

                CompareExpansions(resultA, resultB, $"seed {s}");
            }
        }

        [Test]
        public void ExplosionExpansionIsCurrentlySeedIndependent()
        {
            // Documents actual behaviour, and it is not what the event struct implies.
            //
            // ExplosionExpansion.Expand constructs a DeterministicRandom from evt.seed and
            // then never reads it: the affected set is a perfect integer sphere. So the
            // expansion is deterministic trivially rather than through the seeded PRNG that
            // AlterationEvent.seed exists to drive.
            //
            // That is fine for determinism (a sphere is identical everywhere) but it means
            // the seed plumbing is currently inert, and any future jitter, material-dependent
            // spalling, or irregular blast shape has to start by actually consuming the rng.
            // This test will fail the moment that happens, which is the intended signal to
            // replace it with a real different-seeds-differ assertion.
            var pool = new BrickPool(256, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);

            using var withSeedA = ExplosionExpansion.Expand(in pool, in table, ExplosionEvent(1u));
            using var withSeedB = ExplosionExpansion.Expand(in pool, in table, ExplosionEvent(99999u));

            Assert.AreEqual(withSeedA.Length, withSeedB.Length,
                "Explosion expansion ignores the seed today, so both runs must match.");

            for (int i = 0; i < withSeedA.Length; i++)
            {
                Assert.IsTrue(math.all(withSeedA[i] == withSeedB[i]),
                    $"Brick {i} differs between seeds, but expansion does not read the seed.");
            }
        }

        [Test]
        public void DeterministicRandomProducesIdenticalSequence()
        {
            var r1 = new DeterministicRandom(Seed);
            var r2 = new DeterministicRandom(Seed);

            for (int i = 0; i < 100; i++)
            {
                var v1 = r1.NextRange(-100, 100);
                var v2 = r2.NextRange(-100, 100);
                Assert.AreEqual(v1, v2, $"Sequence[{i}] differs: {v1} vs {v2}");
            }
        }

        [Test]
        public void DeterministicRandomAdvancesState()
        {
            // A generator whose state never advanced would satisfy the sequence-equality
            // test above while being useless, so assert that it actually varies.
            var rng = new DeterministicRandom(Seed);

            uint first = rng.NextUint();
            bool sawDifferent = false;

            for (int i = 0; i < 32 && !sawDifferent; i++)
                sawDifferent = rng.NextUint() != first;

            Assert.IsTrue(sawDifferent, "PRNG returned a constant value — state is not advancing.");
        }

        private static void CompareExpansions(NativeList<int3> a, NativeList<int3> b, string label)
        {
            Assert.AreEqual(a.Length, b.Length, $"{label}: expanded brick count differs.");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, $"{label}[{i}].x");
                Assert.AreEqual(a[i].y, b[i].y, $"{label}[{i}].y");
                Assert.AreEqual(a[i].z, b[i].z, $"{label}[{i}].z");
            }

            a.Dispose();
            b.Dispose();
        }
    }
}
