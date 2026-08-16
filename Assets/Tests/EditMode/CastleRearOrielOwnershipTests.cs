using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRearOrielOwnershipTests
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
        public void PlannedKeepExteriorDelegatesRearOrielToSingleGeometryOwner()
        {
            string exterior = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepExteriorRealizer.cs"));
            string oriel = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRearOrielRealizer.cs"));

            StringAssert.Contains("CastleRearOrielRealizer.Build(ref brush, in plan)", exterior);
            StringAssert.DoesNotContain("private static void BuildRearOriel", exterior,
                "Rear-oriel voxel geometry must have one Runtime owner.");
            StringAssert.Contains("internal static void Build", oriel);
        }
    }
}
