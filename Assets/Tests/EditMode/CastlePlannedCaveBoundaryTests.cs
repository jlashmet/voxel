using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedCaveBoundaryTests
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
        public void PlannedCaveIsCompletedUpstreamAndRuntimeOnlyConsumesIt()
        {
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string underground = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));

            StringAssert.Contains("AttachCave(in plan, withDungeon)", completion,
                "Natural cave topology must be completed before Runtime sees the castle.");
            StringAssert.Contains("CastleCavePlanning.Create(in plan, spatial.Dungeon)", completion,
                "Castle-to-cave adaptation belongs in the planning/completion layer.");
            StringAssert.Contains("CavePlanSnapshot.CloneValidated(spatialPlan.Cave)", pipeline,
                "Runtime must snapshot caller-owned cave arrays at its trust boundary.");
            StringAssert.Contains("CaveRealizer.Build(ref brush, cavePlan)", underground,
                "Spatial castles must use the reusable natural-cave realizer.");
            StringAssert.Contains("CastlePlannedCaveDecorator.Build(ref brush, cavePlan)", underground,
                "Castle-specific cave dressing must consume planned chamber geometry after carving.");
            StringAssert.DoesNotContain("CastleCaveRealizer.Build(", underground,
                "The spatial path must not fall back to the legacy fixed-topology cave recipe.");

            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string runtime = File.ReadAllText(file);
                StringAssert.DoesNotContain("CavePlanner.Create(", runtime,
                    $"{Path.GetFileName(file)} must consume CavePlan rather than plan natural topology.");
                StringAssert.DoesNotContain("CastleCavePlanning.Create(", runtime,
                    $"{Path.GetFileName(file)} must not adapt castle semantics into cave topology during realization.");
            }
        }
    }
}
