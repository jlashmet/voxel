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
        public static CastleSpatialPlan Create(
            in CastlePlan dimensions,
            in CastleTopologyPlan topology)
        {
            int2[] outer = BuildOuterWard(in dimensions, in topology);
            int2[] inner = topology.Wards == CastleWardPattern.InnerAndOuterWards
                ? ScaleRing(outer, 0.64f)
                : Array.Empty<int2>();

            CastleGatePlacementSpec gate = PlacePrimaryGate(outer);
            bool hasInnerGate = inner.Length != 0;
            CastleGatePlacementSpec innerGate = hasInnerGate
                ? PlaceGateOnEdge(inner, gate.EdgeIndex, gate.Outward)
                : default;
            CastleTowerPlacementSpec[] towers = PlaceTowers(
                dimensions.Seed, outer, gate.EdgeIndex, topology.DesiredTowerCount);
            int2 keepCentre = PlaceKeep(
                in dimensions, topology.KeepPlacement, in gate,
                out bool requiresTerrainResolution);

            return new CastleSpatialPlan(
                in topology,
                outer,
                inner,
                towers,
                in gate,
                hasInnerGate,
                in innerGate,
                keepCentre,
                requiresTerrainResolution);
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

        private static int2[] ScaleRing(int2[] outer, float scale)
        {
            var inner = new int2[outer.Length];
            for (int i = 0; i < outer.Length; i++)
                inner[i] = new int2(
                    (int)math.round(outer[i].x * scale),
                    (int)math.round(outer[i].y * scale));
            return inner;
        }

        private static CastleGatePlacementSpec PlacePrimaryGate(int2[] perimeter)
        {
            int bestEdge = 0;
            int bestMidZ = int.MaxValue;

            for (int i = 0; i < perimeter.Length; i++)
            {
                int2 a = perimeter[i];
                int2 b = perimeter[(i + 1) % perimeter.Length];
                int midZ = a.y + b.y;
                if (midZ >= bestMidZ) continue;
                bestMidZ = midZ;
                bestEdge = i;
            }

            return PlaceGateOnEdge(perimeter, bestEdge, new float2(0f, -1f));
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

        private static CastleTowerPlacementSpec[] PlaceTowers(
            uint seed,
            int2[] perimeter,
            int gateEdge,
            int desiredCount)
        {
            int target = math.max(perimeter.Length, desiredCount);
            var towers = new List<CastleTowerPlacementSpec>(target);

            for (int i = 0; i < perimeter.Length; i++)
            {
                towers.Add(new CastleTowerPlacementSpec
                {
                    Id = towers.Count,
                    Centre = perimeter[i],
                    Role = CastleTowerPlacementRole.Corner,
                });
            }

            bool[] usedEdges = new bool[perimeter.Length];
            usedEdges[gateEdge] = true;
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
                towers.Add(new CastleTowerPlacementSpec
                {
                    Id = towers.Count,
                    Centre = new int2((a.x + b.x) / 2, (a.y + b.y) / 2),
                    Role = CastleTowerPlacementRole.Wall,
                });
            }

            return towers.ToArray();
        }

        private static int2 PlaceKeep(
            in CastlePlan dimensions,
            CastleKeepPlacement placement,
            in CastleGatePlacementSpec gate,
            out bool requiresTerrainResolution)
        {
            requiresTerrainResolution = false;
            if (placement == CastleKeepPlacement.Central)
                return int2.zero;

            if (placement == CastleKeepPlacement.HighestGround)
            {
                requiresTerrainResolution = true;
                return int2.zero;
            }

            float2 inward = -gate.Outward;
            int insetX = placement == CastleKeepPlacement.WallIntegrated
                ? math.max(0, dimensions.BaileyHalfX - dimensions.KeepHalfX)
                : math.max(0, dimensions.BaileyHalfX - dimensions.KeepHalfX
                              - dimensions.WallThickness - 24);
            int insetZ = placement == CastleKeepPlacement.WallIntegrated
                ? math.max(0, dimensions.BaileyHalfZ - dimensions.KeepHalfZ)
                : math.max(0, dimensions.BaileyHalfZ - dimensions.KeepHalfZ
                              - dimensions.WallThickness - 24);

            float distance = float.MaxValue;
            if (math.abs(inward.x) > 0.001f)
                distance = math.min(distance, insetX / math.abs(inward.x));
            if (math.abs(inward.y) > 0.001f)
                distance = math.min(distance, insetZ / math.abs(inward.y));
            if (distance == float.MaxValue)
                distance = 0f;

            if (placement == CastleKeepPlacement.Rear)
                distance *= 0.78f;

            return new int2(
                (int)math.round(inward.x * distance),
                (int)math.round(inward.y * distance));
        }
    }
}
