using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationShowcaseCatalogTests
    {
        [Test]
        public void CatalogueEnumeratesEveryCanonicalEntryExactlyOnce()
        {
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();
            var identities = new HashSet<string>();
            var sourceCounts = new int[5];

            Assert.AreEqual(529, entries.Length);
            Assert.AreEqual(440, DecorationShowcaseCatalog.RegisteredDecorationCount);

            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                Assert.IsTrue(entry.IsWellFormed, $"Entry {i} was malformed.");
                Assert.IsTrue(identities.Add(entry.StableId), $"Duplicate showcase identity {entry.StableId}.");
                sourceCounts[(int)entry.Source]++;
            }

            Assert.Multiple(() =>
            {
                Assert.AreEqual(440, sourceCounts[(int)DecorationShowcaseEntrySource.RegisteredDecoration]);
                Assert.AreEqual(25, sourceCounts[(int)DecorationShowcaseEntrySource.Preset]);
                Assert.AreEqual(8, sourceCounts[(int)DecorationShowcaseEntrySource.MineCave]);
                Assert.AreEqual(8, sourceCounts[(int)DecorationShowcaseEntrySource.NaturalCave]);
                Assert.AreEqual(48, sourceCounts[(int)DecorationShowcaseEntrySource.WorldObject]);
                Assert.AreEqual("decoration:1", entries[0].StableId);
                Assert.AreEqual("world-object:48", entries[entries.Length - 1].StableId);
            });
        }

        [Test]
        public void EveryRegisteredDecorationResolvesThroughItsOwningProductionCatalogue()
        {
            DecorationContext context = Context();
            for (ushort id = 1; id <= DecorationShowcaseCatalog.RegisteredDecorationCount; id++)
            {
                Assert.IsTrue(
                    DecorationShowcaseCatalog.TryDescribeDecoration(in context, id, out DecorationPropDescriptor descriptor),
                    $"Registered decoration {id} did not resolve through its owning catalogue.");
                Assert.IsTrue(descriptor.IsWellFormed, $"Registered decoration {id} returned a malformed descriptor.");
            }
        }

        [Test]
        public void EveryRegisteredProceduralDecorationUsesTheProductionGeometryConsumer()
        {
            DecorationContext context = Context();
            int proceduralCount = 0;

            for (ushort id = 1; id <= DecorationShowcaseCatalog.RegisteredDecorationCount; id++)
            {
                Assert.IsTrue(
                    DecorationShowcaseCatalog.TryDescribeDecoration(in context, id, out DecorationPropDescriptor descriptor),
                    $"Registered decoration {id} did not resolve.");
                if (descriptor.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;

                proceduralCount++;
                var placement = new DecorationPlacement
                {
                    Id = GeneratedPropIds.Create(in context, DecorationShowcaseCatalog.PreviewSceneId, id),
                    SceneId = DecorationShowcaseCatalog.PreviewSceneId,
                    SlotId = id,
                    Family = descriptor.Family,
                    Backend = descriptor.Backend,
                    Interaction = descriptor.Interaction,
                    Bounds = new DecorationBounds
                    {
                        Min = int3.zero,
                        MaxExclusive = descriptor.Size,
                    },
                    Facing = new int3(0, 0, 1),
                    Variant = descriptor.Variant,
                };

                DecorationProceduralMeshRequest[] requests =
                    DecorationProceduralMeshHookPlanner.Collect(new[] { placement });
                Assert.AreEqual(1, requests.Length, $"Registered procedural decoration {id} emitted no request.");
                Assert.IsTrue(requests[0].Id.IsWellFormed, $"Registered procedural decoration {id} lost canonical identity.");
                Assert.IsTrue(
                    DecorationProceduralGeometryBuilder.TryBuild(in requests[0], out DecorationProceduralGeometry geometry),
                    $"Registered procedural decoration {id} has no production geometry realization.");
                Assert.IsTrue(geometry.IsWellFormed, $"Registered procedural decoration {id} produced malformed geometry.");
            }

            Assert.Greater(proceduralCount, 0, "The canonical catalogue unexpectedly contained no procedural decorations.");
        }

        [Test]
        public void EveryReusablePresetFactoryResolvesFromOneProductionOwnedEnumeration()
        {
            DecorationContext context = Context();
            for (ushort id = 1; id <= DecorationShowcaseCatalog.PresetCount; id++)
            {
                var kind = (DecorationShowcasePresetKind)id;
                Assert.IsTrue(
                    DecorationShowcaseCatalog.TryDescribePreset(in context, kind, out DecorationPropDescriptor descriptor),
                    $"Preset {kind} did not resolve.");
                Assert.IsTrue(descriptor.IsWellFormed, $"Preset {kind} returned a malformed descriptor.");
            }
        }

        [Test]
        public void EveryWorldObjectEntryDelegatesToTheProductionWorldObjectCatalogue()
        {
            for (ushort id = 1; id <= DecorationShowcaseCatalog.WorldObjectCount; id++)
            {
                Assert.IsTrue(DecorationShowcaseCatalog.TryGetWorldObjectPreset(id, out WorldObjectPreset preset),
                    $"World object {id} did not resolve.");
                Assert.AreEqual((WorldObjectKind)id, preset.Kind);
            }
        }

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 0x50525031u,
            StructureId = 0x50525032u,
            SpaceId = 0x50525033u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 17u),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };
    }
}
