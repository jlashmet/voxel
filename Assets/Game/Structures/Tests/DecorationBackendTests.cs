using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class DecorationBackendTests
    {
        [Test]
        public void CampfireVoxelStampAuthorsWorldIntegratedGeometry()
        {
            DecorationContext context = Context(DecorationConditionTier.Maintained);
            DecorationPropDescriptor descriptor = CaveCampPropPresets.Campfire(in context);
            DecorationPlacement placement = Placement(
                DecorationPropFamily.Campfire,
                descriptor.Backend,
                descriptor.Interaction,
                new DecorationBounds
                {
                    Min = new int3(10, 4, 20),
                    MaxExclusive = new int3(17, 8, 27),
                },
                new int3(0, 1, 0));
            var authoring = new RecordingAuthoringSession();

            bool authored = DecorationVoxelStampBackend.TryAuthor(
                authoring, in placement, in context);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(authored);
                Assert.AreEqual(DecorationRenderBackend.VoxelStamp, descriptor.Backend);
                Assert.AreEqual(1, authoring.CylinderCalls);
                Assert.AreEqual(2, authoring.BoxCalls);
                Assert.AreEqual(1, authoring.DiscCalls);
            });
        }

        [Test]
        public void ProceduralMeshPlacementsBecomeDataOnlyRequests()
        {
            DecorationPlacement placement = Placement(
                DecorationPropFamily.Banner,
                DecorationRenderBackend.ProceduralMesh,
                DecorationInteractionFlags.Destructible,
                new DecorationBounds
                {
                    Min = new int3(-4, 12, 30),
                    MaxExclusive = new int3(8, 32, 31),
                },
                new int3(0, 0, -1));

            DecorationProceduralMeshRequest[] requests =
                DecorationProceduralMeshHookPlanner.Collect(new[] { placement });

            Assert.Multiple(() =>
            {
                Assert.AreEqual(1, requests.Length);
                Assert.AreEqual(placement.Id, requests[0].Id);
                Assert.AreEqual(DecorationPropFamily.Banner, requests[0].Family);
                Assert.AreEqual(placement.Bounds.Min, requests[0].Bounds.Min);
                Assert.AreEqual(placement.Facing, requests[0].Facing);
                Assert.AreEqual(placement.Variant, requests[0].Variant);
            });
        }

        [Test]
        public void EffectHooksRespectConditionWithoutChangingSemanticIdentity()
        {
            DecorationPlacement torch = Placement(
                DecorationPropFamily.WallTorch,
                DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible |
                DecorationInteractionFlags.EmitsLight |
                DecorationInteractionFlags.EmitsParticles,
                new DecorationBounds
                {
                    Min = new int3(2, 8, 4),
                    MaxExclusive = new int3(5, 16, 7),
                },
                new int3(1, 0, 0));
            DecorationContext maintained = Context(DecorationConditionTier.Maintained);
            DecorationContext abandoned = Context(DecorationConditionTier.Abandoned);
            DecorationContext ruined = Context(DecorationConditionTier.Ruined);

            DecorationEffectHook[] maintainedHooks =
                DecorationEffectHookPlanner.Collect(new[] { torch }, in maintained);
            DecorationEffectHook[] abandonedHooks =
                DecorationEffectHookPlanner.Collect(new[] { torch }, in abandoned);
            DecorationEffectHook[] ruinedHooks =
                DecorationEffectHookPlanner.Collect(new[] { torch }, in ruined);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(2, maintainedHooks.Length);
                Assert.AreEqual(DecorationEffectKind.Light, maintainedHooks[0].Kind);
                Assert.AreEqual(DecorationEffectKind.Particles, maintainedHooks[1].Kind);
                Assert.AreEqual(torch.Id, maintainedHooks[0].Id);
                Assert.AreEqual(torch.Id, maintainedHooks[1].Id);
                Assert.AreEqual(1, abandonedHooks.Length);
                Assert.AreEqual(DecorationEffectKind.Particles, abandonedHooks[0].Kind);
                Assert.AreEqual(0, ruinedHooks.Length);
                Assert.AreEqual(torch.Id.Value, Placement(
                    torch.Family, torch.Backend, torch.Interaction, torch.Bounds, torch.Facing).Id.Value);
            });
        }

        private static DecorationContext Context(DecorationConditionTier condition) => new DecorationContext
        {
            WorldSeed = 17u,
            StructureId = 18u,
            SpaceId = 19u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Frontier, 20u),
            StructureKind = DecorationStructureKind.Cave,
            SpaceKind = DecorationSpaceKind.CaveChamber,
            Wealth = DecorationWealthTier.Modest,
            Condition = condition,
            Environment = DecorationEnvironmentTags.Underground | DecorationEnvironmentTags.Damp,
        };

        private static DecorationPlacement Placement(
            DecorationPropFamily family,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            DecorationBounds bounds,
            int3 facing) => new DecorationPlacement
        {
            Id = new GeneratedPropId(0x1234567812345678ul),
            SceneId = 1u,
            SlotId = 1u,
            Family = family,
            Backend = backend,
            Interaction = interaction,
            Bounds = bounds,
            Facing = facing,
            Variant = 7u,
        };

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            public int CylinderCalls { get; private set; }
            public int BoxCalls { get; private set; }
            public int DiscCalls { get; private set; }

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(
                int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) => BoxCalls++;
            public void HollowBox(
                int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(
                int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) => CylinderCalls++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => DiscCalls++;
            public void Cone(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(
                int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(
                int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(
                int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(
                int3 min, int width, int height, int depth,
                int depthAxis, byte material) { }
            public void Stairs(
                int3 min, int width, int steps, int rise, int run,
                int axis, byte material) { }
            public void SpiralStair(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(
                int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
