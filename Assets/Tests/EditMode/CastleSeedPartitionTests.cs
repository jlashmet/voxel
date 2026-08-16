using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSeedPartitionTests
    {
        [Test]
        public void SemanticDomainsHaveStableKnownSeeds()
        {
            const uint rootSeed = 12345u;

            Assert.AreEqual(0xE4D1C4C4u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Layout));
            Assert.AreEqual(0xCDBAC842u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Walls));
            Assert.AreEqual(0xAFFF5A53u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Keep));
            Assert.AreEqual(0x3F0B34B5u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Rooms));
            Assert.AreEqual(0xBFA22EC6u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Dungeon));
            Assert.AreEqual(0xCD7427B5u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Cave));
            Assert.AreEqual(0x158C0FD2u,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Decor));
        }

        [Test]
        public void ElementSeedsAreStableAndIndependentWithinDomain()
        {
            const uint rootSeed = 12345u;

            uint first = CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Walls, 0u);
            uint second = CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Walls, 1u);

            Assert.AreEqual(0x3E93A1FCu, first);
            Assert.AreEqual(0x48AE1695u, second);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first,
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Walls, 0u));
        }

        [Test]
        public void DerivedSeedsAreNonZeroForZeroAndTypicalRootSeeds()
        {
            CastleSeedDomain[] domains =
            {
                CastleSeedDomain.Layout,
                CastleSeedDomain.Walls,
                CastleSeedDomain.Keep,
                CastleSeedDomain.Rooms,
                CastleSeedDomain.Dungeon,
                CastleSeedDomain.Cave,
                CastleSeedDomain.Decor,
            };

            for (uint rootSeed = 0; rootSeed <= 256; rootSeed++)
            {
                foreach (CastleSeedDomain domain in domains)
                {
                    Assert.AreNotEqual(0u, CastleSeedPartition.Derive(rootSeed, domain),
                        $"root {rootSeed}, domain {domain}");
                }
            }
        }

        [Test]
        public void AddingElementStreamsDoesNotConsumeOrChangeDomainSeed()
        {
            const uint rootSeed = 9127u;
            uint before = CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Dungeon);

            for (uint elementId = 0; elementId < 128; elementId++)
                CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Dungeon, elementId);

            uint after = CastleSeedPartition.Derive(rootSeed, CastleSeedDomain.Dungeon);
            Assert.AreEqual(before, after);
        }
    }
}
