using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpatialActivationTests
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
        public void SpatialActivationSeamOwnsPlanningBuildCommitAndInteractionGeometry()
        {
            string seam = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan()", seam);
            StringAssert.Contains("PlanCastleSpatial(", seam);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", seam);
            StringAssert.Contains("StructuresComposition.BeginCastleBuild(", seam);
            StringAssert.Contains("CommitPendingCastleSpatialPlan()", seam);
            StringAssert.Contains("CastleSpatialProjection.Create(", seam);
            StringAssert.Contains("BuildCastlePresentationLights(in presentationPlan)", seam);
            StringAssert.Contains("PrimaryGateGeometry", seam);
            StringAssert.Contains("geometry.WorldVoxel(w, h, d)", seam);
            StringAssert.Contains("ActiveCastleTrapdoorCentre()", seam);
        }

        [Test]
        public void MainShowcaseRemainsLegacyUntilAllActivationCallsSwitchTogether()
        {
            string world = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));

            // This is intentionally an all-at-once activation boundary. Building a rotated gate
            // while interaction still clears CastleLayout.FrontGateMinimum would create an
            // invisible/unclickable door, so the main world must remain entirely legacy until the
            // four seam calls below are wired in the same patch.
            StringAssert.DoesNotContain("PreparePendingCastleSpatialPlan();", world);
            StringAssert.DoesNotContain("BeginPendingSpatialCastleBuild(", world);
            StringAssert.DoesNotContain("CommitPendingCastleSpatialPlan();", world);
            StringAssert.Contains("CastleLayout.FrontGateMinimum", world);
            StringAssert.Contains("CastleLayout.TrapdoorCentre", world);
        }
    }
}
