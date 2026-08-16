using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure planning policy for the secondary defensive ring. The topology's DesiredTowerCount
    /// continues to describe the outer perimeter; a nested ward receives one smaller tower at
    /// each of its planned corners without changing that outer semantic contract.
    /// </summary>
    public static class CastleInnerWardTowerPlanner
    {
        private const uint RoofElementBase = 0x2A00u;

        public static CastleTowerPlacementSpec[] Create(int2[] innerWardVertices)
        {
            if (innerWardVertices == null || innerWardVertices.Length == 0)
                return Array.Empty<CastleTowerPlacementSpec>();

            uint ringSeed = RingIdentity(innerWardVertices);
            var towers = new CastleTowerPlacementSpec[innerWardVertices.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                uint variation = CastleSeedPartition.Derive(
                    ringSeed, CastleSeedDomain.Walls, RoofElementBase + (uint)i);
                towers[i] = new CastleTowerPlacementSpec
                {
                    Id = i,
                    Centre = innerWardVertices[i],
                    Role = CastleTowerPlacementRole.Corner,
                    HeightVariation = 0,
                    HasRoof = (variation & 1u) != 0u,
                };
            }
            return towers;
        }

        public static int Radius(in CastlePlan plan) =>
            math.max(18, plan.TowerRadius * 3 / 4);

        public static int Height(in CastlePlan plan) =>
            math.max(plan.WallHeight + 30, plan.TowerHeight * 4 / 5);

        /// <summary>
        /// Gives an identical planned defensive ring identical tower styling independent of when
        /// the immutable CastleSpatialPlan is copied/enriched. The ring geometry itself is already
        /// root-seed-derived, so Runtime never needs the castle seed to choose a roof.
        /// </summary>
        private static uint RingIdentity(int2[] vertices)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < vertices.Length; i++)
                {
                    hash = (hash ^ (uint)vertices[i].x) * 16777619u;
                    hash = (hash ^ (uint)vertices[i].y) * 16777619u;
                }
                return hash == 0u ? 0x6E624EB7u : hash;
            }
        }
    }
}
