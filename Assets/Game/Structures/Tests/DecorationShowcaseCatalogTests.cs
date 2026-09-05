using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;

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
