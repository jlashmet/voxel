using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative world-voxel envelope for one fully resolved spatial castle build.
    /// Min is inclusive and MaxExclusive is exclusive. Planned decoration contributes its actual
    /// semantic footprint instead of inflating unrelated cave space with a fixed safety halo.
    /// </summary>
    public readonly struct CastleBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal CastleBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Pure dependency-bounds resolver. This is intentionally independent of storage/runtime;
    /// Composition can queue every intersected region before any castle voxel mutation begins.
    /// </summary>
    public static class CastleBuildBoundsResolver
    {
        public static CastleBuildBounds Resolve(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            int baseY = plan.Centre.y + plan.PlateauHeight;

            // Site sculpt: CastleSiteRealizer iterates the full plateau + cliff skirt. Keep a small
            // extra horizontal margin for tower faces and later wall-foot dressing.
            int siteReach = plan.PlateauRadius + plan.CliffDrop + plan.TowerRadius + 24;
            int minX = plan.Centre.x - siteReach;
            int maxX = plan.Centre.x + siteReach;
            int minZ = plan.Centre.z - siteReach;
            int maxZ = plan.Centre.z + siteReach;

            // The site mutates down through the authored river/cliff band. Do not clamp to world
            // Y=0: voxel coordinates are signed and dependency bounds must remain conservative for
            // castles sited in low or negative-Y worlds. Upper headroom covers the keep roofline
            // and four-storey chapel bell tower.
            int minY = baseY - 256;
            int authoredHeight = math.max(
                plan.KeepHeight + 128,
                math.max(plan.TowerHeight, plan.GateTowerHeight) + 128);
            int maxY = baseY + authoredHeight;

            // Gate-oriented site/approach work. Current terrain carving runs approximately one
            // plateau+cliff span along the wall and ~220 voxels outside it. These margins are
            // intentionally larger so bridge rails, rubble, and modest recipe growth stay inside.
            int tangentReach = plan.PlateauRadius + plan.CliffDrop + 64;
            int outwardReach = plan.WallThickness + 256;
            IncludeApproachCorner(
                in plan, in projection.Approach, -tangentReach, -64,
                ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeApproachCorner(
                in plan, in projection.Approach, tangentReach, -64,
                ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeApproachCorner(
                in plan, in projection.Approach, -tangentReach, outwardReach,
                ref minX, ref maxX, ref minZ, ref maxZ);
            IncludeApproachCorner(
                in plan, in projection.Approach, tangentReach, outwardReach,
                ref minX, ref maxX, ref minZ, ref maxZ);

            // Keep-local authored annexes still use the compatibility keep frame. Keep this broad
            // envelope for those details, but do not use it as the dungeon contract: DungeonPlan
            // may place its cave threshold on either side of the keep.
            int2 keep = projection.KeepCentreWorld;
            const int keepSideReach = 384;
            const int keepRearReach = 640;
            const int keepForwardReach = 256;
            minX = math.min(minX, keep.x - keepSideReach);
            maxX = math.max(maxX, keep.x + keepSideReach);
            minZ = math.min(minZ, keep.y - keepRearReach);
            maxZ = math.max(maxZ, keep.y + keepForwardReach);

            IncludePlannedUnderground(
                spatial.Dungeon,
                spatial.Cave,
                spatial.CaveDecoration,
                ref minX,
                ref maxX,
                ref minY,
                ref maxY,
                ref minZ,
                ref maxZ);

            // Planned vertices/towers should normally be inside the site envelope, but include the
            // actual topology explicitly so a future planner can use more of the legal plateau
            // without silently invalidating streaming dependencies.
            int perimeterPadding = math.max(plan.TowerRadius, plan.GateTowerRadius) + 24;
            int2[] outer = spatial.OuterWardVertices;
            for (int i = 0; i < outer.Length; i++)
            {
                int x = plan.Centre.x + outer[i].x;
                int z = plan.Centre.z + outer[i].y;
                minX = math.min(minX, x - perimeterPadding);
                maxX = math.max(maxX, x + perimeterPadding);
                minZ = math.min(minZ, z - perimeterPadding);
                maxZ = math.max(maxZ, z + perimeterPadding);
            }

            return new CastleBuildBounds(
                new int3(minX, minY, minZ),
                new int3(maxX + 1, maxY + 1, maxZ + 1));
        }

        private static void IncludePlannedUnderground(
            DungeonPlan dungeon,
            CavePlan cave,
            CastleCaveDecorationPlan caveDecoration,
            ref int minX,
            ref int maxX,
            ref int minY,
            ref int maxY,
            ref int minZ,
            ref int maxZ)
        {
            if (dungeon == null)
                return;

            DungeonBuildBounds dungeonBounds = DungeonBuildBoundsResolver.Resolve(dungeon);
            const int designedPadding = 16;
            minX = math.min(minX, dungeonBounds.Min.x - designedPadding);
            maxX = math.max(maxX, dungeonBounds.MaxExclusive.x - 1 + designedPadding);
            minY = math.min(minY, dungeonBounds.Min.y - designedPadding);
            maxY = math.max(maxY, dungeonBounds.MaxExclusive.y - 1 + designedPadding);
            minZ = math.min(minZ, dungeonBounds.Min.z - designedPadding);
            maxZ = math.max(maxZ, dungeonBounds.MaxExclusive.z - 1 + designedPadding);

            if (!dungeon.HasCaveExit)
                return;
            if (cave == null)
                throw new InvalidOperationException(
                    "Castle dungeon has a cave exit but the spatial plan has no attached cave plan.");

            // CaveBuildBoundsResolver mirrors the generic CaveRealizer. Retain a small generic
            // safety margin for rasterization/recipe evolution, but do not use that margin to stand
            // in for castle-specific decorations now that their placements are planned explicitly.
            CaveBuildBounds caveBounds = CaveBuildBoundsResolver.Resolve(cave);
            const int cavePadding = 16;
            minX = math.min(minX, caveBounds.Min.x - cavePadding);
            maxX = math.max(maxX, caveBounds.MaxExclusive.x - 1 + cavePadding);
            minY = math.min(minY, caveBounds.Min.y - cavePadding);
            maxY = math.max(maxY, caveBounds.MaxExclusive.y - 1 + cavePadding);
            minZ = math.min(minZ, caveBounds.Min.z - cavePadding);
            maxZ = math.max(maxZ, caveBounds.MaxExclusive.z - 1 + cavePadding);

            if (caveDecoration == null)
                throw new InvalidOperationException(
                    "Castle cave has no attached decoration plan for dependency sizing.");

            CastleCaveDecorationBuildBounds decorationBounds =
                CastleCaveDecorationBuildBoundsResolver.Resolve(cave, caveDecoration);
            minX = math.min(minX, decorationBounds.Min.x);
            maxX = math.max(maxX, decorationBounds.MaxExclusive.x - 1);
            minY = math.min(minY, decorationBounds.Min.y);
            maxY = math.max(maxY, decorationBounds.MaxExclusive.y - 1);
            minZ = math.min(minZ, decorationBounds.Min.z);
            maxZ = math.max(maxZ, decorationBounds.MaxExclusive.z - 1);
        }

        private static void IncludeApproachCorner(
            in CastlePlan plan,
            in CastleApproachFrame approach,
            float tangentDistance,
            float outwardDistance,
            ref int minX,
            ref int maxX,
            ref int minZ,
            ref int maxZ)
        {
            int2 local = approach.LocalPoint(tangentDistance, outwardDistance);
            int x = plan.Centre.x + local.x;
            int z = plan.Centre.z + local.y;
            minX = math.min(minX, x);
            maxX = math.max(maxX, x);
            minZ = math.min(minZ, z);
            maxZ = math.max(maxZ, z);
        }
    }
}
