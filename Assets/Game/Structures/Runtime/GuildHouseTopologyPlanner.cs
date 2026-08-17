using System;

namespace Game.Structures.Runtime
{
    public readonly struct GuildHouseRoomNode
    {
        public readonly GuildHouseRoomProgram Room;
        public readonly byte Depth;
        public readonly int ParentIndex;
        public readonly bool HiddenAccess;

        public GuildHouseRoomNode(GuildHouseRoomProgram room, byte depth, int parentIndex, bool hiddenAccess)
        {
            Room = room;
            Depth = depth;
            ParentIndex = parentIndex;
            HiddenAccess = hiddenAccess;
        }
    }

    /// <summary>
    /// Produces a deterministic semantic adjacency spine before any rectangular/polygonal shell is
    /// allocated. Public rooms stay shallow, operational rooms sit in the middle, and vault/hidden
    /// spaces are pushed deeper. Secretive guilds can mark deep HiddenRoom/Vault access as concealed.
    /// </summary>
    public static class GuildHouseTopologyPlanner
    {
        public static GuildHouseRoomNode[] Plan(GuildHouseProgram program, GuildHouseRoomProgram[] selected)
        {
            if (selected == null || selected.Length == 0)
                return Array.Empty<GuildHouseRoomNode>();

            var ordered = (GuildHouseRoomProgram[])selected.Clone();
            Array.Sort(ordered, CompareRooms);
            var nodes = new GuildHouseRoomNode[ordered.Length];
            var secretive = (program.Traits & GuildHouseTrait.Secretive) != 0;

            for (var i = 0; i < ordered.Length; i++)
            {
                var depth = DesiredDepth(ordered[i].Role);
                // Keep the graph connected as a simple semantic spine/branch seed. Spatial allocation
                // may later add more adjacency edges, but never needs to infer public/private order.
                var parent = i == 0 ? -1 : FindParent(nodes, i, depth);
                var hidden = secretive && depth >= 3
                    && (ordered[i].Role == GuildHouseRoomRole.HiddenRoom
                        || ordered[i].Role == GuildHouseRoomRole.Vault);
                nodes[i] = new GuildHouseRoomNode(ordered[i], depth, parent, hidden);
            }

            return nodes;
        }

        private static int CompareRooms(GuildHouseRoomProgram a, GuildHouseRoomProgram b)
        {
            var depth = DesiredDepth(a.Role).CompareTo(DesiredDepth(b.Role));
            if (depth != 0) return depth;
            if (a.Required != b.Required) return a.Required ? -1 : 1;
            return a.Role.CompareTo(b.Role);
        }

        private static int FindParent(GuildHouseRoomNode[] nodes, int count, byte depth)
        {
            for (var i = count - 1; i >= 0; i--)
                if (nodes[i].Depth <= depth)
                    return i;
            return 0;
        }

        private static byte DesiredDepth(GuildHouseRoomRole role)
        {
            switch (role)
            {
                case GuildHouseRoomRole.EntryHall:
                case GuildHouseRoomRole.ContractHall:
                case GuildHouseRoomRole.PerformanceHall:
                    return 0;
                case GuildHouseRoomRole.CommonHall:
                case GuildHouseRoomRole.Shrine:
                case GuildHouseRoomRole.Garden:
                case GuildHouseRoomRole.TrophyHall:
                case GuildHouseRoomRole.Stable:
                    return 1;
                case GuildHouseRoomRole.Library:
                case GuildHouseRoomRole.Workshop:
                case GuildHouseRoomRole.TrainingRoom:
                case GuildHouseRoomRole.Kitchen:
                case GuildHouseRoomRole.Infirmary:
                case GuildHouseRoomRole.Dormitory:
                    return 2;
                case GuildHouseRoomRole.GuildmasterOffice:
                case GuildHouseRoomRole.RitualRoom:
                case GuildHouseRoomRole.Vault:
                    return 3;
                case GuildHouseRoomRole.HiddenRoom:
                    return 4;
                default:
                    return 2;
            }
        }
    }
}
