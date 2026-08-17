using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Typed bridge from city palette ids to the ordinary structure preset factories. The city
    /// layer owns selection only; it never re-implements archetype geometry.
    /// </summary>
    public static class CityStructurePresetLibrary
    {
        public static bool MatchesArchetype(CityStructureArchetype archetype, CityStructurePresetId preset)
        {
            switch (preset)
            {
                case CityStructurePresetId.CompactCabin:
                case CityStructurePresetId.Farmhouse:
                    return archetype == CityStructureArchetype.House;
                case CityStructurePresetId.StorageShed:
                case CityStructurePresetId.WorkshopShed:
                    return archetype == CityStructureArchetype.Shed;
                case CityStructurePresetId.Chapel:
                case CityStructurePresetId.ParishChurch:
                    return archetype == CityStructureArchetype.Church;
                case CityStructurePresetId.SimpleCathedral:
                case CityStructurePresetId.GothicCathedral:
                    return archetype == CityStructureArchetype.Cathedral;
                case CityStructurePresetId.ClassicalTemple:
                case CityStructurePresetId.CourtyardTemple:
                    return archetype == CityStructureArchetype.Temple;
                case CityStructurePresetId.KeepCastle:
                case CityStructurePresetId.WalledCastle:
                    return archetype == CityStructureArchetype.Castle;
                case CityStructurePresetId.CivicHall:
                    return archetype == CityStructureArchetype.Civic;
                default:
                    return false;
            }
        }

        public static bool TryResolveHouse(
            CityStructurePresetId preset,
            in StructureMaterialPalette palette,
            out HouseConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.CompactCabin:
                    config = HouseStylePresets.CompactCabin(palette.PrimaryWall, palette.Roof);
                    config.Palette = palette;
                    return true;
                case CityStructurePresetId.Farmhouse:
                    config = HouseStylePresets.Farmhouse(palette.PrimaryWall, palette.Roof);
                    config.Palette = palette;
                    return true;
                default:
                    config = default;
                    return false;
            }
        }

        public static bool TryResolveShed(
            CityStructurePresetId preset,
            in StructureMaterialPalette palette,
            out ShedConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.StorageShed:
                    config = ShedPresets.Storage(in palette);
                    return true;
                case CityStructurePresetId.WorkshopShed:
                    config = ShedPresets.Workshop(in palette);
                    return true;
                default:
                    config = default;
                    return false;
            }
        }

        public static bool TryResolveChurch(
            CityStructurePresetId preset,
            in StructureMaterialPalette palette,
            out ChurchConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.Chapel:
                    config = ChurchPresets.Chapel(in palette);
                    return true;
                case CityStructurePresetId.ParishChurch:
                    config = ChurchPresets.ParishChurch(in palette);
                    return true;
                default:
                    config = default;
                    return false;
            }
        }

        public static bool TryResolveCathedral(
            CityStructurePresetId preset,
            in StructureMaterialPalette palette,
            out CathedralWorldbuildingConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.SimpleCathedral:
                    config = CathedralWorldbuildingPresets.Simple(in palette);
                    return true;
                case CityStructurePresetId.GothicCathedral:
                    config = CathedralWorldbuildingPresets.Gothic(in palette);
                    return true;
                default:
                    config = default;
                    return false;
            }
        }

        public static bool TryResolveTemple(
            CityStructurePresetId preset,
            in StructureMaterialPalette palette,
            out TempleConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.ClassicalTemple:
                    config = TemplePresets.ClassicalColumned(in palette);
                    return true;
                case CityStructurePresetId.CourtyardTemple:
                    config = TemplePresets.CourtyardTemple(in palette);
                    return true;
                default:
                    config = default;
                    return false;
            }
        }

        public static bool TryResolveCastle(
            CityStructurePresetId preset,
            in CastlePlan plan,
            in StructureMaterialPalette palette,
            out CastlePresetConfig config)
        {
            switch (preset)
            {
                case CityStructurePresetId.KeepCastle:
                    config = CastlePresets.KeepOnly(in plan, in palette);
                    return true;
                case CityStructurePresetId.WalledCastle:
                    config = CastlePresets.WalledCastle(in plan, in palette);
                    return true;
                default:
                    config = default;
                    return false;
            }
        }
    }
}
