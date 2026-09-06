using System;
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
            Assert.AreEqual(DecorationShowcaseCatalog.Count, entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                Assert.IsTrue(entry.IsWellFormed, $"Entry {i} was malformed.");
                Assert.IsTrue(identities.Add(entry.StableId), $"Duplicate showcase identity {entry.StableId}.");
                sourceCounts[(int)entry.Source]++;
            }

            Assert.Multiple(() =>
            {
                Assert.AreEqual(440, DecorationShowcaseCatalog.RegisteredDecorationCount);
                Assert.AreEqual(25, DecorationShowcaseCatalog.PresetCount);
                Assert.AreEqual(8, DecorationShowcaseCatalog.MineCaveCount);
                Assert.AreEqual(8, DecorationShowcaseCatalog.NaturalCaveCount);
                Assert.AreEqual(48, DecorationShowcaseCatalog.WorldObjectCount);
                Assert.AreEqual(DecorationShowcaseCatalog.RegisteredDecorationCount,
                    sourceCounts[(int)DecorationShowcaseEntrySource.RegisteredDecoration]);
                Assert.AreEqual(DecorationShowcaseCatalog.PresetCount,
                    sourceCounts[(int)DecorationShowcaseEntrySource.Preset]);
                Assert.AreEqual(DecorationShowcaseCatalog.MineCaveCount,
                    sourceCounts[(int)DecorationShowcaseEntrySource.MineCave]);
                Assert.AreEqual(DecorationShowcaseCatalog.NaturalCaveCount,
                    sourceCounts[(int)DecorationShowcaseEntrySource.NaturalCave]);
                Assert.AreEqual(DecorationShowcaseCatalog.WorldObjectCount,
                    sourceCounts[(int)DecorationShowcaseEntrySource.WorldObject]);
            });
        }

        [Test]
        public void EveryListedEntryCreatesOneProductionRealization()
        {
            DecorationContext context = Context();
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();

            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                Assert.IsTrue(
                    DecorationShowcaseRealizer.TryCreate(in entry, in context, out DecorationShowcaseRealization realization),
                    $"{entry.StableId} had no production realization.");
                Assert.IsTrue(realization.IsWellFormed, $"{entry.StableId} produced a malformed realization.");
                Assert.AreEqual(entry.StableId, realization.Entry.StableId);
            }
        }

        [Test]
        public void RegisteredDecorationEntriesResolveThroughOwningProductionCatalogues()
        {
            DecorationContext context = Context();
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                if (entry.Source != DecorationShowcaseEntrySource.RegisteredDecoration)
                    continue;
                count++;
                Assert.IsTrue(
                    DecorationShowcaseCatalog.TryDescribeDecoration(in context, entry.SourceId, out DecorationPropDescriptor descriptor),
                    $"Registered decoration {entry.StableId} did not resolve through its owning catalogue.");
                Assert.IsTrue(descriptor.IsWellFormed, $"Registered decoration {entry.StableId} returned a malformed descriptor.");
            }
            Assert.AreEqual(DecorationShowcaseCatalog.RegisteredDecorationCount, count);
        }

        [Test]
        public void EveryRegisteredProceduralDecorationUsesTheProductionGeometryConsumer()
        {
            DecorationContext context = Context();
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();
            int proceduralCount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                if (entry.Source != DecorationShowcaseEntrySource.RegisteredDecoration)
                    continue;
                Assert.IsTrue(
                    DecorationShowcaseRealizer.TryCreate(in entry, in context, out DecorationShowcaseRealization realization),
                    $"Registered decoration {entry.StableId} did not realize.");
                if (realization.Decoration.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;

                proceduralCount++;
                DecorationProceduralMeshRequest[] requests =
                    DecorationProceduralMeshHookPlanner.Collect(new[] { realization.Decoration });
                Assert.AreEqual(1, requests.Length, $"{entry.StableId} emitted no procedural request.");
                Assert.AreEqual(realization.Decoration.Id, requests[0].Id,
                    $"{entry.StableId} lost canonical prop identity.");
                Assert.IsTrue(
                    DecorationProceduralGeometryBuilder.TryBuild(in requests[0], out DecorationProceduralGeometry geometry),
                    $"{entry.StableId} has no production geometry realization.");
                Assert.IsTrue(geometry.IsWellFormed, $"{entry.StableId} produced malformed geometry.");
            }

            Assert.Greater(proceduralCount, 0, "The canonical catalogue unexpectedly contained no procedural decorations.");
        }

        [Test]
        public void PresetCaveAndWorldObjectEntriesTrackTheirCanonicalEnums()
        {
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();
            var presetIds = SourceIds(entries, DecorationShowcaseEntrySource.Preset);
            var mineIds = SourceIds(entries, DecorationShowcaseEntrySource.MineCave);
            var naturalIds = SourceIds(entries, DecorationShowcaseEntrySource.NaturalCave);
            var worldIds = SourceIds(entries, DecorationShowcaseEntrySource.WorldObject);

            AssertEnumBacked<DecorationShowcasePresetKind>(presetIds, plusOneForZeroBased: false);
            AssertEnumBacked<MineCaveDecorationKind>(mineIds, plusOneForZeroBased: true);
            AssertEnumBacked<NaturalCaveDecorationKind>(naturalIds, plusOneForZeroBased: true);

            WorldObjectKind[] worldKinds = WorldObjectCatalogQuery.Kinds();
            Assert.AreEqual(worldKinds.Length, worldIds.Count);
            for (int i = 0; i < worldKinds.Length; i++)
            {
                ushort id = Convert.ToUInt16(worldKinds[i]);
                Assert.IsTrue(worldIds.Contains(id), $"World object {worldKinds[i]} was not enumerated.");
                Assert.IsTrue(DecorationShowcaseCatalog.TryGetWorldObjectPreset(id, out WorldObjectPreset preset));
                Assert.AreEqual(worldKinds[i], preset.Kind);
            }
        }

        private static HashSet<ushort> SourceIds(
            DecorationShowcaseEntry[] entries,
            DecorationShowcaseEntrySource source)
        {
            var result = new HashSet<ushort>();
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].Source == source)
                    Assert.IsTrue(result.Add(entries[i].SourceId), $"Duplicate source id {entries[i].SourceId} for {source}.");
            return result;
        }

        private static void AssertEnumBacked<TEnum>(HashSet<ushort> sourceIds, bool plusOneForZeroBased)
            where TEnum : struct
        {
            Array values = Enum.GetValues(typeof(TEnum));
            Assert.AreEqual(values.Length, sourceIds.Count);
            for (int i = 0; i < values.Length; i++)
            {
                ushort raw = Convert.ToUInt16(values.GetValue(i));
                ushort expected = plusOneForZeroBased ? (ushort)(raw + 1) : raw;
                Assert.IsTrue(sourceIds.Contains(expected), $"Canonical {typeof(TEnum).Name} value {values.GetValue(i)} was not enumerated.");
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
