using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepOrchestratorBoundaryTests
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
        public void CompatibilityKeepRealizerOnlySequencesDedicatedComponents()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("CastleKeepShellRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepTurretRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFloorRealizer.BuildCompatibility(", keep);
            StringAssert.Contains("CastleKeepCompatibilityCirculationRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFenestrationRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFacadeRealizer.Build(", keep);
            StringAssert.Contains("CastleRearOrielRealizer.Build(", keep);

            StringAssert.DoesNotContain("brush.", keep,
                "The keep orchestrator must sequence realization, not own voxel geometry.");
            StringAssert.DoesNotContain("private static void Build", keep,
                "Geometry helpers belong in dedicated keep components, not the stage orchestrator.");
            StringAssert.DoesNotContain("new Random(", keep);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive", keep);
        }

        [Test]
        public void PlannedKeepRealizerOnlySequencesFrozenKeepComponents()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepRealizer.cs"));

            StringAssert.Contains("CastleSpatialProjection.ActualKeepCentre(", keep,
                "Spatial keep coordinates must come from the shared projection rather than a local compatibility offset.");
            StringAssert.Contains("CastleKeepShellRealizer.Build(", keep);
            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", keep);
            StringAssert.Contains("CastleKeepFloorRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", keep);
            StringAssert.Contains("CastlePlannedKeepWindowRealizer.BuildAll(", keep);
            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", keep);
            StringAssert.Contains("CastlePlannedKeepAnnexRealizer.Build(", keep);

            StringAssert.DoesNotContain("CastleLayout.LegacyKeepCentreZOffset", keep,
                "The temporary compatibility anchor belongs in CastleSpatialProjection, not planned Runtime code.");
            StringAssert.DoesNotContain("CastleKeepWindowRealizer.Build(", keep,
                "Spatial keep realization must not route through the compatibility window adapter.");
            StringAssert.DoesNotContain("brush.", keep,
                "The planned keep orchestrator must sequence realization, not own voxel geometry.");
            StringAssert.DoesNotContain("new Random(", keep);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive", keep);
        }
    }
}
