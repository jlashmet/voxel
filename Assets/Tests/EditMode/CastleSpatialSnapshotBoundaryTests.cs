using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialSnapshotBoundaryTests
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
        public void CompositionDetachesCompletedCastleBeforeRuntimeHandoff()
        {
            string terrainPlanning = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "CastleTerrainPlanning.cs"));
            string snapshot = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanSnapshot.cs"));

            StringAssert.Contains("CastleSpatialPlanSnapshot.CloneRuntimeReady(", terrainPlanning);
            StringAssert.Contains("CastleSpatialPlanCompletion.CompleteResolved(", terrainPlanning);
            StringAssert.Contains("CloneDetached", snapshot);
            StringAssert.Contains("CastleSpatialBuildReadiness.TryValidate(", snapshot);
            StringAssert.Contains("source.Dungeon", snapshot);
            StringAssert.Contains("source.Cave", snapshot);
            StringAssert.Contains("source.Landscape", snapshot);
        }
    }
}
