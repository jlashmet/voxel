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
        public void SpatialActivationSeamOwnsRuntimeReadyBundleBuildCommitAndInteractionGeometry()
        {
            string seam = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan()", seam);
            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", seam);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", seam);
            StringAssert.Contains("StructuresComposition.BeginCastleBuild(", seam);
            StringAssert.Contains("CommitPendingCastleSpatialPlan()", seam);
            StringAssert.Contains("_castleSpatialProjection = _plannedCastle.Projection;", seam);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.BuildPresentationLights(", seam);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.PrimaryGateInteractionPosition(", seam);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.PrimaryGateLeafVoxels(", seam);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.TrapdoorCentre(", seam);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.TrapdoorInteractionPosition(", seam);

            StringAssert.DoesNotContain("PlanCastleSpatial(", seam,
                "The showcase should consume the runtime-ready CastleBuildPlan bundle instead of reassembling planning passes.");
            StringAssert.DoesNotContain("CastleSpatialProjection.Create(", seam,
                "The runtime-ready bundle owns its validated projection; the showcase should not recreate it.");
            StringAssert.DoesNotContain("CastleGateGeometryResolver.LegacyFront", seam,
                "Activated interaction must not fall back to the historical fixed -Z gate.");
            StringAssert.DoesNotContain("CastleLayout.TrapdoorCentre", seam,
                "Activated interaction must not fall back to the unprojected keep hatch.");
            StringAssert.DoesNotContain("_hasCastleSpatialProjection", seam,
                "The completed showcase castle always has its committed spatial projection.");
        }

        [Test]
        public void SpatialBuildAdmissionQueuesFullThreeDimensionalDependencyBounds()
        {
            string seam = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string regionRange = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleDependencyRegionRange.cs"));

            StringAssert.Contains("CastleBuildBoundsResolver.Resolve(", seam);
            StringAssert.Contains("ShowcaseCastleDependencyRegionRange.FromCastleBounds", seam);
            StringAssert.Contains(
                "for (int ry = regionRange.Min.y; ry <= regionRange.MaxInclusive.y; ry++)",
                seam,
                "Upper and underground castle layers must be generated before voxel mutation begins.");
            StringAssert.Contains("QueuePendingCastleDependencyRegions();", seam);
            StringAssert.Contains("DependencyGatedCastleBuildSession", seam);
            StringAssert.Contains("PendingCastleDependenciesReady()", seam);
            StringAssert.Contains("public bool IsComplete => _inner == null || _inner.IsComplete", seam,
                "The pre-build gate must stay quiescent so terrain streaming can satisfy dependencies.");

            StringAssert.Contains("int shift = VoxelDimensions.RegionVoxelEdgeLog2;", regionRange);
            StringAssert.Contains("min >> shift", regionRange);
            StringAssert.Contains("(maxExclusive - 1) >> shift", regionRange,
                "Half-open bounds ending exactly on a region boundary must not queue the next region.");
        }

        [Test]
        public void MainShowcaseActivatesSpatialCastleAtomically()
        {
            string world = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan();", world);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", world);
            StringAssert.Contains("CommitPendingCastleSpatialPlan();", world);
            StringAssert.Contains("return ActiveCastleFrontGatePosition();", world);
            StringAssert.Contains("OpenActiveCastleFrontGate();", world);
            StringAssert.Contains("return ActiveCastleTrapdoorPosition();", world);
            StringAssert.Contains("OpenActiveCastleTrapdoor();", world);

            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", world,
                "The activated showcase must not interact with the historical fixed -Z gate.");
            StringAssert.DoesNotContain("CastleLayout.TrapdoorCentre", world,
                "The activated showcase must not derive its hatch from the unprojected CastlePlan.");
        }
    }
}
