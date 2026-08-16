using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleSpatialPlanIssue : byte
    {
        None,
        MissingOuterWard,
        InvalidTopology,
        DegeneratePerimeter,
        SelfIntersectingOuterWard,
        PerimeterOutsidePlateau,
        InvalidGateEdge,
        GateEdgeTooShort,
        GateDetachedFromPerimeter,
        InvalidGateNormal,
        PosternGateMismatch,
        InvalidPosternGateEdge,
        PosternGateEdgeTooShort,
        PosternGateDetachedFromPerimeter,
        InvalidPosternGateNormal,
        PosternGateConflictsWithPrimaryGate,
        TowerCountMismatch,
        TowerIdMismatch,
        DuplicateTower,
        MissingCornerTower,
        TowerOffPerimeter,
        WallTowerOnGateEdge,
        InnerWardMismatch,
        InvalidInnerTowerPlacement,
        SelfIntersectingInnerWard,
        InnerWardOutsideOuterWard,
        InnerGateMismatch,
        InvalidInnerGateEdge,
        InnerGateEdgeTooShort,
        InnerGateDetachedFromPerimeter,
        InvalidInnerGateNormal,
        InnerGateMisaligned,
        InvalidKeepResolution,
        KeepOutsideOuterWard,
        KeepOutsideInnerWard,
        CentralKeepPlacementMismatch,
        RearKeepPlacementMismatch,
        WallIntegratedKeepNotAgainstWard,
        InvalidAccessRoute,
        InvalidWellResolution,
        InvalidWellPlacement,
        InvalidCourtyardBuildingResolution,
        InvalidCourtyardBuildingPlacement,
        InvalidDungeonPlan,
        DungeonEntranceMismatch,
    }

    /// <summary>
    /// Pure structural validation for the spatial castle plan. This deliberately has no terrain,
    /// storage, material, or runtime dependencies so a plan can be rejected before voxel mutation.
    /// </summary>
    public static class CastleSpatialPlanValidator
    {
        public static bool TryValidate(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial,
            out CastleSpatialPlanIssue issue)
        {
            if (spatial == null || spatial.OuterWardVertices == null ||
                spatial.OuterWardVertices.Length < 4)
            {
                issue = CastleSpatialPlanIssue.MissingOuterWard;
                return false;
            }

            CastleTopologyPlan topology = spatial.Topology;
            if (!CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue _))
            {
                issue = CastleSpatialPlanIssue.InvalidTopology;
                return false;
            }

            int2[] outer = spatial.OuterWardVertices;
            long signedAreaTwice = 0;
            long plateauRadiusSquared =
                (long)dimensions.PlateauRadius * dimensions.PlateauRadius;

            for (int i = 0; i < outer.Length; i++)
            {
                int2 a = outer[i];
                int2 b = outer[(i + 1) % outer.Length];
                if (a.Equals(b))
                {
                    issue = CastleSpatialPlanIssue.DegeneratePerimeter;
                    return false;
                }

                long radiusSquared = (long)a.x * a.x + (long)a.y * a.y;
                if (radiusSquared > plateauRadiusSquared)
                {
                    issue = CastleSpatialPlanIssue.PerimeterOutsidePlateau;
                    return false;
                }

                signedAreaTwice += (long)a.x * b.y - (long)b.x * a.y;
            }

            if (signedAreaTwice == 0)
            {
                issue = CastleSpatialPlanIssue.DegeneratePerimeter;
                return false;
            }

            if (!CastlePolygonGeometry.IsSimplePolygon(outer))
            {
                issue = CastleSpatialPlanIssue.SelfIntersectingOuterWard;
                return false;
            }

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            if (!TryValidateGate(
                    outer,
                    in primaryGate,
                    CastleGatePlanningRules.PrimaryMinimumEdgeLength(in dimensions),
                    CastleSpatialPlanIssue.InvalidGateEdge,
                    CastleSpatialPlanIssue.GateEdgeTooShort,
                    CastleSpatialPlanIssue.GateDetachedFromPerimeter,
                    CastleSpatialPlanIssue.InvalidGateNormal,
                    out issue))
                return false;

            if (spatial.HasPosternGate != spatial.Topology.HasPosternGate)
            {
                issue = CastleSpatialPlanIssue.PosternGateMismatch;
                return false;
            }

            if (spatial.HasPosternGate)
            {
                CastleGatePlacementSpec posternGate = spatial.PosternGate;
                if (!TryValidateGate(
                        outer,
                        in posternGate,
                        CastleGatePlanningRules.PosternMinimumEdgeLength(in dimensions),
                        CastleSpatialPlanIssue.InvalidPosternGateEdge,
                        CastleSpatialPlanIssue.PosternGateEdgeTooShort,
                        CastleSpatialPlanIssue.PosternGateDetachedFromPerimeter,
                        CastleSpatialPlanIssue.InvalidPosternGateNormal,
                        out issue))
                    return false;

                if (posternGate.EdgeIndex == primaryGate.EdgeIndex ||
                    posternGate.Centre.Equals(primaryGate.Centre))
                {
                    issue = CastleSpatialPlanIssue.PosternGateConflictsWithPrimaryGate;
                    return false;
                }
            }

            CastleTowerPlacementSpec[] towers = spatial.Towers;
            if (towers == null || towers.Length != spatial.Topology.DesiredTowerCount)
            {
                issue = CastleSpatialPlanIssue.TowerCountMismatch;
                return false;
            }

            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i].Id != i)
                {
                    issue = CastleSpatialPlanIssue.TowerIdMismatch;
                    return false;
                }

                for (int other = 0; other < i; other++)
                {
                    if (!towers[i].Centre.Equals(towers[other].Centre)) continue;
                    issue = CastleSpatialPlanIssue.DuplicateTower;
                    return false;
                }

                if (!CastlePolygonGeometry.PointOnPerimeter(towers[i].Centre, outer))
                {
                    issue = CastleSpatialPlanIssue.TowerOffPerimeter;
                    return false;
                }

                if (towers[i].Role == CastleTowerPlacementRole.Wall &&
                    (PointOnEdge(towers[i].Centre, outer, primaryGate.EdgeIndex) ||
                     (spatial.HasPosternGate &&
                      PointOnEdge(towers[i].Centre, outer, spatial.PosternGate.EdgeIndex))))
                {
                    issue = CastleSpatialPlanIssue.WallTowerOnGateEdge;
                    return false;
                }
            }

            for (int vertex = 0; vertex < outer.Length; vertex++)
            {
                bool foundCorner = false;
                for (int tower = 0; tower < towers.Length; tower++)
                {
                    if (towers[tower].Role != CastleTowerPlacementRole.Corner ||
                        !towers[tower].Centre.Equals(outer[vertex])) continue;
                    foundCorner = true;
                    break;
                }

                if (!foundCorner)
                {
                    issue = CastleSpatialPlanIssue.MissingCornerTower;
                    return false;
                }
            }

            int2[] inner = spatial.InnerWardVertices;
            bool expectsInner = spatial.Topology.Wards == CastleWardPattern.InnerAndOuterWards;
            if (inner == null || (expectsInner && inner.Length != outer.Length) ||
                (!expectsInner && inner.Length != 0))
            {
                issue = CastleSpatialPlanIssue.InnerWardMismatch;
                return false;
            }

            CastleTowerPlacementSpec[] innerTowers = spatial.InnerTowers;
            if (innerTowers == null || innerTowers.Length != inner.Length)
            {
                issue = CastleSpatialPlanIssue.InvalidInnerTowerPlacement;
                return false;
            }

            for (int i = 0; i < innerTowers.Length; i++)
            {
                if (innerTowers[i].Id != i ||
                    innerTowers[i].Role != CastleTowerPlacementRole.Corner ||
                    !innerTowers[i].Centre.Equals(inner[i]))
                {
                    issue = CastleSpatialPlanIssue.InvalidInnerTowerPlacement;
                    return false;
                }
            }

            if (spatial.HasInnerGate != expectsInner)
            {
                issue = CastleSpatialPlanIssue.InnerGateMismatch;
                return false;
            }

            if (expectsInner)
            {
                if (!CastlePolygonGeometry.IsSimplePolygon(inner))
                {
                    issue = CastleSpatialPlanIssue.SelfIntersectingInnerWard;
                    return false;
                }

                for (int i = 0; i < inner.Length; i++)
                {
                    if (CastlePolygonGeometry.PointInOrOnPolygon(inner[i], outer)) continue;
                    issue = CastleSpatialPlanIssue.InnerWardOutsideOuterWard;
                    return false;
                }

                CastleGatePlacementSpec innerGate = spatial.InnerGate;
                if (!TryValidateGate(
                        inner,
                        in innerGate,
                        CastleGatePlanningRules.InnerMinimumEdgeLength(in dimensions),
                        CastleSpatialPlanIssue.InvalidInnerGateEdge,
                        CastleSpatialPlanIssue.InnerGateEdgeTooShort,
                        CastleSpatialPlanIssue.InnerGateDetachedFromPerimeter,
                        CastleSpatialPlanIssue.InvalidInnerGateNormal,
                        out issue))
                    return false;

                if (innerGate.EdgeIndex != primaryGate.EdgeIndex ||
                    math.dot(innerGate.Outward, primaryGate.Outward) <= 0.5f)
                {
                    issue = CastleSpatialPlanIssue.InnerGateMisaligned;
                    return false;
                }
            }

            bool highestGround =
                spatial.Topology.KeepPlacement == CastleKeepPlacement.HighestGround;
            if ((!highestGround && spatial.KeepRequiresTerrainResolution) ||
                (spatial.KeepRequiresTerrainResolution && !spatial.KeepCentre.Equals(int2.zero)))
            {
                issue = CastleSpatialPlanIssue.InvalidKeepResolution;
                return false;
            }

            if (!spatial.KeepRequiresTerrainResolution && !CastlePolygonGeometry.KeepFootprintFits(
                    in dimensions, spatial.KeepCentre, outer))
            {
                issue = CastleSpatialPlanIssue.KeepOutsideOuterWard;
                return false;
            }

            if (!spatial.KeepRequiresTerrainResolution && expectsInner &&
                !CastlePolygonGeometry.KeepFootprintFits(
                    in dimensions, spatial.KeepCentre, inner))
            {
                issue = CastleSpatialPlanIssue.KeepOutsideInnerWard;
                return false;
            }

            // Validate the semantic keep choice before courtyard dependencies such as the well.
            // This keeps diagnostics rooted in the earliest planning invariant that drifted.
            if (!spatial.KeepRequiresTerrainResolution)
            {
                int2[] keepWard = expectsInner ? inner : outer;
                if (spatial.Topology.KeepPlacement == CastleKeepPlacement.Central &&
                    !spatial.KeepCentre.Equals(int2.zero))
                {
                    issue = CastleSpatialPlanIssue.CentralKeepPlacementMismatch;
                    return false;
                }

                if (spatial.Topology.KeepPlacement == CastleKeepPlacement.Rear &&
                    !CastleKeepPlacementGeometry.IsRearKeepCentreAlong(
                        in dimensions, spatial.KeepCentre, -primaryGate.Outward, keepWard))
                {
                    issue = CastleSpatialPlanIssue.RearKeepPlacementMismatch;
                    return false;
                }

                if (spatial.Topology.KeepPlacement == CastleKeepPlacement.WallIntegrated &&
                    !CastleKeepPlacementGeometry.IsFarthestKeepCentreAlong(
                        in dimensions, spatial.KeepCentre, -primaryGate.Outward, keepWard))
                {
                    issue = CastleSpatialPlanIssue.WallIntegratedKeepNotAgainstWard;
                    return false;
                }

                CastleAccessRoute accessRoute = CastleAccessRoute.Create(in dimensions, spatial);
                if (!CastleAccessRouteValidator.TryValidate(
                        in accessRoute, outer, inner, out _))
                {
                    issue = CastleSpatialPlanIssue.InvalidAccessRoute;
                    return false;
                }
            }

            if (spatial.KeepRequiresTerrainResolution)
            {
                if (spatial.HasWell || !spatial.WellCentre.Equals(int2.zero))
                {
                    issue = CastleSpatialPlanIssue.InvalidWellResolution;
                    return false;
                }

                if (spatial.CourtyardBuildings == null || spatial.CourtyardBuildings.Length != 0)
                {
                    issue = CastleSpatialPlanIssue.InvalidCourtyardBuildingResolution;
                    return false;
                }
            }
            else
            {
                int2[] wellWard = expectsInner ? inner : outer;
                bool canPlaceWell = CastleCourtyardPlacementGeometry.TryChooseWell(
                    in dimensions,
                    wellWard,
                    in primaryGate,
                    spatial.KeepCentre,
                    out int2 expectedWell);
                if (!canPlaceWell || !spatial.HasWell || !spatial.WellCentre.Equals(expectedWell))
                {
                    issue = CastleSpatialPlanIssue.InvalidWellPlacement;
                    return false;
                }

                CastleGatePlacementSpec posternGate = spatial.PosternGate;
                CastleGatePlacementSpec innerGate = spatial.InnerGate;
                CastleCourtyardBuildingSpec[] expectedBuildings =
                    CastleCourtyardBuildingPlacementGeometry.Plan(
                        in dimensions,
                        outer,
                        inner,
                        in primaryGate,
                        spatial.HasPosternGate,
                        in posternGate,
                        spatial.HasInnerGate,
                        in innerGate,
                        spatial.KeepCentre,
                        spatial.HasWell,
                        spatial.WellCentre);
                if (!SameBuildings(spatial.CourtyardBuildings, expectedBuildings))
                {
                    issue = CastleSpatialPlanIssue.InvalidCourtyardBuildingPlacement;
                    return false;
                }
            }

            // Dungeon completion is optional at the general spatial-planning layer, but once a
            // dungeon is attached it is part of this planning snapshot: both its own graph and its
            // attachment point to the castle must agree with the supplied dimensions/spatial plan.
            if (spatial.Dungeon != null)
            {
                if (!DungeonPlanValidator.TryValidate(spatial.Dungeon, out _))
                {
                    issue = CastleSpatialPlanIssue.InvalidDungeonPlan;
                    return false;
                }

                if (!spatial.KeepRequiresTerrainResolution)
                {
                    CastleSpatialProjection projection = CastleSpatialProjection.Create(
                        in dimensions, spatial);
                    if (!spatial.Dungeon.Entrance.Equals(projection.TrapdoorCentre))
                    {
                        issue = CastleSpatialPlanIssue.DungeonEntranceMismatch;
                        return false;
                    }
                }
            }

            issue = CastleSpatialPlanIssue.None;
            return true;
        }

        private static bool SameBuildings(
            CastleCourtyardBuildingSpec[] actual,
            CastleCourtyardBuildingSpec[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
                return false;

            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i].Id != expected[i].Id ||
                    actual[i].Purpose != expected[i].Purpose ||
                    actual[i].WallEdgeIndex != expected[i].WallEdgeIndex ||
                    !actual[i].Centre.Equals(expected[i].Centre) ||
                    math.lengthsq(actual[i].Tangent - expected[i].Tangent) > 0.000001f ||
                    math.lengthsq(actual[i].Inward - expected[i].Inward) > 0.000001f ||
                    actual[i].Width != expected[i].Width ||
                    actual[i].Depth != expected[i].Depth ||
                    actual[i].Height != expected[i].Height)
                    return false;
            }

            return true;
        }

        private static bool PointOnEdge(int2 point, int2[] perimeter, int edgeIndex)
        {
            if (perimeter == null || edgeIndex < 0 || edgeIndex >= perimeter.Length)
                return false;
            return CastlePolygonGeometry.PointOnSegment(
                point,
                perimeter[edgeIndex],
                perimeter[(edgeIndex + 1) % perimeter.Length]);
        }

        private static bool TryValidateGate(
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int minimumEdgeLength,
            CastleSpatialPlanIssue invalidEdgeIssue,
            CastleSpatialPlanIssue edgeTooShortIssue,
            CastleSpatialPlanIssue detachedIssue,
            CastleSpatialPlanIssue invalidNormalIssue,
            out CastleSpatialPlanIssue issue)
        {
            if (gate.EdgeIndex < 0 || gate.EdgeIndex >= perimeter.Length)
            {
                issue = invalidEdgeIssue;
                return false;
            }

            if (!CastleGatePlanningRules.EdgeCanHostOpening(
                    perimeter, gate.EdgeIndex, minimumEdgeLength))
            {
                issue = edgeTooShortIssue;
                return false;
            }

            int2 gateStart = perimeter[gate.EdgeIndex];
            int2 gateEnd = perimeter[(gate.EdgeIndex + 1) % perimeter.Length];
            int2 expectedGateCentre = new int2(
                (gateStart.x + gateEnd.x) / 2,
                (gateStart.y + gateEnd.y) / 2);
            if (!gate.Centre.Equals(expectedGateCentre))
            {
                issue = detachedIssue;
                return false;
            }

            if (!math.all(math.isfinite(gate.Outward)) || math.lengthsq(gate.Outward) < 0.25f)
            {
                issue = invalidNormalIssue;
                return false;
            }

            float2 centroid = float2.zero;
            for (int i = 0; i < perimeter.Length; i++)
                centroid += new float2(perimeter[i].x, perimeter[i].y);
            centroid /= perimeter.Length;
            float2 toGate = new float2(gate.Centre.x, gate.Centre.y) - centroid;
            if (math.dot(toGate, gate.Outward) <= 0f)
            {
                issue = invalidNormalIssue;
                return false;
            }

            issue = CastleSpatialPlanIssue.None;
            return true;
        }
    }
}
