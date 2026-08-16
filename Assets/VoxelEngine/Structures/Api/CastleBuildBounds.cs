using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative world-voxel envelope for one fully resolved spatial castle build.
    /// Min is inclusive and MaxExclusive is exclusive. The envelope deliberately includes
    /// headroom around authored decoration so streaming dependencies remain safe when small
    /// realization details move without changing the semantic plan.
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

            IncludePlannedDungeon(
                spatial.Dungeon,
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

        private static void IncludePlannedDungeon(
            DungeonPlan dungeon,
            ref int minX,
            ref int maxX,
            ref int minY,
            ref int maxY,
            ref int minZ,
            ref int maxZ)
        {
            if (dungeon == null)
                return;

            if (!DungeonPlanValidator.TryValidate(dungeon, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle dependency bounds require a valid dungeon plan: {issue}.");
            }

            DungeonRoomPlan[] rooms = dungeon.Rooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                DungeonRoomPlan room = rooms[i];
                int3 half = (room.Size + 1) / 2;
                const int roomPadding = 16;
                minX = math.min(minX, room.Centre.x - half.x - roomPadding);
                maxX = math.max(maxX, room.Centre.x + half.x + roomPadding);
                minY = math.min(minY, room.Centre.y - half.y - roomPadding);
                maxY = math.max(maxY, room.Centre.y + half.y + roomPadding);
                minZ = math.min(minZ, room.Centre.z - half.z - roomPadding);
                maxZ = math.max(maxZ, room.Centre.z + half.z + roomPadding);
            }

            if (!dungeon.HasCaveExit)
                return;

            // CastleCaveRealizer owns natural geometry beyond the semantic CaveThreshold. Its
            // current authored cavern/side-cave envelope reaches less than ~200 voxels sideways,
            // ~140 along Z and ~80 vertically from the threshold floor. Reserve a round margin so
            // either seeded cave direction remains safe and modest cave-detail growth does not
            // require retuning streaming admission immediately.
            DungeonRoomPlan threshold = rooms[dungeon.CaveThresholdRoomId];
            int caveFloorY = threshold.Centre.y - threshold.Size.y / 2;
            const int caveHorizontalPadding = 256;
            const int caveDownPadding = 64;
            const int caveUpPadding = 128;
            minX = math.min(minX, threshold.Centre.x - caveHorizontalPadding);
            maxX = math.max(maxX, threshold.Centre.x + caveHorizontalPadding);
            minY = math.min(minY, caveFloorY - caveDownPadding);
            maxY = math.max(maxY, caveFloorY + caveUpPadding);
            minZ = math.min(minZ, threshold.Centre.z - caveHorizontalPadding);
            maxZ = math.max(maxZ, threshold.Centre.z + caveHorizontalPadding);
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
