using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSiteSeedTests
    {
        private const uint SiteRandomElementId = 0x53495445u;

        [Test]
        public void SiteElementSeedCannotCollapseToZeroAtLegacyXorCollision()
        {
            const uint legacyCollisionSeed = 0x51E5u;
            uint siteSeed = CastleSeedPartition.Derive(
                legacyCollisionSeed,
                CastleSeedDomain.Decor,
                SiteRandomElementId);

            Assert.AreNotEqual(0u, siteSeed);
            Assert.AreEqual(
                siteSeed,
                CastleSeedPartition.Derive(
                    legacyCollisionSeed,
                    CastleSeedDomain.Decor,
                    SiteRandomElementId));
        }

        [Test]
        public void SiteRealizerUsesPartitionedDecorElementStream()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Structures", "Runtime", "CastleSiteRealizer.cs"));

            StringAssert.Contains("CastleSeedPartition.Derive(", source);
            StringAssert.Contains("CastleSeedDomain.Decor", source);
            StringAssert.Contains("SiteRandomElementId", source);
            StringAssert.DoesNotContain("plan.Seed ^ 0x51E5", source,
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
