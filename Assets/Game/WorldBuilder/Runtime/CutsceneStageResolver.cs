using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Resolves semantic stage-region requirements into deterministic positions. It never contains
    /// cutscene-specific point names or site-specific offsets.
    /// </summary>
    public static class ProceduralCutsceneStageResolver
    {
        private sealed class RegionState
        {
            public int MaxClearance;
            public int OccupiedCount;
            public int NextOccupiedIndex;
        }

        public static CutsceneStageBinding Resolve(
            CutsceneStagePlan plan,
            CutsceneSiteGeometry geometry)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Definition == null)
                throw new InvalidOperationException("Cutscene stage plan has no authored definition.");

            var regions = BuildRegionState(plan, plan.Definition);
            var binding = new CutsceneStageBinding();

            for (var i = 0; i < plan.Requirements.Count; i++)
            {
                CutsceneStagePointRequirement requirement = plan.Requirements[i];
                if (requirement.Region == CutsceneStageRegion.Unspecified)
                    throw new InvalidOperationException(
                        "Cutscene '" + plan.Cutscene + "' stage point '" + requirement.Point +
                        "' has no procedural stage region.");

                RegionState region = regions[requirement.Region];
                ValidateClearance(plan, requirement, geometry, region.MaxClearance);

                bool occupied = IsOccupiedPoint(plan.Definition, requirement.Point);
                int lateral = 0;
                if (occupied)
                {
                    lateral = ResolveOccupiedLateralOffset(
                        region.NextOccupiedIndex,
                        region.OccupiedCount,
                        region.MaxClearance,
                        geometry.InteriorHalfWidthDecimetres);
                    region.NextOccupiedIndex++;
                }

                int inwardDistance = ResolveRegionDepth(
                    requirement.Region,
                    region.MaxClearance,
                    geometry.InteriorDepthDecimetres);

                CutsceneInt3 position = Offset(
                    geometry.EntrancePosition,
                    geometry.Inward,
                    inwardDistance,
                    geometry.Right,
                    lateral);

                CutsceneInt3 forward = ResolveFacing(
                    requirement.Facing,
                    position,
                    geometry,
                    regions);

                binding.Bind(requirement.Point, new CutsceneStagePoint(position, forward));
            }

            return binding;
        }

        private static Dictionary<CutsceneStageRegion, RegionState> BuildRegionState(
            CutsceneStagePlan plan,
            CutsceneDefinition definition)
        {
            var result = new Dictionary<CutsceneStageRegion, RegionState>();
            for (var i = 0; i < plan.Requirements.Count; i++)
            {
                CutsceneStagePointRequirement requirement = plan.Requirements[i];
                if (!result.TryGetValue(requirement.Region, out RegionState state))
                {
                    state = new RegionState();
                    result.Add(requirement.Region, state);
                }

                state.MaxClearance = Math.Max(
                    state.MaxClearance,
                    requirement.MinimumClearanceDecimetres);
                if (IsOccupiedPoint(definition, requirement.Point))
                    state.OccupiedCount++;
            }
            return result;
        }

        private static void ValidateClearance(
            CutsceneStagePlan plan,
            CutsceneStagePointRequirement requirement,
            CutsceneSiteGeometry geometry,
            int regionClearance)
        {
            if (regionClearance > geometry.InteriorHalfWidthDecimetres)
                throw new InvalidOperationException(
                    "Cutscene '" + plan.Cutscene + "' cannot stage region '" + requirement.Region +
                    "': required lateral clearance exceeds site width.");

            if (regionClearance * 2 > geometry.InteriorDepthDecimetres)
                throw new InvalidOperationException(
                    "Cutscene '" + plan.Cutscene + "' cannot stage region '" + requirement.Region +
                    "': required clearance exceeds site depth.");
        }

        private static int ResolveOccupiedLateralOffset(
            int index,
            int count,
            int clearance,
            int halfWidth)
        {
            if (count <= 1) return 0;

            int separation = Math.Max(10, clearance * 2);
            int totalSpan = separation * (count - 1);
            int availableSpan = 2 * Math.Max(0, halfWidth - clearance);
            if (totalSpan > availableSpan)
                throw new InvalidOperationException(
                    "Cutscene stage region cannot fit " + count +
                    " occupied points with the requested clearance.");

            return ((2 * index - (count - 1)) * separation) / 2;
        }

        private static int ResolveRegionDepth(
            CutsceneStageRegion region,
            int clearance,
            int depth)
        {
            int minimum = clearance;
            int maximum = depth - clearance;
            if (maximum < minimum)
                throw new InvalidOperationException("Cutscene stage region has no usable depth after clearance.");

            int desired;
            switch (region)
            {
                case CutsceneStageRegion.PublicEntrance:
                    desired = Math.Max(5, clearance);
                    break;
                case CutsceneStageRegion.EntranceApproach:
                    desired = Math.Max(depth / 3, clearance);
                    break;
                case CutsceneStageRegion.PlayerSpawnArea:
                    desired = Math.Max(depth / 4, clearance);
                    break;
                case CutsceneStageRegion.InteriorGatheringArea:
                    desired = Math.Max((depth * 2) / 3, clearance);
                    break;
                case CutsceneStageRegion.ConversationApproach:
                    desired = Math.Max((depth * 3) / 5, clearance);
                    break;
                case CutsceneStageRegion.SiteInterior:
                    desired = depth / 2;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported procedural cutscene stage region: " + region + ".");
            }

            return Math.Max(minimum, Math.Min(maximum, desired));
        }

        private static CutsceneInt3 ResolveFacing(
            CutsceneStageFacingHint hint,
            CutsceneInt3 position,
            CutsceneSiteGeometry geometry,
            Dictionary<CutsceneStageRegion, RegionState> regions)
        {
            switch (hint)
            {
                case CutsceneStageFacingHint.TowardEntrance:
                    return Negate(geometry.Inward);

                case CutsceneStageFacingHint.TowardStageCenter:
                    int clearance = 0;
                    if (regions.TryGetValue(CutsceneStageRegion.InteriorGatheringArea, out RegionState gathering))
                        clearance = gathering.MaxClearance;
                    int centerDepth = ResolveRegionDepth(
                        CutsceneStageRegion.InteriorGatheringArea,
                        clearance,
                        geometry.InteriorDepthDecimetres);
                    CutsceneInt3 center = Offset(
                        geometry.EntrancePosition,
                        geometry.Inward,
                        centerDepth,
                        geometry.Right,
                        0);
                    return CardinalToward(position, center, geometry.Inward, geometry.Right);

                case CutsceneStageFacingHint.SiteDefault:
                case CutsceneStageFacingHint.IntoSite:
                default:
                    return geometry.Inward;
            }
        }

        private static CutsceneInt3 CardinalToward(
            CutsceneInt3 from,
            CutsceneInt3 to,
            CutsceneInt3 fallback,
            CutsceneInt3 right)
        {
            int dx = to.X - from.X;
            int dz = to.Z - from.Z;
            if (dx == 0 && dz == 0) return fallback;

            int rightProjection = dx * right.X + dz * right.Z;
            int inwardProjection = dx * fallback.X + dz * fallback.Z;
            if (Math.Abs(rightProjection) > Math.Abs(inwardProjection))
                return rightProjection >= 0 ? right : Negate(right);
            return inwardProjection >= 0 ? fallback : Negate(fallback);
        }

        private static bool IsOccupiedPoint(
            CutsceneDefinition definition,
            CutsceneStagePointId point)
        {
            for (var i = 0; i < definition.Setup.Placements.Count; i++)
                if (definition.Setup.Placements[i].StagePoint.Equals(point))
                    return true;

            for (var i = 0; i < definition.Steps.Count; i++)
                if (StepOccupiesPoint(definition.Steps[i], point))
                    return true;

            return false;
        }

        private static bool StepOccupiesPoint(CutsceneStep step, CutsceneStagePointId point)
        {
            if (step.Type == CutsceneStepType.MoveActor && step.StagePoint.Equals(point))
                return true;
            if (step.Type != CutsceneStepType.Parallel) return false;

            for (var i = 0; i < step.Children.Count; i++)
                if (StepOccupiesPoint(step.Children[i], point))
                    return true;
            return false;
        }

        private static CutsceneInt3 Offset(
            CutsceneInt3 origin,
            CutsceneInt3 inward,
            int inwardDistance,
            CutsceneInt3 right,
            int lateralDistance) =>
            new CutsceneInt3(
                origin.X + inward.X * inwardDistance + right.X * lateralDistance,
                origin.Y,
                origin.Z + inward.Z * inwardDistance + right.Z * lateralDistance);

        private static CutsceneInt3 Negate(CutsceneInt3 value) =>
            new CutsceneInt3(-value.X, -value.Y, -value.Z);
    }

    public static class CutsceneStageRealizer
    {
        public static IReadOnlyList<CutsceneStageRealization> Realize(
            PlanningGraph graph,
            ICutsceneSiteGeometryProvider geometryProvider)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (geometryProvider == null) throw new ArgumentNullException(nameof(geometryProvider));

            var result = new List<CutsceneStageRealization>(graph.CutsceneStages.Count);
            for (var i = 0; i < graph.CutsceneStages.Count; i++)
            {
                CutsceneStagePlan plan = graph.CutsceneStages[i];
                if (!geometryProvider.TryResolve(plan.Site, out CutsceneSiteGeometry geometry))
                    throw new InvalidOperationException(
                        "No realized site geometry is available for cutscene '" + plan.Cutscene +
                        "' at site '" + plan.Site + "'.");

                result.Add(new CutsceneStageRealization(
                    plan.Cutscene,
                    plan.Site,
                    ProceduralCutsceneStageResolver.Resolve(plan, geometry)));
            }
            return result;
        }
    }
}
