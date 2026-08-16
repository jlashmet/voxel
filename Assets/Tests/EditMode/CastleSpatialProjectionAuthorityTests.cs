using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialProjectionAuthorityTests
    {
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

        [Test]
        public void StructuresApiHasSingleLegacyKeepProjectionAuthority()
        {
            string apiDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api");
            int offsetAuthorities = 0;

            foreach (string file in Directory.GetFiles(apiDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain(
                    "CastleSpatialLayoutProjector",
                    source,
                    $"{Path.GetFileName(file)} reintroduced a second spatial castle projection.");

                if (source.Contains("LegacyKeepCentreZOffset"))
                    offsetAuthorities++;
            }

            Assert.AreEqual(
                1,
                offsetAuthorities,
                "CastleSpatialProjection must be the only Structures.Api authority for the legacy +60 keep anchor.");
        }
    }
}
