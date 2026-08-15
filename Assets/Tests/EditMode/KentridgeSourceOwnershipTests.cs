using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeSourceOwnershipTests
    {
        private static string RepoRoot
        {
            get
            {
                var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                    directory = directory.Parent;

                Assert.NotNull(directory, "Could not locate project root containing Packages/.");
                return directory.FullName;
            }
        }

        [Test]
        public void ArchitecturalGrammarsLiveOnlyInArchitectureOwnedUnit()
        {
            string runtimeRoot = Path.Combine(
                RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime");
            string contentRoot = Path.Combine(runtimeRoot, "Content", "Kentridge");
            string architectureRoot = Path.Combine(runtimeRoot, "Architecture", "Kentridge");
            string[] grammarFiles =
            {
                "KentridgeBuildingGrammar.cs",
                "KentridgeUrbanFabricGrammar.cs",
            };

            Assert.IsTrue(Directory.Exists(contentRoot), "Missing Kentridge content root: " + contentRoot);
            Assert.IsTrue(Directory.Exists(architectureRoot), "Missing Kentridge architecture root: " + architectureRoot);

            foreach (string grammarFile in grammarFiles)
            {
                string contentPath = Path.Combine(contentRoot, grammarFile);
                string architecturePath = Path.Combine(architectureRoot, grammarFile);

                Assert.IsFalse(
                    File.Exists(contentPath),
                    "Architecture detail must not leave migration markers or duplicate grammar files in " +
                    "the high-level Kentridge content unit: " + grammarFile);
                Assert.IsTrue(
                    File.Exists(architecturePath),
                    "Kentridge architectural detail must be physically owned by the Architecture unit: " +
                    grammarFile);
            }
        }
    }
}
