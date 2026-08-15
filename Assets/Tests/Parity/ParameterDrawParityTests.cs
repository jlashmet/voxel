using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Parameter draws decide how tall a house is and which materials it uses, so two clients
    /// that draw differently build visibly different worlds from the same seed.
    ///
    /// These assert the properties that make the draw reproducible: it depends on nothing but its
    /// inputs, it is stable across repeated evaluation, and it stays inside the authored range so
    /// the footprint proof validation performed still holds.
    /// </summary>
    public sealed class ParameterDrawParityTests
    {
        [Test]
        public void DrawsDependOnlyOnSeedDefinitionAndPosition()
        {
            var position = new int3(1234, 0, 5678);

            ulong a = FeatureGeneration.InstanceSeed(99u, 3, position);
            ulong b = FeatureGeneration.InstanceSeed(99u, 3, position);

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, FeatureGeneration.InstanceSeed(99u, 4, position));
            Assert.AreNotEqual(a, FeatureGeneration.InstanceSeed(100u, 3, position));
            Assert.AreNotEqual(a, FeatureGeneration.InstanceSeed(99u, 3, position + new int3(1, 0, 0)));
        }

        [Test]
        public void RepeatedDrawsFromTheSameSeedMatch()
        {
            const int samples = 256;

            var first = new int[samples];
            var second = new int[samples];

            ulong stateA = FeatureGeneration.InstanceSeed(7u, 0, new int3(64, 0, 64));
            ulong stateB = stateA;

            for (var i = 0; i < samples; i++) first[i] = FeatureHash.Range(ref stateA, 24, 40);
            for (var i = 0; i < samples; i++) second[i] = FeatureHash.Range(ref stateB, 24, 40);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void DrawsStayInsideTheAuthoredRange()
        {
            // Validation proves footprint containment over the declared range, so a draw outside
            // it would invalidate a proof the evaluator relies on rather than re-checking.
            var spec = new ParameterSpec { Min = 24, Max = 40, Quantum = 4 };
            ulong state = 12345ul;

            for (var i = 0; i < 2000; i++)
            {
                int value = spec.Clamp(FeatureHash.Range(ref state, spec.Min, spec.Max));

                Assert.GreaterOrEqual(value, spec.Min);
                Assert.LessOrEqual(value, spec.Max);
                Assert.AreEqual(0, (value - spec.Min) % spec.Quantum, "draw ignored the quantum");
            }
        }

        [Test]
        public void NeighbouringPositionsDoNotProduceCorrelatedDraws()
        {
            // A hash that correlates across adjacent positions builds rows of identical houses.
            int identical = 0;

            for (var i = 0; i < 64; i++)
            {
                ulong a = FeatureGeneration.InstanceSeed(5u, 0, new int3(i, 0, 0));
                ulong b = FeatureGeneration.InstanceSeed(5u, 0, new int3(i + 1, 0, 0));

                if (FeatureHash.Range(ref a, 0, 15) == FeatureHash.Range(ref b, 0, 15)) identical++;
            }

            // Roughly a sixteenth should collide by chance; anything near all of them is a broken
            // hash rather than bad luck.
            Assert.Less(identical, 20, "adjacent positions draw the same values — the hash correlates");
        }
    }
}
