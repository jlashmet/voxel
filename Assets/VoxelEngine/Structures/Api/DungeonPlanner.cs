using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Deterministically resolves designed dungeon semantics into rooms and connections. The caller
    /// supplies the scale/depth envelope and an independent dungeon seed; voxel realization and
    /// natural cave generation remain downstream concerns.
    /// </summary>
    public static class DungeonPlanner
    {
        public static DungeonPlan Create(uint seed, in DungeonPlanningConstraints constraints)
        {
            ValidateConstraints(in constraints);

            var rooms = new List<DungeonRoomPlan>(6);
            var connections = new List<DungeonConnectionPlan>(5);

            int entranceId = AddRoom(
                rooms,
                DungeonRoomPurpose.Entrance,
                constraints.Entrance + new int3(0, 2, 0),
                new int3(32, 4, 32));

            int previousId = entranceId;
            if (constraints.IncludeArchive)
            {
                int archiveFloorY = constraints.Entrance.y - constraints.UpperLevelDrop;
                int archiveId = AddRoom(
                    rooms,
                    DungeonRoomPurpose.Archive,
                    new int3(
                        constraints.Entrance.x,
                        archiveFloorY + constraints.RoomHeight / 2,
                        constraints.Entrance.z),
                    new int3(
                        math.max(40, constraints.MainHallHalfX * 2 - 40),
                        constraints.RoomHeight,
                        math.max(40, constraints.MainHallHalfZ * 2 - 40)));
                Connect(connections, previousId, archiveId, DungeonConnectionKind.Stair);
                previousId = archiveId;
            }

            int mainFloorY = constraints.Entrance.y - constraints.MainLevelDrop;
            int3 mainCentre = new int3(
                constraints.Entrance.x,
                mainFloorY + constraints.RoomHeight / 2,
                constraints.Entrance.z);
            int hallId = AddRoom(
                rooms,
                DungeonRoomPurpose.GreatHall,
                mainCentre,
                new int3(
                    constraints.MainHallHalfX * 2,
                    constraints.RoomHeight,
                    constraints.MainHallHalfZ * 2));
            Connect(connections, previousId, hallId, DungeonConnectionKind.Stair);

            uint variation = Mix(seed ^ 0xD06E0A11u);
            int puzzleSide = (variation & 1u) == 0u ? 1 : -1;
            int treasurySide = -puzzleSide;

            if (constraints.IncludePuzzle)
            {
                int puzzleId = AddSideRoom(
                    rooms,
                    DungeonRoomPurpose.Puzzle,
                    in constraints,
                    mainCentre,
                    puzzleSide);
                Connect(connections, hallId, puzzleId, DungeonConnectionKind.Corridor);
            }

            if (constraints.IncludeTreasury)
            {
                int treasuryId = AddSideRoom(
                    rooms,
                    DungeonRoomPurpose.Treasury,
                    in constraints,
                    mainCentre,
                    treasurySide);
                Connect(connections, hallId, treasuryId, DungeonConnectionKind.Corridor);
            }

            int caveThresholdRoomId = -1;
            if (constraints.IncludeCaveExit)
            {
                int caveDirection = (variation & 2u) == 0u ? -1 : 1;
                int caveDistance = constraints.MainHallHalfZ + constraints.CavePassageLength;
                caveThresholdRoomId = AddRoom(
                    rooms,
                    DungeonRoomPurpose.CaveThreshold,
                    mainCentre + new int3(0, 0, caveDirection * caveDistance),
                    new int3(32, constraints.RoomHeight, 32));
                Connect(
                    connections,
                    hallId,
                    caveThresholdRoomId,
                    DungeonConnectionKind.SecretPassage);
            }

            var plan = new DungeonPlan(
                seed,
                constraints.Entrance,
                rooms.ToArray(),
                connections.ToArray(),
                entranceId,
                caveThresholdRoomId);
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Dungeon planner produced an invalid plan: {issue}.");
            }

            return plan;
        }

        private static int AddSideRoom(
            List<DungeonRoomPlan> rooms,
            DungeonRoomPurpose purpose,
            in DungeonPlanningConstraints constraints,
            int3 mainCentre,
            int side)
        {
            return AddRoom(
                rooms,
                purpose,
                mainCentre + new int3(side * constraints.SideRoomOffset, 0, 0),
                new int3(
                    constraints.SideRoomHalfX * 2,
                    constraints.RoomHeight,
                    constraints.SideRoomHalfZ * 2));
        }

        private static int AddRoom(
            List<DungeonRoomPlan> rooms,
            DungeonRoomPurpose purpose,
            int3 centre,
            int3 size)
        {
            int id = rooms.Count;
            rooms.Add(new DungeonRoomPlan
            {
                Id = id,
                Purpose = purpose,
                Centre = centre,
                Size = size,
            });
            return id;
        }

        private static void Connect(
            List<DungeonConnectionPlan> connections,
            int from,
            int to,
            DungeonConnectionKind kind)
        {
            connections.Add(new DungeonConnectionPlan
            {
                FromRoomId = from,
                ToRoomId = to,
                Kind = kind,
            });
        }

        private static void ValidateConstraints(in DungeonPlanningConstraints constraints)
        {
            if (constraints.RoomHeight < 24)
                throw new ArgumentOutOfRangeException(nameof(constraints.RoomHeight));
            if (constraints.UpperLevelDrop < constraints.RoomHeight + 4)
                throw new ArgumentOutOfRangeException(nameof(constraints.UpperLevelDrop));
            if (constraints.MainLevelDrop <
                constraints.UpperLevelDrop + constraints.RoomHeight + 8)
                throw new ArgumentOutOfRangeException(nameof(constraints.MainLevelDrop));
            if (constraints.MainHallHalfX < 30 || constraints.MainHallHalfZ < 30)
                throw new ArgumentOutOfRangeException(nameof(constraints.MainHallHalfX));
            if (constraints.SideRoomHalfX < 20 || constraints.SideRoomHalfZ < 20)
                throw new ArgumentOutOfRangeException(nameof(constraints.SideRoomHalfX));
            if ((constraints.IncludePuzzle || constraints.IncludeTreasury) &&
                constraints.SideRoomOffset <=
                constraints.MainHallHalfX + constraints.SideRoomHalfX + 8)
                throw new ArgumentOutOfRangeException(nameof(constraints.SideRoomOffset));
            if (constraints.IncludeCaveExit && constraints.CavePassageLength < 48)
                throw new ArgumentOutOfRangeException(nameof(constraints.CavePassageLength));
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }
    }
}
