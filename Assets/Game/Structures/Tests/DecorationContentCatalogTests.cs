using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class DecorationContentCatalogTests
    {
        [Test]
        public void FirstFortyTwoArchetypesAreStableWellFormedAndRoundTripVariantIdentity()
        {
            DecorationContext context = Context(17u);
            var seenVariants = new uint[DecorationContentCatalog.KindCount];

            for (int raw = 1; raw <= DecorationContentCatalog.KindCount; raw++)
            {
                DecorationContentKind kind = (DecorationContentKind)raw;
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                uint sceneId = 0x43415431u; // CAT1
                uint slotId = (uint)(1000 + raw);
                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, sceneId, slotId, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(DecorationContentCatalog.IsDefined(kind), $"Kind {kind} was not defined.");
                    Assert.IsTrue(recipe.IsWellFormed, $"Recipe {kind} malformed.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Descriptor {kind} malformed.");
                    Assert.AreEqual(recipe.ProxyFamily, descriptor.Family, $"Proxy family mismatch for {kind}.");
                    Assert.IsTrue(DecorationContentVariants.IsContent(descriptor.Variant), $"Variant marker missing for {kind}.");
                    Assert.AreEqual(kind, DecorationContentVariants.KindOf(descriptor.Variant), $"Variant kind failed round trip for {kind}.");
                });
                seenVariants[raw - 1] = descriptor.Variant;
            }

            for (int i = 0; i < seenVariants.Length; i++)
            for (int j = i + 1; j < seenVariants.Length; j++)
                Assert.AreNotEqual(seenVariants[i], seenVariants[j], $"Archetype variants {i + 1}/{j + 1} collided.");
        }

        [Test]
        public void SevenInitialSceneDefinitionsReuseCoreSchedulerAndResolverAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int sceneRaw = 0; sceneRaw <= (int)DecorationContentSceneKind.CivicCorner; sceneRaw++)
            {
                DecorationContentSceneKind sceneKind = (DecorationContentSceneKind)sceneRaw;
                DecorationContentSceneSlot[] slots = DecorationContentSceneCatalog.CreateSlots(sceneKind);
                Assert.Greater(slots.Length, 0, $"Scene {sceneKind} had no slots.");

                for (uint seed = 1; seed <= 16; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationContentSceneResolver.TryResolve(
                        sceneKind, in space, in context, null, out DecorationPlacement[] placements),
                        $"Scene {sceneKind} failed for seed {seed}.");

                    AssertRequiredSlots(slots, placements, sceneKind, seed);
                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.Multiple(() =>
                        {
                            Assert.IsTrue(placements[i].IsWellFormed, $"Scene {sceneKind} placement {i} malformed for seed {seed}.");
                            Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds), $"Scene {sceneKind} placement {i} escaped space.");
                            Assert.IsTrue(DecorationContentVariants.IsContent(placements[i].Variant), $"Scene {sceneKind} placement {i} lost archetype identity.");
                        });
                        for (int j = i + 1; j < placements.Length; j++)
                        {
                            Assert.AreNotEqual(placements[i].Id, placements[j].Id,
                                $"Scene {sceneKind} placements {i}/{j} shared an ID.");
                            Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                                $"Scene {sceneKind} placements {i}/{j} overlapped for seed {seed}.");
                        }
                    }
                }
            }
        }

        [Test]
        public void SmithyWorkClusterStaysInFrontOfForgeHearthAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= 32; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentSceneResolver.TryResolve(
                    DecorationContentSceneKind.Smithy,
                    in space,
                    in context,
                    null,
                    out DecorationPlacement[] placements),
                    $"Smithy failed for seed {seed}.");

                DecorationPlacement hearth = FindPlacement(placements, 1u);
                DecorationPlacement anvil = FindPlacement(placements, 2u);
                DecorationPlacement bellows = FindPlacement(placements, 3u);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(hearth.IsWellFormed);
                    Assert.IsTrue(anvil.IsWellFormed);
                    Assert.IsTrue(bellows.IsWellFormed);
                });
                AssertNearWorkingFace(in hearth, in anvil, "anvil", seed);
                AssertNearWorkingFace(in hearth, in bellows, "bellows", seed);
            }
        }

        [Test]
        public void GenericAuthoringGrammarHandlesGeometryAndPartitionsMeshAndThinContent()
        {
            DecorationContext context = Context(91u);
            var placements = new[]
            {
                Placement(DecorationContentKind.Anvil, DecorationRenderBackend.BoxAssembly,
                    new DecorationBounds { Min = new int3(0, 0, 0), MaxExclusive = new int3(12, 10, 8) }, new int3(0, 1, 0), 1u),
                Placement(DecorationContentKind.Well, DecorationRenderBackend.VoxelStamp,
                    new DecorationBounds { Min = new int3(30, 0, 0), MaxExclusive = new int3(50, 15, 20) }, new int3(0, 1, 0), 2u),
                Placement(DecorationContentKind.LampPost, DecorationRenderBackend.BoxAssembly,
                    new DecorationBounds { Min = new int3(60, 0, 0), MaxExclusive = new int3(65, 24, 5) }, new int3(0, 1, 0), 3u),
                Placement(DecorationContentKind.HangingScale, DecorationRenderBackend.ProceduralMesh,
                    new DecorationBounds { Min = new int3(80, 40, 0), MaxExclusive = new int3(88, 52, 8) }, new int3(0, -1, 0), 4u),
                Placement(DecorationContentKind.MerchantSign, DecorationRenderBackend.ThinSurface,
                    new DecorationBounds { Min = new int3(100, 10, 0), MaxExclusive = new int3(116, 22, 1) }, new int3(0, 0, 1), 5u),
            };
            var authoring = new RecordingAuthoringSession();

            Assert.IsTrue(DecorationContentAuthoringEmitter.TryAuthorGeometry(authoring, placements, in context));
            DecorationContentMeshRequest[] mesh = DecorationContentAuthoringEmitter.CollectMeshRequests(placements);
            DecorationContentThinSurfaceRequest[] thin = DecorationContentAuthoringEmitter.CollectThinSurfaceRequests(placements);

            Assert.Multiple(() =>
            {
                Assert.Greater(authoring.BoxCalls, 0);
                Assert.Greater(authoring.CylinderCalls, 0);
                Assert.AreEqual(1, mesh.Length);
                Assert.AreEqual(DecorationContentKind.HangingScale, mesh[0].Kind);
                Assert.AreEqual(1, thin.Length);
                Assert.AreEqual(DecorationContentKind.MerchantSign, thin[0].Kind);
            });
        }

        private static void AssertNearWorkingFace(
            in DecorationPlacement anchor,
            in DecorationPlacement placement,
            string label,
            uint seed)
        {
            int anchorX = (anchor.Bounds.Min.x + anchor.Bounds.MaxExclusive.x) / 2;
            int anchorZ = (anchor.Bounds.Min.z + anchor.Bounds.MaxExclusive.z) / 2;
            int itemX = (placement.Bounds.Min.x + placement.Bounds.MaxExclusive.x) / 2;
            int itemZ = (placement.Bounds.Min.z + placement.Bounds.MaxExclusive.z) / 2;
            int dx = itemX - anchorX;
            int dz = itemZ - anchorZ;
            int forward = dx * anchor.Facing.x + dz * anchor.Facing.z;
            int lateral = math.abs(dx * anchor.Facing.z - dz * anchor.Facing.x);

            Assert.Multiple(() =>
            {
                Assert.Greater(forward, 0, $"Smithy {label} was not in front of hearth for seed {seed}.");
                Assert.LessOrEqual(forward, 80, $"Smithy {label} was too far from hearth for seed {seed}.");
                Assert.LessOrEqual(lateral, 60, $"Smithy {label} drifted too far laterally for seed {seed}.");
            });
        }

        private static DecorationPlacement FindPlacement(DecorationPlacement[] placements, uint slotId)
        {
            if (placements != null)
            {
                for (int i = 0; i < placements.Length; i++)
                    if (placements[i].SlotId == slotId)
                        return placements[i];
            }
            return default;
        }

        private static void AssertRequiredSlots(
            DecorationContentSceneSlot[] slots,
            DecorationPlacement[] placements,
            DecorationContentSceneKind scene,
            uint seed)
        {
            for (int s = 0; s < slots.Length; s++)
            {
                if (!slots[s].Required)
                    continue;
                bool found = false;
                for (int p = 0; p < placements.Length; p++)
                    if (placements[p].SlotId == slots[s].SlotId)
                        found = true;
                Assert.IsTrue(found, $"Scene {scene} missing required slot {slots[s].SlotId} for seed {seed}.");
            }
        }

        private static DecorationPlacement Placement(
            DecorationContentKind kind,
            DecorationRenderBackend backend,
            DecorationBounds bounds,
            int3 facing,
            uint slotId)
        {
            DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
            return new DecorationPlacement
            {
                Id = new GeneratedPropId(0xC071E17000000000ul + slotId),
                SceneId = 0x434F4E31u,
                SlotId = slotId,
                Family = recipe.ProxyFamily,
                Backend = backend,
                Interaction = recipe.Interaction,
                Bounds = bounds,
                Facing = facing,
                Variant = DecorationContentVariants.Encode(kind, slotId * 17u),
            };
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xC071E17u,
            SpaceId = 0xC071E18u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xC071E18u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-160, 0, -140),
                MaxExclusive = new int3(160, 80, 140),
            },
        };

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            public int CylinderCalls { get; private set; }
            public int BoxCalls { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) => BoxCalls++;
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) => CylinderCalls++;
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
