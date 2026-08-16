using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepExteriorDedupBoundaryTests
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
        public void PlannedKeepExteriorDelegatesSharedFacadeRecipe()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string planned = File.ReadAllText(Path.Combine(
                runtime, "CastlePlannedKeepExteriorRealizer.cs"));
            string facade = File.ReadAllText(Path.Combine(
                runtime, "CastleKeepFacadeRealizer.cs"));

            StringAssert.Contains("CastleKeepFacadeRealizer.Build(", planned);
            StringAssert.DoesNotContain("private static void BuildFacade(", planned);
            StringAssert.DoesNotContain("Mat.Cloth", planned,
                "Planned exterior must not duplicate facade voxel dressing.");
            StringAssert.DoesNotContain("Mat.Moss", planned,
                "Planned exterior must not duplicate facade weathering.");

            StringAssert.Contains("Mat.Cloth", facade);
            StringAssert.Contains("Mat.Moss", facade);
        }
    }
}
