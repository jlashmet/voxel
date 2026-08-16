using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepExteriorOwnershipTests
    {
        [Test]
        public void SpatialKeepExteriorOwnsRearOrielExactlyOnce()
        {
            string pipeline = ReadRuntime("CastleBuildPipeline.cs");
            string exterior = ReadRuntime("CastlePlannedKeepExteriorRealizer.cs");
            string annex = ReadRuntime("CastlePlannedKeepAnnexRealizer.cs");
            string keep = ReadRuntime("CastleKeepRealizer.cs");

            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", pipeline,
                "Spatial keep stage 6 must route through the extracted exterior realizer.");
            StringAssert.Contains("annexes.HasRearOriel", exterior);
            StringAssert.Contains("CastleRearOrielRealizer.Build(", exterior);

            StringAssert.DoesNotContain("CastleRearOrielRealizer.Build(", annex,
                "The planned annex stage runs after the exterior stage and must not emit the oriel twice.");

            StringAssert.Contains("if (roomPlans == null)", keep,
                "The dimension-only compatibility path must retain its historical oriel.");
            StringAssert.Contains("CastleRearOrielRealizer.Build(", keep);
        }

        private static string ReadRuntime(string file) => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", file));

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
