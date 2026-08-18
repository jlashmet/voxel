using System;
using System.Collections.Generic;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministically selects the semantic rooms that fit a guild-house shell. Required rooms are
    /// always selected first; optional rooms compete by authored weight and stable seed. Geometry and
    /// adjacency are intentionally a later stage so this stays independent from any one shell planner.
    /// </summary>
    public static class GuildHouseRoomSelector
    {
        public static GuildHouseRoomProgram[] Select(GuildHouseProgram program, uint seed, int roomCapacity)
        {
            if (roomCapacity <= 0 || program.Rooms.Length == 0)
                return Array.Empty<GuildHouseRoomProgram>();

            var selected = new List<GuildHouseRoomProgram>(Math.Min(roomCapacity, program.Rooms.Length));
            var optional = new List<ScoredRoom>();

            for (var i = 0; i < program.Rooms.Length; i++)
            {
                var room = program.Rooms[i];
                if (room.Required)
                {
                    if (selected.Count < roomCapacity)
                        selected.Add(room);
                }
                else
                {
                    optional.Add(new ScoredRoom(room, Score(seed, program.Kind, room.Role, room.Weight)));
                }
            }

            optional.Sort((a, b) =>
            {
                var score = b.Score.CompareTo(a.Score);
                return score != 0 ? score : a.Room.Role.CompareTo(b.Room.Role);
            });

            for (var i = 0; i < optional.Count && selected.Count < roomCapacity; i++)
                selected.Add(optional[i].Room);

            return selected.ToArray();
        }

        private static uint Score(uint seed, GuildHouseKind kind, GuildHouseRoomRole role, byte weight)
        {
            unchecked
            {
                var x = seed ^ ((uint)kind * 0x9E3779B9u) ^ ((uint)role * 0x85EBCA6Bu);
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                // Weight is deliberately a strong bias, not a hard ordering.
                return x + ((uint)weight << 24);
            }
        }

        private readonly struct ScoredRoom
        {
            public readonly GuildHouseRoomProgram Room;
            public readonly uint Score;

            public ScoredRoom(GuildHouseRoomProgram room, uint score)
            {
                Room = room;
                Score = score;
            }
        }
    }
}
