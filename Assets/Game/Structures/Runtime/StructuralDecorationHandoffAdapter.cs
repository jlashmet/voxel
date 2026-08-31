using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Converts accepted structural-composition metadata into the existing fine-detail decoration
    /// contracts. This adapter deliberately stops at spaces/sockets; prop selection and placement
    /// remain owned by the independent Game.Structures decoration pipeline.
    /// </summary>
    public static class StructuralDecorationHandoffAdapter
    {
        public static bool TryCreateSpace(
            in StructuralInstance instance,
            in FeatureDefinition definition,
            DecorationSpaceKind kind,
            out DecorationSpace space)
        {
            space = default;
            if (instance.PieceId == 0 || kind == DecorationSpaceKind.Unknown)
                return false;

            int3 footprint = (instance.Orientation & 1) == 0
                ? definition.Footprint
                : new int3(definition.Footprint.z, definition.Footprint.y, definition.Footprint.x);
            if (math.any(footprint <= 0))
                return false;

            uint structureId = Fold(instance.SemanticStructureId);
            uint instanceId = Fold(instance.InstanceId);
            uint spaceId = DecorationSeed.Derive(structureId, instanceId ^ instance.PieceId);
            space = new DecorationSpace
            {
                SpaceId = spaceId,
                Kind = kind,
                Bounds = new DecorationBounds
                {
                    Min = instance.Position,
                    MaxExclusive = instance.Position + footprint,
                },
            };
            return space.IsWellFormed;
        }

        public static DecorationSocket[] CreateSockets(
            in StructuralAttachmentDecision decision,
            in SlotSpec authoredSlot,
            byte parentOrientation)
        {
            if (!decision.Accepted ||
                decision.SocketId == 0 ||
                decision.SocketId != authoredSlot.SocketId ||
                (decision.SocketFlags & StructuralSocketFlags.DecorationHandoff) == 0 ||
                decision.DecorationHandoff == StructuralDecorationHandoff.None)
                return new DecorationSocket[0];

            int count = CountKinds(decision.DecorationHandoff);
            var sockets = new DecorationSocket[count];
            int cursor = 0;
            Facing worldFacing = RotateFacing(authoredSlot.Facing, parentOrientation);

            Append(decision, StructuralDecorationHandoff.Floor, DecorationSocketKind.Floor,
                new int3(0, 1, 0), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.Wall, DecorationSocketKind.Wall,
                FacingVector(worldFacing), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.Corner, DecorationSocketKind.Corner,
                FacingVector(worldFacing), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.Ceiling, DecorationSocketKind.Ceiling,
                new int3(0, -1, 0), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.Tabletop, DecorationSocketKind.Tabletop,
                new int3(0, 1, 0), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.BesideAnchor, DecorationSocketKind.BesideAnchor,
                FacingVector(worldFacing), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.AboveAnchor, DecorationSocketKind.AboveAnchor,
                new int3(0, 1, 0), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.DoorwaySide, DecorationSocketKind.DoorwaySide,
                FacingVector(worldFacing), sockets, ref cursor);
            Append(decision, StructuralDecorationHandoff.WindowSide, DecorationSocketKind.WindowSide,
                FacingVector(worldFacing), sockets, ref cursor);
            return sockets;
        }

        private static void Append(
            in StructuralAttachmentDecision decision,
            StructuralDecorationHandoff handoff,
            DecorationSocketKind kind,
            int3 facing,
            DecorationSocket[] destination,
            ref int cursor)
        {
            if ((decision.DecorationHandoff & handoff) == 0)
                return;

            uint discriminator = (uint)kind | ((uint)cursor << 16);
            destination[cursor++] = new DecorationSocket
            {
                SocketId = DecorationSeed.Derive(decision.SocketId, discriminator),
                Kind = kind,
                Bounds = new DecorationBounds
                {
                    Min = decision.AttachmentPosition,
                    MaxExclusive = decision.AttachmentPosition + 1,
                },
                Facing = facing,
                AnchorSlotId = 0,
            };
        }

        private static int CountKinds(StructuralDecorationHandoff handoff)
        {
            uint bits = (uint)handoff;
            int count = 0;
            while (bits != 0)
            {
                count += (int)(bits & 1u);
                bits >>= 1;
            }
            return count;
        }

        private static Facing RotateFacing(Facing facing, byte orientation)
        {
            if (facing == Facing.Up || facing == Facing.Down)
                return facing;
            return (Facing)(((int)facing + orientation) & 3);
        }

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return new int3(0, 0, 1);
                case Facing.East: return new int3(1, 0, 0);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.West: return new int3(-1, 0, 0);
                case Facing.Up: return new int3(0, 1, 0);
                case Facing.Down: return new int3(0, -1, 0);
                default: return int3.zero;
            }
        }

        private static uint Fold(ulong value)
        {
            uint folded = (uint)value ^ (uint)(value >> 32);
            return folded == 0 ? 0xA511E9B3u : folded;
        }
    }
}
