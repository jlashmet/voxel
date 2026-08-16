using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSiteBuildBoundsTests
    {
        [Test]
        public void CastleBoundsFollowFrozenRiverOffsetAndMeanderEnvelope()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 173u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(173u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;

            CastleSitePlan originalSite = topology.Site;
            CastleSiteGeometryPlan original = originalSite.Geometry;
            CastleRiverCrossSectionPlan crossSection = original.RiverCrossSection;
            var expandedGeometry = new CastleSiteGeometryPlan(
                original.EdgeFrequencyA,
                original.EdgeAmplitudeA,
                original.EdgeFrequencyB,
                original.EdgeAmplitudeB,
                original.EdgeFrequencyC,
                original.EdgeAmplitudeC,
                original.CliffFalloffExponent,
                original.CliffNoiseAngularFrequency,
                original.CliffNoiseProgressFrequency,
                original.CliffNoiseAmplitude,
                original.CliffGroundInset,
                original.GrassEdgeInset,
                original.ApproachReachInset,
                riverOffset: 1200,
                original.RiverHalfWidth,
                original.WaterHalfWidth,
                original.RiverDepth,
                original.MeanderFrequencyA,
                original.MeanderAmplitudeA,
                original.MeanderFrequencyB,
                original.MeanderAmplitudeB,
                in crossSection);
            topology.Site = new CastleSitePlan(
                originalSite.GrassPatternSeed,
                originalSite.GrassCoveragePercent,
                originalSite.CourtyardPatternSeed,
                originalSite.CourtyardStonePercent,
                in expandedGeometry);

            Assert.IsTrue(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue topologyIssue),
                topologyIssue.ToString());

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleGatehousePlanCompletion.Attach(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            int tangentReach = plan.PlateauRadius + plan.CliffDrop
                             - expandedGeometry.ApproachReachInset;
            float outwardReach = plan.WallThickness
                                + expandedGeometry.RiverOffset
                                + expandedGeometry.MeanderAmplitudeA
                                + expandedGeometry.MeanderAmplitudeB
                                + expandedGeometry.RiverHalfWidth;
            int2 farLocal = projection.Approach.LocalPoint(tangentReach, outwardReach);
            var farRiver = new int3(
                plan.Centre.x + farLocal.x,
                plan.Centre.y + plan.PlateauHeight - expandedGeometry.RiverDepth,
                plan.Centre.z + farLocal.y);

            Assert.IsTrue(bounds.Contains(farRiver),
                "Castle dependency bounds stopped at the historical river-offset safety halo.");
        }
    }
}
