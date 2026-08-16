using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFenestrationBoundaryTests
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
        public void KeepDelegatesFenestrationToDedicatedRealizer()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string windows = File.ReadAllText(Path.Combine(runtime, "CastleKeepFenestrationRealizer.cs"));

            StringAssert.Contains("CastleKeepFenestrationRealizer.Build(", keep);
            StringAssert.DoesNotContain("private static void BuildWindows", keep);
            StringAssert.Contains("brush.Arch(", windows);
            StringAssert.Contains("Mat.LitWindow", windows);
        }
    }
}
