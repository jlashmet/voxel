using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleSpatialPlanIssue : byte
    {
        None,
        MissingOuterWard,
        DegeneratePerimeter,
        PerimeterOutsidePlateau,
        InvalidGateEdge,
        GateDetachedFromPerimeter,
        InvalidGateNormal,
        TowerCountMismatch,
        TowerIdMismatch,
        DuplicateTower,
        MissingCornerTower,
        TowerOffPerimeter,
        InnerWardMismatch,
        InnerWardOutsideOuterWard,
        InnerGateMismatch,
        InvalidInnerGateEdge,
        InnerGateDetachedFromPerimeter,
        InvalidInnerGateNormal,
        InnerGateMisaligned,
        InvalidKeepResolution,
        KeepOutsideOuterWard,
        KeepOutsideInnerWard,
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

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            if (!TryValidateGate(
                    outer,
                    in primaryGate,
                    CastleSpatialPlanIssue.InvalidGateEdge,
                    CastleSpatialPlanIssue.GateDetachedFromPerimeter,
                    CastleSpatialPlanIssue.InvalidGateNormal,
                    out issue))
                return false;

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

            if (spatial.HasInnerGate != expectsInner)
            {
                issue = CastleSpatialPlanIssue.InnerGateMismatch;
                return false;
            }

            if (expectsInner)
            {
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
                        CastleSpatialPlanIssue.InvalidInnerGateEdge,
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

            issue = CastleSpatialPlanIssue.None;
            return true;
        }

        private static bool TryValidateGate(
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            CastleSpatialPlanIssue invalidEdgeIssue,
            CastleSpatialPlanIssue detachedIssue,
            CastleSpatialPlanIssue invalidNormalIssue,
            out CastleSpatialPlanIssue issue)
        {
            if (gate.EdgeIndex < 0 || gate.EdgeIndex >= perimeter.Length)
            {
                issue = invalidEdgeIssue;
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
