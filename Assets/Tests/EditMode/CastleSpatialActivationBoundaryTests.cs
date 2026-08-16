using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialActivationBoundaryTests
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
        public void RuntimeUsesApiProjectionForLegacyKeepAnchor()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleSpatialProjection.Create(", pipeline);
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);

            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain("CastleKeepPlacementAdapter", source,
                    $"{Path.GetFileName(file)} must use the shared API projection.");
                StringAssert.DoesNotContain("LegacyKeepCentreZOffset", source,
                    $"{Path.GetFileName(file)} must not redeclare or consume the migration anchor.");
            }
        }

        [Test]
        public void ShowcaseUsesResolvedSpatialProjectionForBuildInteractionAndPresentation()
        {
            string showcase = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));
            string activation = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));

            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", activation);
            StringAssert.Contains("_castleSpatialProjection = _plannedCastle.Projection", activation);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.BuildPresentationLights(", activation);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.PrimaryGateLeafVoxels(", activation);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.TrapdoorCentre(", activation);

            StringAssert.Contains("ActiveCastleFrontGatePosition()", showcase);
            StringAssert.Contains("OpenActiveCastleFrontGate();", showcase);
            StringAssert.Contains("ActiveCastleTrapdoorPosition()", showcase);
            StringAssert.Contains("OpenActiveCastleTrapdoor();", showcase);
            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", showcase,
                "Showcase gate interaction must follow the realized spatial gate.");
            StringAssert.DoesNotContain("BuildCastlePresentationLights(in plan);", showcase,
                "The legacy light recipe must not be reactivated after spatial commit.");

            StringAssert.Contains("projection.PrimaryGateGeometry", layout);
            StringAssert.Contains("projection.TrapdoorCentre", layout);
            StringAssert.Contains("projection.KeepCentreWorld", layout);
        }
    }
}
