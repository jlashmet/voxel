using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-only projection of a resolved castle layout into interaction and presentation
    /// coordinates. Structures owns semantic/spatial planning; this class owns only application
    /// concerns that must follow the geometry which was actually realized.
    /// </summary>
    internal static class ShowcaseCastleSpatialLayout
    {
        internal static Vector3 PrimaryGateInteractionPosition(
            in CastleSpatialProjection projection,
            float voxelSize)
        {
            float3 point = projection.PrimaryGateGeometry.InteractionPointVoxels;
            return new Vector3(point.x, point.y, point.z) * voxelSize;
        }

        internal static int3[] PrimaryGateLeafVoxels(
            in CastleSpatialProjection projection)
        {
            CastleGateGeometry geometry = projection.PrimaryGateGeometry;
            var voxels = new int3[geometry.RectangularVoxelCount];
            int count = 0;
            for (int index = 0; index < geometry.RectangularVoxelCount; index++)
            {
                if (!geometry.TryGetArchVoxel(index, out int3 voxel, out _)) continue;
                voxels[count++] = voxel;
            }

            if (count == voxels.Length) return voxels;
            Array.Resize(ref voxels, count);
            return voxels;
        }

        internal static Vector3 TrapdoorInteractionPosition(
            in CastleSpatialProjection projection,
            float voxelSize)
        {
            int3 centre = projection.TrapdoorCentre;
            return new Vector3(centre.x + 0.5f, centre.y + 0.2f, centre.z + 0.5f)
                 * voxelSize;
        }

        internal static int3 TrapdoorCentre(in CastleSpatialProjection projection) =>
            projection.TrapdoorCentre;

        internal static void BuildPresentationLights(
            in CastleSpatialProjection projection,
            DungeonPlan dungeon,
            out Vector4[] lights,
            out Vector4[] colours)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            if (!DungeonPlanValidator.TryValidate(dungeon, out DungeonPlanIssue dungeonIssue))
            {
                throw new InvalidOperationException(
                    $"Cannot place castle presentation lights for invalid dungeon: {dungeonIssue}.");
            }

            CastlePlan plan = projection.KeepPlan;
            CastleKeepAnnexPlan annexes = projection.KeepAnnexes;
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = projection.KeepCentreWorld.y - plan.KeepHalfZ;
            int keepCentreZ = projection.KeepCentreWorld.y;
            int keepMaxX = projection.KeepCentreWorld.x + plan.KeepHalfX;
            int wingWidth = math.max(96, plan.KeepHalfX * 4 / 5);
            int wingDepth = math.max(80, plan.KeepHalfZ * 2 - 72);
            int wingCentreX = keepMaxX - 4 + wingWidth / 2;
            int wingCentreZ = keepMinZ + 24 + wingDepth / 2;
            int chapelWidth = math.max(78, plan.KeepHalfX * 2 / 3);
            int chapelDepth = math.max(96, plan.KeepHalfZ * 6 / 5);
            int chapelCentreX = projection.KeepCentreWorld.x - plan.KeepHalfX
                              - chapelWidth / 2 + 4;
            int chapelCentreZ = keepMinZ + plan.KeepHalfZ * 2 - chapelDepth / 2 - 38;
            int3 bellTower = projection.ChapelBellTowerCentre;
            int keepCentreX = projection.KeepCentreWorld.x;

            DungeonRoomPlan hall = FindRequiredRoom(dungeon, DungeonRoomPurpose.GreatHall);
            int hallFloorY = RoomFloorY(in hall);

            var hallWarm = new Vector4(1.00f, 0.38f, 0.10f, 1.85f);
            var upperWarm = new Vector4(1.00f, 0.40f, 0.13f, 1.05f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var sideRoomWarm = new Vector4(1.00f, 0.34f, 0.09f, 1.05f);
            var caveWarm = new Vector4(1.00f, 0.27f, 0.06f, 2.35f);
            var caveBlue = new Vector4(0.10f, 0.58f, 1.00f, 2.05f);

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            var lightList = new List<Vector4>(23);
            var colourList = new List<Vector4>(23);

            void AddLight(int x, int y, int z, float radiusMetres, Vector4 colour)
            {
                lightList.Add(LightAt(x, y, z, radiusMetres));
                colourList.Add(colour);
            }

            AddLight(keepCentreX - 45, baseY + 26, keepCentreZ - 28, 8.0f, hallWarm);
            AddLight(keepCentreX + 42, baseY + 26, keepCentreZ + 30, 8.0f, hallWarm);
            AddLight(keepCentreX, baseY + plan.FloorHeight + 17, keepCentreZ, 8.0f, upperWarm);
            AddLight(keepCentreX, baseY + plan.FloorHeight * 3 + 17, keepCentreZ, 7.0f, upperWarm);

            if (annexes.HasGreatHallWing)
            {
                AddLight(wingCentreX, baseY + 17, wingCentreZ, 7.5f, hallWarm);
                AddLight(wingCentreX, baseY + plan.FloorHeight + 17, wingCentreZ, 7.0f, upperWarm);
            }

            if (annexes.HasChapelWing)
            {
                AddLight(chapelCentreX - 18, baseY + 24, chapelCentreZ, 7.5f, chapelWarm);
                AddLight(chapelCentreX + 22, baseY + 27, chapelCentreZ, 7.5f, chapelWarm);
            }

            if (TryFindRoom(dungeon, DungeonRoomPurpose.Archive, out DungeonRoomPlan archive))
            {
                int archiveFloorY = RoomFloorY(in archive);
                AddLight(archive.Centre.x - 55, archiveFloorY + 17, archive.Centre.z, 7.0f, cellarWarm);
                AddLight(archive.Centre.x + 58, archiveFloorY + 17, archive.Centre.z, 7.0f, cellarWarm);
            }

            AddLight(hall.Centre.x - 55, hallFloorY + 18, hall.Centre.z, 8.5f, cellarWarm);
            AddLight(hall.Centre.x + 55, hallFloorY + 18, hall.Centre.z, 8.5f, cellarWarm);

            if (TryFindRoom(dungeon, DungeonRoomPurpose.Puzzle, out DungeonRoomPlan puzzle))
            {
                AddLight(
                    puzzle.Centre.x,
                    RoomFloorY(in puzzle) + 16,
                    puzzle.Centre.z,
                    8.0f,
                    sideRoomWarm);
            }

            if (TryFindRoom(dungeon, DungeonRoomPurpose.Treasury, out DungeonRoomPlan treasury))
            {
                AddLight(
                    treasury.Centre.x,
                    RoomFloorY(in treasury) + 15,
                    treasury.Centre.z,
                    8.0f,
                    sideRoomWarm);
            }

            if (TryFindRoom(dungeon, DungeonRoomPurpose.CaveThreshold, out DungeonRoomPlan cave))
            {
                int caveFloorY = RoomFloorY(in cave);
                AddLight(cave.Centre.x - 40, caveFloorY + 9, cave.Centre.z - 15, 11.5f, caveWarm);
                AddLight(cave.Centre.x + 45, caveFloorY + 11, cave.Centre.z + 24, 11.5f, caveWarm);
                AddLight(cave.Centre.x + 145, caveFloorY + 12, cave.Centre.z + 25, 10.5f, caveBlue);
            }

            AddLight(
                keepCentreX - 52,
                baseY + plan.FloorHeight + 16,
                keepCentreZ + 27,
                6.5f,
                upperWarm);
            AddLight(
                keepCentreX,
                baseY + plan.FloorHeight * 3 + 17,
                keepCentreZ - 42,
                6.0f,
                upperWarm);
            AddLight(
                keepCentreX,
                baseY + plan.FloorHeight * 3 + 17,
                keepCentreZ + 42,
                6.0f,
                upperWarm);

            if (annexes.HasBellTower)
            {
                AddLight(bellTower.x, baseY + 17, bellTower.z, 5.5f, chapelWarm);
                AddLight(
                    bellTower.x,
                    baseY + plan.FloorHeight * 2 + 17,
                    bellTower.z,
                    5.5f,
                    upperWarm);
                AddLight(
                    bellTower.x,
                    baseY + plan.FloorHeight * 3 + 17,
                    bellTower.z,
                    5.0f,
                    upperWarm);
            }

            lights = lightList.ToArray();
            colours = colourList.ToArray();
            if (lights.Length != colours.Length)
            {
                throw new InvalidOperationException(
                    $"Castle presentation light/colour count mismatch: {lights.Length}/{colours.Length}.");
            }
        }

        private static DungeonRoomPlan FindRequiredRoom(
            DungeonPlan plan,
            DungeonRoomPurpose purpose)
        {
            if (TryFindRoom(plan, purpose, out DungeonRoomPlan room))
                return room;

            throw new InvalidOperationException(
                $"Castle presentation requires dungeon room purpose {purpose}.");
        }

        private static bool TryFindRoom(
            DungeonPlan plan,
            DungeonRoomPurpose purpose,
            out DungeonRoomPlan room)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                if (plan.Rooms[i].Purpose != purpose) continue;
                room = plan.Rooms[i];
                return true;
            }

            room = default;
            return false;
        }

        private static int RoomFloorY(in DungeonRoomPlan room) =>
            room.Centre.y - room.Size.y / 2;
    }
}
