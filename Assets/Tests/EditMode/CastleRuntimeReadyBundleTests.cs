using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRuntimeReadyBundleTests
    {
        [Test]
        public void PlannedBundlesAreRuntimeReadyAndBoundsContainPlannedLandscape()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                uint terrainSeed = seed ^ 0x71A5u;
                PlannedCastleBuild planned = StructuresComposition.PlanCastleBuild(
                    new int3(256, 220, 376), seed, terrainSeed);
                CastlePlan plan = planned.Dimensions;
                CastleSpatialPlan spatial = planned.Spatial;
                CastleGatehousePlan gatehouse = planned.Gatehouse;

                Assert.NotNull(spatial, $"seed {seed}: missing spatial plan");
                Assert.IsFalse(spatial.KeepRequiresTerrainResolution,
                    $"seed {seed}: runtime-ready bundle retained unresolved terrain placement");

                Assert.IsTrue(
                    CastleGatehousePlanValidator.TryValidate(
                        in gatehouse, out CastleGatehousePlanIssue gatehouseIssue),
                    $"seed {seed}: invalid frozen gatehouse recipe: {gatehouseIssue}");

                CastleBuildPreflightResult preflight = CastleBuildPreflight.EvaluateRuntimeReady(
                    in plan, spatial, long.MaxValue);
                Assert.IsTrue(preflight.IsValid,
                    $"seed {seed}: runtime-ready preflight failed: " +
                    $"{preflight.Issue}/{preflight.SpatialPlanIssue}/{preflight.ReadinessIssue}");

                CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
                CastleSpatialProjection projection = planned.Projection;
                Assert.IsTrue(bounds.Contains(projection.TrapdoorCentre),
                    $"seed {seed}: projected trapdoor escaped dependency bounds");

                CastleGateGeometry gate = projection.PrimaryGate;
                Assert.IsTrue(bounds.Contains(gate.Origin),
                    $"seed {seed}: primary gate origin escaped dependency bounds");
                Assert.IsTrue(bounds.Contains(gate.WorldVoxel(
                        gate.Width - 1, gate.Height - 1, gate.Depth - 1)),
                    $"seed {seed}: primary gate far corner escaped dependency bounds");

                CastleLandscapePlan landscape = spatial.Landscape;
                Assert.NotNull(landscape, $"seed {seed}: missing planned landscape");
                CastleLandscapeDecorationSpec[] decorations = landscape.Decorations;
                for (int i = 0; i < decorations.Length; i++)
                {
                    CastleLandscapeDecorationSpec decoration = decorations[i];
                    int worldX = plan.Centre.x + decoration.Centre.x;
                    int worldZ = plan.Centre.z + decoration.Centre.y;

                    switch (decoration.Kind)
                    {
                        case CastleLandscapeDecorationKind.PerimeterStoneRubble:
                        case CastleLandscapeDecorationKind.PerimeterDarkStoneRubble:
                            AssertHorizontalContains(
                                in bounds,
                                worldX,
                                worldX + decoration.Size.x - 1,
                                worldZ,
                                worldZ + decoration.Size.z - 1,
                                seed,
                                decoration.Id);
                            break;

                        default:
                            AssertHorizontalContains(
                                in bounds,
                                worldX - decoration.Radius,
                                worldX + decoration.Radius,
                                worldZ - decoration.Radius,
                                worldZ + decoration.Radius,
                                seed,
                                decoration.Id);
                            break;
                    }
                }
            }
        }

        private static void AssertHorizontalContains(
            in CastleBuildBounds bounds,
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            uint seed,
            int decorationId)
        {
            Assert.GreaterOrEqual(minX, bounds.Min.x,
                $"seed {seed}: landscape decoration {decorationId} escaped min X bound");
            Assert.Less(maxX, bounds.MaxExclusive.x,
                $"seed {seed}: landscape decoration {decorationId} escaped max X bound");
            Assert.GreaterOrEqual(minZ, bounds.Min.z,
                $"seed {seed}: landscape decoration {decorationId} escaped min Z bound");
            Assert.Less(maxZ, bounds.MaxExclusive.z,
                $"seed {seed}: landscape decoration {decorationId} escaped max Z bound");
        }
    }
}
