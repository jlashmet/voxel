using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition-owned site-aware completion for castle plans that deliberately leave terrain
    /// choices unresolved in Structures.Api. Runtime receives only a finished detached spatial plan.
    /// </summary>
    public static class CastleTerrainPlanning
    {
        public static CastleSpatialPlan Resolve(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            uint terrainSeed)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            CastleSpatialPlan resolved = spatial;
            if (spatial.KeepRequiresTerrainResolution)
            {
                if (spatial.Topology.KeepPlacement != CastleKeepPlacement.HighestGround)
                {
                    throw new InvalidOperationException(
                        "Unexpected terrain dependency: only HighestGround keep placement is supported.");
                }

                int2 chosen = FindHighestGroundKeep(in plan, spatial, terrainSeed);
                resolved = CastleSpatialPlanner.ResolveHighestGroundKeep(
                    in plan, spatial, chosen);
            }

            resolved = CastleGatehousePlanCompletion.Attach(in plan, resolved);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, resolved);
            completed = CastleKeepTurretPlanCompletion.Attach(in plan, completed);
            completed = CastleTowerSlitPlanCompletion.Attach(in plan, completed);

            // Detach every mutable planning array before the object crosses into Runtime. This
            // keeps production builds isolated even while planning/test APIs intentionally expose
            // lightweight mutable arrays for corruption tests and incremental plan enrichment.
            return CastleSpatialPlanSnapshot.CloneRuntimeReady(in plan, completed);
        }

        private static int2 FindHighestGroundKeep(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            uint terrainSeed)
        {
            int2[] ward = spatial.InnerWardVertices != null && spatial.InnerWardVertices.Length != 0
                ? spatial.InnerWardVertices
                : spatial.OuterWardVertices;
            if (ward == null || ward.Length < 3)
                throw new InvalidOperationException("Castle keep has no valid ward to search.");

            int minX = ward[0].x;
            int maxX = minX;
            int minZ = ward[0].y;
            int maxZ = minZ;
            for (int i = 1; i < ward.Length; i++)
            {
                minX = math.min(minX, ward[i].x);
                maxX = math.max(maxX, ward[i].x);
                minZ = math.min(minZ, ward[i].y);
                maxZ = math.max(maxZ, ward[i].y);
            }

            bool found = false;
            int2 best = default;
            int bestHeight = int.MinValue;
            int bestSlope = int.MaxValue;
            uint bestTieBreak = 0u;

            Consider(
                int2.zero,
                in plan,
                spatial,
                terrainSeed,
                ref found,
                ref best,
                ref bestHeight,
                ref bestSlope,
                ref bestTieBreak);

            const int stride = 12;
            for (int z = minZ; z <= maxZ; z += stride)
            for (int x = minX; x <= maxX; x += stride)
            {
                Consider(
                    new int2(x, z),
                    in plan,
                    spatial,
                    terrainSeed,
                    ref found,
                    ref best,
                    ref bestHeight,
                    ref bestSlope,
                    ref bestTieBreak);
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    "No terrain-resolved keep site can satisfy the complete castle spatial plan.");
            }

            return best;
        }

        private static void Consider(
            int2 candidate,
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            uint terrainSeed,
            ref bool found,
            ref int2 best,
            ref int bestHeight,
            ref int bestSlope,
            ref uint bestTieBreak)
        {
            if (!CastleSpatialPlanner.CanResolveHighestGroundKeep(
                    in plan, spatial, candidate))
                return;

            int worldX = plan.Centre.x + candidate.x;
            int worldZ = plan.Centre.z + candidate.y;
            int height = TerrainQuery.HeightAt(worldX, worldZ, terrainSeed);
            int slope = TerrainQuery.SlopeAt(worldX, worldZ, terrainSeed);
            uint elementId = unchecked(
                (uint)candidate.x * 73856093u ^ (uint)candidate.y * 19349663u);
            uint tieBreak = CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Layout, elementId);

            bool better = !found
                       || height > bestHeight
                       || (height == bestHeight && slope < bestSlope)
                       || (height == bestHeight && slope == bestSlope && tieBreak > bestTieBreak);
            if (!better) return;

            found = true;
            best = candidate;
            bestHeight = height;
            bestSlope = slope;
            bestTieBreak = tieBreak;
        }
    }
}
