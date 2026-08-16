using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Resolves semantic topology into local X/Z placement without touching terrain or voxel
    /// storage. Terrain-dependent choices remain explicitly unresolved for a later site-aware pass.
    /// </summary>
    public static class CastleSpatialPlanner
    {
        private const uint PrimaryGateSeedElement = 0x47415445u; // "GATE"

        public static CastleSpatialPlan Create(
            in CastlePlan dimensions,
            in CastleTopologyPlan topology)
        {
            int2[] outer = BuildOuterWard(in dimensions, in topology);
            CastleGatePlacementSpec gate = PlacePrimaryGate(in dimensions, outer);
            int2[] inner = topology.Wards == CastleWardPattern.InnerAndOuterWards
                ? BuildInnerWard(in dimensions, in topology, outer, in gate)
                : Array.Empty<int2>();
            CastleTowerPlacementSpec[] innerTowers = CastleInnerWardTowerPlanner.Create(inner);

            bool hasPosternGate = topology.HasPosternGate;
            CastleGatePlacementSpec posternGate = hasPosternGate
                ? PlacePosternGate(in dimensions, outer, gate.EdgeIndex, gate.Outward)
                : default;
            bool hasInnerGate = inner.Length != 0;
            CastleGatePlacementSpec innerGate = hasInnerGate
                ? PlaceGateOnEdge(inner, gate.EdgeIndex, gate.Outward)
                : default;
            CastleTowerPlacementSpec[] towers = PlaceTowers(
                dimensions.Seed,
                outer,
                gate.EdgeIndex,
                hasPosternGate ? posternGate.EdgeIndex : -1,
                topology.DesiredTowerCount);
            int2[] keepWard = inner.Length != 0 ? inner : outer;
            int2 keepCentre = PlaceKeep(
                in dimensions, topology.KeepPlacement, in gate, keepWard,
                out bool requiresTerrainResolution);
            int2 wellCentre = default;
            bool hasWell = !requiresTerrainResolution &&
                CastleCourtyardPlacementGeometry.TryChooseWell(
                    in dimensions, keepWard, in gate, keepCentre, out wellCentre);
            CastleCourtyardBuildingSpec[] courtyardBuildings = requiresTerrainResolution
                ? Array.Empty<CastleCourtyardBuildingSpec>()
                : CastleCourtyardBuildingPlacementGeometry.Plan(
                    in dimensions,
                    outer,
                    inner,
                    in gate,
                    hasPosternGate,
                    in posternGate,
                    hasInnerGate,
                    in innerGate,
                    keepCentre,
                    hasWell,
                    wellCentre);

            return new CastleSpatialPlan(
                in topology,
                outer,
                inner,
                towers,
                in gate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                keepCentre,
                requiresTerrainResolution,
                innerTowers);
        }

        /// <summary>
        /// Returns true when a terrain-selected HighestGround keep centre supports the complete
        /// dependent courtyard programme, not merely the keep footprint itself.
        /// </summary>
        public static bool CanResolveHighestGroundKeep(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial,
            int2 localKeepCentre)
        {
            if (spatial == null ||
                spatial.Topology.KeepPlacement != CastleKeepPlacement.HighestGround ||
                !spatial.KeepRequiresTerrainResolution)
                return false;

            int2[] keepWard = spatial.InnerWardVertices != null && spatial.InnerWardVertices.Length != 0
                ? spatial.InnerWardVertices
                : spatial.OuterWardVertices;
            if (!CastlePolygonGeometry.ContainsKeepFootprint(
                    in dimensions, localKeepCentre, keepWard))
                return false;

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            return CastleCourtyardPlacementGeometry.TryChooseWell(
                in dimensions,
                keepWard,
                in primaryGate,
                localKeepCentre,
                out _);
        }

        /// <summary>
        /// Finishes an unresolved HighestGround placement after a site-aware caller has selected a
        /// concrete local X/Z centre. The resolver does not query terrain itself; it only validates
        /// that the supplied centre fits the ward and returns a new immutable planning result.
        /// </summary>
        public static CastleSpatialPlan ResolveHighestGroundKeep(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial,
            int2 localKeepCentre)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.Topology.KeepPlacement != CastleKeepPlacement.HighestGround)
            {
                throw new InvalidOperationException(
                    "Only HighestGround castle plans require terrain keep resolution.");
            }

            if (!spatial.KeepRequiresTerrainResolution)
                return spatial;

            if (!CanResolveHighestGroundKeep(in dimensions, spatial, localKeepCentre))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localKeepCentre),
                    "Resolved keep centre must fit its assigned ward and preserve a valid courtyard well/access route.");
            }

            int2[] keepWard = spatial.InnerWardVertices != null && spatial.InnerWardVertices.Length != 0
                ? spatial.InnerWardVertices
                : spatial.OuterWardVertices;
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;
            bool hasWell = CastleCourtyardPlacementGeometry.TryChooseWell(
                in dimensions,
                keepWard,
                in primaryGate,
                localKeepCentre,
                out int2 wellCentre);
            CastleCourtyardBuildingSpec[] courtyardBuildings =
                CastleCourtyardBuildingPlacementGeometry.Plan(
                    in dimensions,
                    spatial.OuterWardVertices,
                    spatial.InnerWardVertices,
                    in primaryGate,
                    spatial.HasPosternGate,
                    in posternGate,
                    spatial.HasInnerGate,
                    in innerGate,
                    localKeepCentre,
                    hasWell,
                    wellCentre);
            var resolved = new CastleSpatialPlan(
                in topology,
                (int2[])spatial.OuterWardVertices.Clone(),
                (int2[])spatial.InnerWardVertices.Clone(),
                (CastleTowerPlacementSpec[])spatial.Towers.Clone(),
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                localKeepCentre,
                false,
                spatial.InnerTowers != null
                    ? (CastleTowerPlacementSpec[])spatial.InnerTowers.Clone()
                    : Array.Empty<CastleTowerPlacementSpec>());

            if (!CastleSpatialPlanValidator.TryValidate(
                    in dimensions, resolved, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Resolved highest-ground keep produced an invalid spatial plan: {issue}.");
            }

            return resolved;
        }

        private static int2[] BuildOuterWard(
            in CastlePlan dimensions,
            in CastleTopologyPlan topology)
        {
            switch (topology.Perimeter)
            {
                case CastlePerimeterKind.Rectangular:
                    return Rectangle(dimensions.BaileyHalfX, dimensions.BaileyHalfZ);

                case CastlePerimeterKind.IrregularQuadrilateral:
                    return IrregularQuadrilateral(in dimensions);

                case CastlePerimeterKind.IrregularPolygon:
                    return RadialPolygon(
                        in dimensions,
                        math.clamp(topology.DesiredTowerCount, 5, 8),
                        0.84f,
                        1f);

                case CastlePerimeterKind.Concentric:
                    return RadialPolygon(
                        in dimensions,
                        math.clamp(topology.DesiredTowerCount, 6, 8),
                        0.92f,
                        1f);

                default:
                    return Rectangle(dimensions.BaileyHalfX, dimensions.BaileyHalfZ);
            }
        }

        private static int2[] Rectangle(int halfX, int halfZ) =>
            new[]
            {
                new int2(-halfX, -halfZ),
                new int2( halfX, -halfZ),
                new int2( halfX,  halfZ),
                new int2(-halfX,  halfZ),
            };

        private static int2[] IrregularQuadrilateral(in CastlePlan dimensions)
        {
            int hx = dimensions.BaileyHalfX;
            int hz = dimensions.BaileyHalfZ;
            var result = new int2[4];

            for (int i = 0; i < result.Length; i++)
            {
                var rng = new Random(CastleSeedPartition.Derive(
                    dimensions.Seed, CastleSeedDomain.Layout, (uint)i));
                int insetX = rng.NextInt(6, 31);
                int insetZ = rng.NextInt(6, 31);

                switch (i)
                {
                    case 0: result[i] = new int2(-hx + insetX, -hz + insetZ); break;
                    case 1: result[i] = new int2( hx - insetX, -hz + insetZ); break;
                    case 2: result[i] = new int2( hx - insetX,  hz - insetZ); break;
                    default: result[i] = new int2(-hx + insetX,  hz - insetZ); break;
                }
            }

            return result;
        }

        private static int2[] RadialPolygon(
            in CastlePlan dimensions,
            int count,
            float minimumScale,
            float maximumScale)
        {
            var vertices = new int2[count];
            const float twoPi = math.PI * 2f;

            for (int i = 0; i < count; i++)
            {
                var rng = new Random(CastleSeedPartition.Derive(
                    dimensions.Seed, CastleSeedDomain.Layout, (uint)(100 + i)));
                float scale = rng.NextFloat(minimumScale, maximumScale);
                float angle = -math.PI * 0.5f + twoPi * i / count;
                vertices[i] = new int2(
                    (int)math.round(math.cos(angle) * dimensions.BaileyHalfX * scale),
                    (int)math.round(math.sin(angle) * dimensions.BaileyHalfZ * scale));
            }

            return vertices;
        }

        private static int2[] BuildInnerWard(
            in CastlePlan dimensions,
            in CastleTopologyPlan topology,
            int2[] outer,
            in CastleGatePlacementSpec primaryGate)
        {
            const float minimumScale = 0.64f;
            const float maximumScale = 0.84f;
            const int scaleSteps = 10;

            int2[] candidate = Array.Empty<int2>();
            for (int step = 0; step <= scaleSteps; step++)
            {
                float t = step / (float)scaleSteps;
                float scale = math.lerp(minimumScale, maximumScale, t);
                candidate = ScaleRing(outer, scale);

                int2 sizingKeep = PlaceKeep(
                    in dimensions,
                    topology.KeepPlacement,
                    in primaryGate,
                    candidate,
                    out bool requiresTerrainResolution);
                if (!CastlePolygonGeometry.KeepFootprintFits(
                        in dimensions, sizingKeep, candidate))
                    continue;

                if (requiresTerrainResolution)
                    sizingKeep = int2.zero;

                if (CastleCourtyardPlacementGeometry.TryChooseWell(
                        in dimensions,
                        candidate,
                        in primaryGate,
                        sizingKeep,
                        out _))
                    return candidate;
            }

            // Validation will reject a castle whose dependent inner-courtyard programme cannot fit
            // even at the maximum nested-ward scale. Keeping the cap below 1 preserves a meaningful
            // defensive gap to the outer ring and its towers.
            return candidate;
        }

        private static int2[] ScaleRing(int2[] outer, float scale)
        {
            var inner = new int2[outer.Length];
            for (int i = 0; i < outer.Length; i++)
                inner[i] = new int2(
                    (int)math.round(outer[i].x * scale),
                    (int)math.round(outer[i].y * scale));
            return inner;
        }

        private static CastleGatePlacementSpec PlacePrimaryGate(
            in CastlePlan dimensions,
            int2[] perimeter)
        {
            int minimumLength = CastleGatePlanningRules.PrimaryMinimumEdgeLength(in dimensions);
            int bestEdge = -1;
            uint bestScore = 0u;

            for (int edge = 0; edge < perimeter.Length; edge++)
            {
                if (!CastleGatePlanningRules.EdgeCanHostOpening(
                        perimeter, edge, minimumLength))
                    continue;

                uint score = CastleSeedPartition.Derive(
                    dimensions.Seed,
                    CastleSeedDomain.Layout,
                    PrimaryGateSeedElement + (uint)edge);
                if (bestEdge >= 0 && score <= bestScore) continue;
                bestEdge = edge;
                bestScore = score;
            }

            if (bestEdge < 0)
                bestEdge = LongestEdge(perimeter, -1);

            return PlaceGateOnEdge(
                perimeter,
                bestEdge,
                EdgeOutwardPreference(perimeter, bestEdge));
        }

        private static CastleGatePlacementSpec PlacePosternGate(
            in CastlePlan dimensions,
            int2[] perimeter,
            int primaryEdge,
            float2 primaryOutward)
        {
            int minimumLength = CastleGatePlanningRules.PosternMinimumEdgeLength(in dimensions);
            int bestEdge = -1;
            float bestScore = float.MinValue;
            float2 inward = -primaryOutward;
            float2 centroid = VertexCentroid(perimeter);

            for (int edge = 0; edge < perimeter.Length; edge++)
            {
                if (edge == primaryEdge || !CastleGatePlanningRules.EdgeCanHostOpening(
                        perimeter, edge, minimumLength))
                    continue;

                int2 a = perimeter[edge];
                int2 b = perimeter[(edge + 1) % perimeter.Length];
                float2 midpoint = new float2(
                    (a.x + b.x) * 0.5f,
                    (a.y + b.y) * 0.5f);
                float score = math.dot(midpoint - centroid, inward);
                if (score <= bestScore) continue;
                bestScore = score;
                bestEdge = edge;
            }

            if (bestEdge < 0)
                bestEdge = LongestEdge(perimeter, primaryEdge);

            return PlaceGateOnEdge(
                perimeter,
                bestEdge,
                EdgeOutwardPreference(perimeter, bestEdge));
        }

        private static CastleGatePlacementSpec PlaceGateOnEdge(
            int2[] perimeter,
            int edgeIndex,
            float2 preferredOutward)
        {
            int2 start = perimeter[edgeIndex];
            int2 end = perimeter[(edgeIndex + 1) % perimeter.Length];
            int2 centre = new int2((start.x + end.x) / 2, (start.y + end.y) / 2);
            int2 edge = end - start;
            float2 outward = new float2(edge.y, -edge.x);
            float length = math.length(outward);
            outward = length > 0.001f ? outward / length : preferredOutward;
            if (math.dot(outward, preferredOutward) < 0f)
                outward = -outward;

            return new CastleGatePlacementSpec
            {
                EdgeIndex = edgeIndex,
                Centre = centre,
                Outward = outward,
            };
        }

        private static int LongestEdge(int2[] perimeter, int excludedEdge)
        {
            int bestEdge = -1;
            long bestLengthSquared = -1;
            for (int edge = 0; edge < perimeter.Length; edge++)
            {
                if (edge == excludedEdge) continue;
                int2 a = perimeter[edge];
                int2 b = perimeter[(edge + 1) % perimeter.Length];
                long dx = (long)b.x - a.x;
                long dz = (long)b.y - a.y;
                long lengthSquared = dx * dx + dz * dz;
                if (lengthSquared <= bestLengthSquared) continue;
                bestLengthSquared = lengthSquared;
                bestEdge = edge;
            }
            return bestEdge >= 0 ? bestEdge : 0;
        }

        private static float2 EdgeOutwardPreference(int2[] perimeter, int edgeIndex)
        {
            int2 a = perimeter[edgeIndex];
            int2 b = perimeter[(edgeIndex + 1) % perimeter.Length];
            float2 midpoint = new float2(
                (a.x + b.x) * 0.5f,
                (a.y + b.y) * 0.5f);
            float2 outward = midpoint - VertexCentroid(perimeter);
            float length = math.length(outward);
            return length > 0.001f ? outward / length : new float2(0f, -1f);
        }

        private static float2 VertexCentroid(int2[] perimeter)
        {
            float2 centroid = float2.zero;
            for (int i = 0; i < perimeter.Length; i++)
                centroid += new float2(perimeter[i].x, perimeter[i].y);
            return centroid / perimeter.Length;
        }

        private static CastleTowerPlacementSpec[] PlaceTowers(
            uint seed,
            int2[] perimeter,
            int primaryGateEdge,
            int posternGateEdge,
            int desiredCount)
        {
            int target = math.max(perimeter.Length, desiredCount);
            var towers = new List<CastleTowerPlacementSpec>(target);

            for (int i = 0; i < perimeter.Length; i++)
            {
                int towerId = towers.Count;
                uint variationSeed = CastleSeedPartition.Derive(
                    seed, CastleSeedDomain.Walls, (uint)(0x2000 + towerId));
                towers.Add(new CastleTowerPlacementSpec
                {
                    Id = towerId,
                    Centre = perimeter[i],
                    Role = CastleTowerPlacementRole.Corner,
                    HeightVariation = 8 + (int)(variationSeed % 51u),
                    HasRoof = ((variationSeed >> 8) & 1u) != 0u,
                });
            }

            bool[] usedEdges = new bool[perimeter.Length];
            usedEdges[primaryGateEdge] = true;
            if (posternGateEdge >= 0 && posternGateEdge < usedEdges.Length)
                usedEdges[posternGateEdge] = true;

            while (towers.Count < target)
            {
                int bestEdge = -1;
                uint bestScore = 0u;

                for (int edge = 0; edge < perimeter.Length; edge++)
                {
                    if (usedEdges[edge]) continue;
                    uint score = CastleSeedPartition.Derive(
                        seed, CastleSeedDomain.Walls, (uint)(1000 + edge));
                    if (bestEdge >= 0 && score <= bestScore) continue;
                    bestEdge = edge;
                    bestScore = score;
                }

                if (bestEdge < 0) break;
                usedEdges[bestEdge] = true;
                int2 a = perimeter[bestEdge];
                int2 b = perimeter[(bestEdge + 1) % perimeter.Length];
                int towerId = towers.Count;
                uint variationSeed = CastleSeedPartition.Derive(
                    seed, CastleSeedDomain.Walls, (uint)(0x2000 + towerId));
                towers.Add(new CastleTowerPlacementSpec
                {
                    Id = towerId,
                    Centre = new int2((a.x + b.x) / 2, (a.y + b.y) / 2),
                    Role = CastleTowerPlacementRole.Wall,
                    HeightVariation = 8 + (int)(variationSeed % 51u),
                    HasRoof = false,
                });
            }

            return towers.ToArray();
        }

        private static int2 PlaceKeep(
            in CastlePlan dimensions,
            CastleKeepPlacement placement,
            in CastleGatePlacementSpec gate,
            int2[] keepWard,
            out bool requiresTerrainResolution)
        {
            requiresTerrainResolution = false;
            if (placement == CastleKeepPlacement.HighestGround)
            {
                requiresTerrainResolution = true;
                return int2.zero;
            }

            if (placement == CastleKeepPlacement.Central)
                return RetractKeepToWard(int2.zero, in dimensions, keepWard);

            float2 inward = -gate.Outward;
            int2 integrated = CastleKeepPlacementGeometry.FarthestKeepCentreAlong(
                in dimensions, inward, keepWard);
            if (placement == CastleKeepPlacement.WallIntegrated)
                return integrated;

            int2 desiredRear = new int2(
                (int)math.round(integrated.x * 0.78f),
                (int)math.round(integrated.y * 0.78f));
            return RetractKeepToWard(desiredRear, in dimensions, keepWard);
        }

        private static int2 RetractKeepToWard(
            int2 desired,
            in CastlePlan dimensions,
            int2[] keepWard)
        {
            if (CastlePolygonGeometry.KeepFootprintFits(in dimensions, desired, keepWard))
                return desired;

            // Preserve the selected placement direction while moving only as far toward the ward
            // centre as necessary to fit the complete keep footprint. Fixed samples keep retries
            // deterministic and avoid floating search tolerances becoming part of the seed contract.
            for (int step = 127; step >= 0; step--)
            {
                float t = step / 128f;
                int2 candidate = new int2(
                    (int)math.round(desired.x * t),
                    (int)math.round(desired.y * t));
                if (CastlePolygonGeometry.KeepFootprintFits(
                        in dimensions, candidate, keepWard))
                    return candidate;
            }

            // Validation will reject this if even the ward centre cannot contain the keep.
            return int2.zero;
        }
    }
}
