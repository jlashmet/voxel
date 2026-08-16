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
            string exterior = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepExteriorRealizer.cs"));
            string annex = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepAnnexRealizer.cs"));
            string windows = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepWindowRealizer.cs"));
            string readiness = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));

            StringAssert.Contains("CastleSpatialProjection.KeepMinimum(", keep,
                "Planned shell/floor bounds must come from the shared projection.");
            StringAssert.Contains("CastleSpatialProjection.KeepSize(", keep,
                "Planned shell/floor size must come from the shared projection.");
            StringAssert.Contains("CastleKeepShellRealizer.Build(", keep);
            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", keep);
            StringAssert.Contains("CastleKeepFloorRealizer.BuildPlanned(", keep);
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", keep);
            StringAssert.Contains("CastlePlannedKeepWindowRealizer.BuildAll(", keep);
            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepAnnexRealizer.BuildPlanned(", keep);
            StringAssert.DoesNotContain("CastlePlannedKeepAnnexRealizer", keep,
                "Planned keep sequencing should call the real annex component directly.");

            StringAssert.Contains("CastleSpatialProjection.KeepMinimum(", exterior,
                "Planned exterior bounds must use the same projection as shell/floors.");
            StringAssert.Contains("CastleSpatialProjection.KeepSize(", exterior,
                "Planned exterior size must use the same projection as shell/floors.");
            StringAssert.DoesNotContain("CastleKeepAnnexPlanValidator", exterior,
                "Spatial preflight owns annex validation; exterior realization should only consume the admitted plan.");
            StringAssert.Contains("CastleSpatialProjection.KeepMinimum(", annex,
                "Shared annex geometry must use the projected keep bounds on both compatibility and spatial paths.");
            StringAssert.Contains("CastleSpatialProjection.KeepSize(", annex,
                "Shared annex geometry must use projected keep dimensions on all realization paths.");
            StringAssert.Contains("CastleKeepWindowPlanValidator.TryValidate(", readiness,
                "Spatial preflight must own planned keep-window admission.");
            StringAssert.DoesNotContain("CastleKeepWindowPlanValidator", windows,
                "Planned keep-window realization should consume an already-admitted plan.");
            StringAssert.DoesNotContain("throw new ", windows,
                "Planned keep-window realization should not repeat preflight validation.");
            StringAssert.Contains("brush.Arch(", windows,
                "Planned keep-window realization must retain its voxel geometry responsibility.");
            StringAssert.DoesNotContain("keepPlan.KeepHalfX * 2", keep,
                "Planned keep realization must not rebuild keep dimensions locally.");
            StringAssert.DoesNotContain("plan.KeepHalfX * 2", exterior,
                "Planned exterior realization must not rebuild keep dimensions locally.");
            StringAssert.DoesNotContain("plan.KeepHalfX * 2", annex,
                "Annex realization must not rebuild keep width locally.");
            StringAssert.DoesNotContain("plan.KeepHalfZ * 2", annex,
                "Annex realization must not rebuild keep depth locally.");
            StringAssert.DoesNotContain("plan.Centre.z - hz + 60", annex,
                "Annex geometry must not reconstruct the legacy keep anchor locally.");
            StringAssert.DoesNotContain("CastleLayout.LegacyKeepCentreZOffset", keep,
                "The temporary compatibility anchor belongs in CastleSpatialProjection, not planned Runtime code.");
            StringAssert.DoesNotContain("CastleLayout.LegacyKeepCentreZOffset", exterior);
            StringAssert.DoesNotContain("CastleKeepWindowRealizer.Build(", keep,
                "Spatial keep realization must not route through the compatibility window adapter.");
            StringAssert.DoesNotContain("brush.", keep,
                "The planned keep orchestrator must sequence realization, not own voxel geometry.");
            StringAssert.DoesNotContain("new Random(", keep);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive", keep);
        }
    }
}
