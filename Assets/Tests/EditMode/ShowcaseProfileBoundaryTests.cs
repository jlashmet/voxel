using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseProfileBoundaryTests
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
        public void ShowcaseProfileReadPathDoesNotLeakStructuresRuntimeType()
        {
            string showcaseWorldPath = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "ShowcaseWorld.cs");
            string voxelShowcasePath = Path.Combine(
                RepoRoot, "Assets", "Scenes", "Showcase", "VoxelShowcase.cs");

            Assert.IsTrue(File.Exists(showcaseWorldPath), "Missing Composition ShowcaseWorld source.");
            Assert.IsTrue(File.Exists(voxelShowcasePath), "Missing Showcase scene source.");

            string showcaseWorld = File.ReadAllText(showcaseWorldPath);
            string voxelShowcase = File.ReadAllText(voxelShowcasePath);

            StringAssert.Contains(
                "public IProfileBlockReadSource ProfileBlocks => _profileBlocks;",
                showcaseWorld,
                "ShowcaseWorld must expose the profile read capability through Storage.Api.");
            StringAssert.DoesNotContain(
                "public ProfileBlockStore ProfileBlocks",
                showcaseWorld,
                "Composition must not leak the concrete profile store through its Showcase-facing API.");
            StringAssert.DoesNotContain(
                "VoxelEngine.Structures.Runtime",
                voxelShowcase,
                "Showcase scene code must not acquire a Structures.Runtime dependency to consume profile blocks.");
            StringAssert.DoesNotContain(
                "ProfileBlockStore",
                voxelShowcase,
                "Showcase scene code must consume the Storage.Api profile read capability, not the concrete store type.");
        }
    }
}
