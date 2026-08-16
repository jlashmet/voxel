using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCompatibilityBoundaryTests
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
        public void CompatibilityKeepUsesExplicitFloorEntryPoint()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string floors = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepFloorRealizer.cs"));

            StringAssert.Contains("CastleKeepFloorRealizer.BuildCompatibility(", keep);
            StringAssert.DoesNotContain(
                "CastleKeepFloorRealizer.Build(\n                        ref brush, in plan, min, size, baseY, floors, null)",
                keep,
                "Compatibility realization must not encode its contract as a null planned-floor array.");
            StringAssert.Contains("internal static void BuildCompatibility(", floors);
            StringAssert.Contains("internal static void Build(", floors);
        }
    }
}
