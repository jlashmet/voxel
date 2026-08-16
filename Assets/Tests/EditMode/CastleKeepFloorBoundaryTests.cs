using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFloorBoundaryTests
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
        public void CompatibilityKeepDoesNotAcceptSemanticFloorPlans()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string floors = File.ReadAllText(Path.Combine(runtime, "CastleKeepFloorRealizer.cs"));

            StringAssert.Contains("CastleKeepFloorRealizer.BuildCompatibility(", keep);
            StringAssert.DoesNotContain("CastleKeepFloorPlan[]", keep,
                "Spatial floor semantics must bypass the compatibility keep dispatcher.");

            StringAssert.Contains("internal static void BuildCompatibility(", floors);
            StringAssert.Contains(
                "Planned keep floor realization requires one semantic room plan per floor.",
                floors);
        }
    }
}
