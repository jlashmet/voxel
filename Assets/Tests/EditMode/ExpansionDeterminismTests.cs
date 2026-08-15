using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExpansionDeterminismTests
    {
        private const uint Seed = 12345u;

        private static AlterationEvent ExplosionEvent(uint seed, ushort radius = 5) =>
            new AlterationEvent(
                AlterationEvent.KindExplosion,
                1u,
                new int3(256, 256, 256),
                radius,
                VoxelDimensions.MaterialEmpty,
                seed,
                1,
                1);

        [Test]
        public void ExplosionProducesIdenticalBricksTwice()
        {
            var poolA = new BrickPool(256, Allocator.Temp);
            var poolB = new BrickPool(256, Allocator.Temp);
            var tableA = new RegionTable(4, Allocator.Temp);
            var tableB = new RegionTable(4, Allocator.Temp);
            var evt = ExplosionEvent(Seed);
            var resultA = ExplosionExpansion.Expand(new RegionReadSource(in tableA, in poolA), in evt);
            var resultB = ExplosionExpansion.Expand(new RegionReadSource(in tableB, in poolB), in evt);
            CompareExpansions(resultA, resultB, "explosion");
        }

        [Test]
        public void BrushCubeProducesIdenticalBricksTwice()
        {
            var poolA = new BrickPool(256, Allocator.Temp);
            var poolB = new BrickPool(256, Allocator.Temp);
            var tableA = new RegionTable(4, Allocator.Temp);
            var tableB = new RegionTable(4, Allocator.Temp);

            var evt = AlterationEvent.CreateCubeBrush(
                tick: 2u,
                origin: new int3(240, 240, 240),
                extentXBricks: 8,
                extentYBricks: 8,
                extentZBricks: 8,
                material: 3,
                seed: Seed,
                playerId: 2,
                sequence: 1);

            var resultA = BrushExpansion.Expand(evt);
            var resultB = BrushExpansion.Expand(evt);
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
                var resultA = ExplosionExpansion.Expand(new RegionReadSource(in tableA, in poolA), in evt);
                var resultB = ExplosionExpansion.Expand(new RegionReadSource(in tableB, in poolB), in evt);
                CompareExpansions(resultA, resultB, $"seed {s}");
            }
        }

        [Test]
        public void ExplosionExpansionIsCurrentlySeedIndependent()
        {
            var pool = new BrickPool(256, Allocator.Temp);
            var table = new RegionTable(4, Allocator.Temp);
            using var withSeedA = ExplosionExpansion.Expand(new RegionReadSource(in table, in pool), ExplosionEvent(1u));
            using var withSeedB = ExplosionExpansion.Expand(new RegionReadSource(in table, in pool), ExplosionEvent(99999u));
            Assert.AreEqual(withSeedA.Length, withSeedB.Length);
            for (int i = 0; i < withSeedA.Length; i++)
                Assert.IsTrue(math.all(withSeedA[i] == withSeedB[i]));
        }

        [Test]
        public void DeterministicRandomProducesIdenticalSequence()
        {
            var r1 = new DeterministicRandom(Seed);
            var r2 = new DeterministicRandom(Seed);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(r1.NextRange(-100, 100), r2.NextRange(-100, 100));
        }

        [Test]
        public void DeterministicRandomAdvancesState()
        {
            var rng = new DeterministicRandom(Seed);
            uint first = rng.NextUint();
            bool sawDifferent = false;
            for (int i = 0; i < 32 && !sawDifferent; i++) sawDifferent = rng.NextUint() != first;
            Assert.IsTrue(sawDifferent);
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
