using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectContentImplementationTests
    {
        [Test]
        public void EveryRegisteredKindEmitsGeometry()
        {
            foreach (WorldObjectKind kind in System.Enum.GetValues(typeof(WorldObjectKind)))
            {
                if (kind == WorldObjectKind.Unknown) continue;
                WorldObjectDescriptor descriptor = Descriptor(kind);
                WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in descriptor, null);
                var authoring = new CountingAuthoringSession();

                Assert.IsTrue(WorldObjectGeometryEmitter.Emit(authoring, in state), kind.ToString());
                Assert.Greater(authoring.WriteOperations, 0, $"{kind} emitted no geometry operations.");
            }
        }

        [Test]
        public void EveryGameplayKindHasConcreteInteraction()
        {
            foreach (WorldObjectKind kind in System.Enum.GetValues(typeof(WorldObjectKind)))
            {
                if (kind == WorldObjectKind.Unknown || kind == WorldObjectKind.SpawnPoint) continue;
                WorldObjectDescriptor descriptor = Descriptor(kind);
                WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in descriptor, null);
                WorldObjectInteraction interaction = kind == WorldObjectKind.PressurePlate
                    ? WorldObjectInteraction.Enter
                    : WorldObjectInteraction.Primary;

                Assert.IsTrue(WorldObjectBehavior.TryInteract(in state, interaction, out WorldObjectInteractionResult result),
                    $"{kind} has no concrete primary interaction.");
                Assert.IsTrue(result.Changed, $"{kind} interaction did not change runtime state.");
                Assert.AreEqual(descriptor.Id, result.Delta.Id);
            }
        }

        [Test]
        public void StaticOnlyEmissionSkipsDynamicProxyObjectsButKeepsStaticProps()
        {
            WorldObjectDescriptor door = Descriptor(WorldObjectKind.Door);
            WorldObjectDescriptor bed = Descriptor(WorldObjectKind.Bed);
            var all = new CountingAuthoringSession();
            var staticOnly = new CountingAuthoringSession();

            WorldObjectGeneratedContent.EmitAll(all, new[] { door, bed }, null,
                WorldObjectGeometryEmissionMode.AllVoxel);
            WorldObjectGeneratedContent.EmitAll(staticOnly, new[] { door, bed }, null,
                WorldObjectGeometryEmissionMode.StaticOnly);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(WorldObjectPresentationPlanner.RequiresDynamicProxy(WorldObjectKind.Door));
                Assert.IsFalse(WorldObjectPresentationPlanner.RequiresDynamicProxy(WorldObjectKind.Bed));
                Assert.Greater(all.WriteOperations, staticOnly.WriteOperations);
                Assert.Greater(staticOnly.WriteOperations, 0,
                    "StaticOnly must still emit static furniture/decoration geometry.");
            });
        }

        [Test]
        public void DoorInteractionPersistsOpenCloseAndAttackSemantics()
        {
            WorldObjectDescriptor descriptor = Descriptor(WorldObjectKind.Door);
            WorldObjectResolvedState closed = WorldObjectStateResolver.Resolve(in descriptor, null);
            Assert.IsTrue(WorldObjectBehavior.TryInteract(in closed, WorldObjectInteraction.Primary, out WorldObjectInteractionResult opened));
            Assert.IsTrue((opened.Delta.State & WorldObjectStateFlags.Open) != 0);
            Assert.AreEqual(WorldObjectSignal.Opened, opened.Signal);

            var store = new WorldObjectStateStore();
            store.Set(in opened.Delta);
            WorldObjectResolvedState open = WorldObjectStateResolver.Resolve(in descriptor, store);
            Assert.IsTrue(WorldObjectBehavior.TryInteract(in open, WorldObjectInteraction.Attack, out WorldObjectInteractionResult destroyed));
            Assert.IsTrue((destroyed.Delta.State & WorldObjectStateFlags.Destroyed) != 0);
            Assert.IsFalse((destroyed.Delta.State & WorldObjectStateFlags.Open) != 0);
            Assert.AreEqual(WorldObjectSignal.Destroyed, destroyed.Signal);
        }

        [Test]
        public void ContainerOpensThenLoots()
        {
            WorldObjectDescriptor descriptor = Descriptor(WorldObjectKind.Chest);
            WorldObjectResolvedState closed = WorldObjectStateResolver.Resolve(in descriptor, null);
            Assert.IsTrue(WorldObjectBehavior.TryInteract(in closed, WorldObjectInteraction.Primary, out WorldObjectInteractionResult opened));

            var store = new WorldObjectStateStore();
            store.Set(in opened.Delta);
            WorldObjectResolvedState open = WorldObjectStateResolver.Resolve(in descriptor, store);
            Assert.IsTrue(WorldObjectBehavior.TryInteract(in open, WorldObjectInteraction.Primary, out WorldObjectInteractionResult looted));
            Assert.IsTrue((looted.Delta.State & WorldObjectStateFlags.Looted) != 0);
            Assert.AreEqual(WorldObjectSignal.Looted, looted.Signal);
        }

        private static WorldObjectDescriptor Descriptor(WorldObjectKind kind)
        {
            WorldObjectPreset preset = WorldObjectContentCatalog.Get(kind);
            Assert.AreEqual(kind, preset.Kind, $"No content preset registered for {kind}.");
            return new WorldObjectDescriptor
            {
                Id = new WorldObjectId((ulong)kind + 1000UL),
                Kind = kind,
                Capabilities = preset.Capabilities,
                Bounds = new DecorationBounds { Min = int3.zero, MaxExclusive = new int3(12, 16, 12) },
                Facing = new int3(0, 0, 1),
                ParentId = 1,
                LocalKey = (uint)kind + 1,
                DefaultState = preset.DefaultState,
                Parameter0 = preset.Parameter0,
                Parameter1 = preset.Parameter1,
                Parameter2 = preset.Parameter2,
                Parameter3 = preset.Parameter3,
            };
        }

        private sealed class CountingAuthoringSession : IStructureAuthoringSession
        {
            public int WriteOperations { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => WriteOperations;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) => WriteOperations++;
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle, byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => WriteOperations++;
            public void Coat(int x, int y, int z, byte coating) => WriteOperations++;
            public void FillBulk(int3 min, int3 size, byte material) => WriteOperations++;
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) => WriteOperations++;
            public void Box(int3 min, int3 size, byte material) => WriteOperations++;
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) => WriteOperations++;
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) => WriteOperations++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => WriteOperations++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => WriteOperations++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => WriteOperations++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => WriteOperations++;
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) => WriteOperations++;
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) => WriteOperations++;
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) => WriteOperations++;
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) => WriteOperations++;
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) => WriteOperations++;
            public void Carve(int3 min, int3 size) => WriteOperations++;
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) => WriteOperations++;
        }
    }
}
