using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVegetationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void LayoutIsDeterministicAndUsesUniqueRoots()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            List<VegetationCandidate> a = KentridgeVegetationLayoutPlanner.Build(plan);
            List<VegetationCandidate> b = KentridgeVegetationLayoutPlanner.Build(
                KentridgeDefinition.Build(Seed));

            Assert.AreEqual(38, a.Count,
                "Kentridge should have a sparse authored tree layer with every residential role represented.");
            Assert.AreEqual(a.Count, b.Count);

            var roots = new HashSet<long>();
            for (int i = 0; i < a.Count; i++)
            {
                VegetationCandidate left = a[i];
                VegetationCandidate right = b[i];

                Assert.AreEqual(i, left.Ordinal,
                    "Vegetation ordinals are stable identity input and should remain contiguous.");
                Assert.AreEqual(left.X, right.X);
                Assert.AreEqual(left.Z, right.Z);
                Assert.AreEqual(left.HeightUnits, right.HeightUnits);
                Assert.AreEqual(left.Species, right.Species);
                Assert.AreEqual(left.HeightMode, right.HeightMode);

                long key = ((long)left.X << 32) ^ (uint)left.Z;
                Assert.IsTrue(roots.Add(key),
                    $"Duplicate Kentridge tree root at {left.X},{left.Z}.");
            }
        }

        [Test]
        public void LayoutCarriesDistrictAndWildernessVocabulary()
        {
            List<VegetationCandidate> trees =
                KentridgeVegetationLayoutPlanner.Build(KentridgeDefinition.Build(Seed));

            int pine = 0;
            int oak = 0;
            int maple = 0;
            int birch = 0;
            int dead = 0;

            for (int i = 0; i < trees.Count; i++)
            {
                switch (trees[i].Species)
                {
                    case SemanticTreeSpecies.Pine: pine++; break;
                    case SemanticTreeSpecies.Oak: oak++; break;
                    case SemanticTreeSpecies.Maple: maple++; break;
                    case SemanticTreeSpecies.Birch: birch++; break;
                    case SemanticTreeSpecies.Dead: dead++; break;
                }
            }

            Assert.GreaterOrEqual(pine, 8,
                "The perimeter transition should read as wilderness from a distance.");
            Assert.GreaterOrEqual(oak, 6,
                "Residential/civic districts need mature broadleaf silhouettes.");
            Assert.GreaterOrEqual(maple, 4,
                "Market and noble planting should remain visually distinct.");
            Assert.GreaterOrEqual(birch, 4);
            Assert.AreEqual(1, dead,
                "The abandoned-house yard intentionally owns the one dead specimen tree.");
        }
    }
}
