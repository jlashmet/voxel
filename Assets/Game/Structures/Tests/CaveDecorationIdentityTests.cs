using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class CaveDecorationIdentityTests
    {
        [Test]
        public void NaturalSlotIdsAreUniqueAcrossKindsAndOrdinals()
        {
            AssertUniqueNatural(4);
        }

        [Test]
        public void MineSlotIdsAreUniqueAcrossKindsAndOrdinals()
        {
            AssertUniqueMine(4);
        }

        private static void AssertUniqueNatural(int ordinals)
        {
            var ids = new uint[NaturalCaveDecorationCatalog.KindCount * ordinals];
            int output = 0;
            for (int kind = 0; kind < NaturalCaveDecorationCatalog.KindCount; kind++)
            for (int ordinal = 0; ordinal < ordinals; ordinal++)
                ids[output++] = NaturalCaveDecorationCatalog.SlotId(
                    (NaturalCaveDecorationKind)kind, ordinal);
            AssertUnique(ids, "natural cave");
        }

        private static void AssertUniqueMine(int ordinals)
        {
            var ids = new uint[MineCaveDecorationCatalog.KindCount * ordinals];
            int output = 0;
            for (int kind = 0; kind < MineCaveDecorationCatalog.KindCount; kind++)
            for (int ordinal = 0; ordinal < ordinals; ordinal++)
                ids[output++] = MineCaveDecorationCatalog.SlotId(
                    (MineCaveDecorationKind)kind, ordinal);
            AssertUnique(ids, "mine cave");
        }

        private static void AssertUnique(uint[] ids, string label)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreNotEqual(0u, ids[i], $"{label} slot {i} was zero.");
                for (int j = i + 1; j < ids.Length; j++)
                    Assert.AreNotEqual(ids[i], ids[j],
                        $"{label} slot IDs collided at indices {i}/{j}.");
            }
        }
    }
}
