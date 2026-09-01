using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class StructuralDecorationHandoffAdapterTests
    {
        [Test]
        public void AcceptedStructuralHandoffProducesWellFormedDecorationSockets()
        {
            var decision = new StructuralDecorationAttachmentHandoff
            {
                SocketId = 0x5151u,
                AttachmentPosition = new int3(10, 20, 30),
                SocketFlags = StructuralSocketFlags.DecorationHandoff,
                DecorationHandoff = StructuralDecorationHandoff.Floor |
                                    StructuralDecorationHandoff.Wall |
                                    StructuralDecorationHandoff.WindowSide,
                Accepted = true,
            };
            var slot = new SlotSpec
            {
                SocketId = decision.SocketId,
                Facing = Facing.East,
            };

            DecorationSocket[] sockets = StructuralDecorationHandoffAdapter.CreateSockets(
                in decision, in slot, parentOrientation: 1);

            Assert.AreEqual(3, sockets.Length);
            Assert.AreEqual(DecorationSocketKind.Floor, sockets[0].Kind);
            Assert.AreEqual(new int3(0, 1, 0), sockets[0].Facing);
            Assert.AreEqual(DecorationSocketKind.Wall, sockets[1].Kind);
            Assert.AreEqual(new int3(0, 0, -1), sockets[1].Facing);
            Assert.AreEqual(DecorationSocketKind.WindowSide, sockets[2].Kind);
            Assert.AreEqual(new int3(0, 0, -1), sockets[2].Facing);
            for (int i = 0; i < sockets.Length; i++)
            {
                Assert.IsTrue(sockets[i].IsWellFormed);
                Assert.AreEqual(decision.AttachmentPosition, sockets[i].Bounds.Min);
                Assert.AreEqual(decision.AttachmentPosition + 1, sockets[i].Bounds.MaxExclusive);
            }
        }

        [Test]
        public void RejectedOrNonHandoffDecisionProducesNoDecorationSockets()
        {
            var slot = new SlotSpec { SocketId = 0x5151u, Facing = Facing.North };
            var rejected = new StructuralDecorationAttachmentHandoff
            {
                SocketId = slot.SocketId,
                SocketFlags = StructuralSocketFlags.DecorationHandoff,
                DecorationHandoff = StructuralDecorationHandoff.Floor,
                Accepted = false,
            };
            var ordinary = new StructuralDecorationAttachmentHandoff
            {
                SocketId = slot.SocketId,
                Accepted = true,
            };

            Assert.AreEqual(0, StructuralDecorationHandoffAdapter.CreateSockets(
                in rejected, in slot, 0).Length);
            Assert.AreEqual(0, StructuralDecorationHandoffAdapter.CreateSockets(
                in ordinary, in slot, 0).Length);
        }

        [Test]
        public void StructuralChildCanDefineDecorationSpaceWithoutRunningDecorationPlacement()
        {
            var instance = new StructuralDecorationInstanceHandoff
            {
                SemanticStructureId = 0x0102030405060708UL,
                InstanceId = 0x1112131415161718UL,
                PieceId = 0x5152u,
                Position = new int3(100, 40, 200),
                Orientation = 1,
            };
            int3 footprint = new int3(12, 20, 28);

            Assert.IsTrue(StructuralDecorationHandoffAdapter.TryCreateSpace(
                in instance, footprint, DecorationSpaceKind.ExteriorYard, out DecorationSpace space));
            Assert.IsTrue(space.IsWellFormed);
            Assert.AreEqual(instance.Position, space.Bounds.Min);
            Assert.AreEqual(instance.Position + new int3(28, 20, 12), space.Bounds.MaxExclusive);
        }
    }
}
