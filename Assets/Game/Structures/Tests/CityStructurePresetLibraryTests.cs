using Game.Structures.Api;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CityStructurePresetLibraryTests
    {
        private static StructureMaterialPalette Palette => new StructureMaterialPalette
        {
            Foundation = 1,
            PrimaryWall = 2,
            SecondaryWall = 3,
            Trim = 4,
            Roof = 5,
            Floor = 6,
            Column = 7,
            Accent = 8,
            Underground = 9,
            Opening = 0,
            Glass = 10,
            Detail = 11,
        };

        [Test]
        public void WeightedEntries_RejectMismatchedPresetArchetypes()
        {
            CityPaletteEntry invalid = new CityPaletteEntry
            {
                Archetype = CityStructureArchetype.House,
                PresetId = CityStructurePresetId.GothicCathedral,
                Districts = CityDistrictMask.Residential,
                Weight = 1,
                MinimumBuildableWidth = 48,
                MinimumBuildableDepth = 48,
            };

            Assert.That(invalid.IsWellFormed, Is.False);
        }

        [Test]
        public void DefaultTownPalette_UsesOnlyRealPresetBindings()
        {
            CityConfig config = CityPresets.MixedTown();
            for (int i = 0; i < config.Palette.Length; i++)
                Assert.That(CityStructurePresetLibrary.MatchesArchetype(
                    config.Palette[i].Archetype, config.Palette[i].PresetId), Is.True);
            for (int i = 0; i < config.Landmarks.Length; i++)
                Assert.That(CityStructurePresetLibrary.MatchesArchetype(
                    config.Landmarks[i].Archetype, config.Landmarks[i].PresetId), Is.True);
        }

        [Test]
        public void TypedBindings_ReturnOrdinaryArchetypeConfigs()
        {
            StructureMaterialPalette palette = Palette;

            Assert.That(CityStructurePresetLibrary.TryResolveHouse(
                CityStructurePresetId.Farmhouse, in palette, out HouseConfig house), Is.True);
            Assert.That(house.IsWellFormed, Is.True);

            Assert.That(CityStructurePresetLibrary.TryResolveShed(
                CityStructurePresetId.WorkshopShed, in palette, out ShedConfig shed), Is.True);
            Assert.That(shed.IsWellFormed, Is.True);

            Assert.That(CityStructurePresetLibrary.TryResolveChurch(
                CityStructurePresetId.Chapel, in palette, out ChurchConfig church), Is.True);
            Assert.That(church.IsWellFormed, Is.True);

            Assert.That(CityStructurePresetLibrary.TryResolveCathedral(
                CityStructurePresetId.SimpleCathedral, in palette,
                out CathedralWorldbuildingConfig cathedral), Is.True);
            Assert.That(cathedral.IsWellFormed, Is.True);

            Assert.That(CityStructurePresetLibrary.TryResolveTemple(
                CityStructurePresetId.ClassicalTemple, in palette, out TempleConfig temple), Is.True);
            Assert.That(temple.IsWellFormed, Is.True);
        }
    }
}
