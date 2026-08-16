using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveSeedTests
    {
        private const uint CaveRandomElementId = 0x43415645u;

        [Test]
        public void CaveElementSeedCannotCollapseToZeroAtLegacyXorCollision()
        {
            const uint legacyCollisionSeed = 0xCAFEu;
            uint caveSeed = CastleSeedPartition.Derive(
                legacyCollisionSeed,
                CastleSeedDomain.Dungeon,
                CaveRandomElementId);

            Assert.AreNotEqual(0u, caveSeed);
            Assert.AreEqual(
                caveSeed,
                CastleSeedPartition.Derive(
                    legacyCollisionSeed,
                    CastleSeedDomain.Dungeon,
                    CaveRandomElementId),
                "Cave seed must remain deterministic for the same castle seed.");
        }

        [Test]
        public void CaveRealizerUsesPartitionedDungeonElementStream()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Structures", "Runtime", "CastleCaveRealizer.cs"));

            StringAssert.Contains("CastleSeedPartition.Derive(", source);
            StringAssert.Contains("CastleSeedDomain.Dungeon", source);
            StringAssert.Contains("CaveRandomElementId", source);
            StringAssert.DoesNotContain("plan.Seed ^ 0xCAFE", source,
                "Ad-hoc XOR seeding can produce Unity.Mathematics.Random seed zero.");
        }

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }
    }
}
