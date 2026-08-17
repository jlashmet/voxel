using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Visible mixed-archetype city demo over the ordinary structure authoring session. This layer
    /// owns no structure geometry: city planning selects preset ids and each placement is delegated
    /// to the same shed/church/temple/cathedral authorer used by standalone content.
    /// </summary>
    public static class ShowcaseMixedCityAuthoring
    {
        public static CityConfig BuildConfig()
        {
            CityConfig config = CityPresets.MixedTown();
            config.OpenSpacePermille = 70;
            config.ResidentialDensityPermille = 960;
            config.MixedDensityPermille = 940;
            config.CivicDensityPermille = 920;
            config.Lot.OccupancyPermille = 980;

            // The showcase session path deliberately uses archetypes with direct session authorers.
            // Houses remain exercised by ShowcaseDetailedHouseCatalogue and by the city preset
            // selection tests; no second house geometry implementation is introduced here.
            config.Palette.Clear();
            config.Palette.Add(new CityPaletteEntry
            {
                Archetype = CityStructureArchetype.Shed,
                PresetId = CityStructurePresetId.StorageShed,
                Districts = CityDistrictMask.All,
                Weight = 4,
                MinimumBuildableWidth = 40,
                MinimumBuildableDepth = 32,
            });
            config.Palette.Add(new CityPaletteEntry
            {
                Archetype = CityStructureArchetype.Shed,
                PresetId = CityStructurePresetId.WorkshopShed,
                Districts = CityDistrictMask.Residential | CityDistrictMask.Mixed,
                Weight = 3,
                MinimumBuildableWidth = 48,
                MinimumBuildableDepth = 40,
            });
            config.Palette.Add(new CityPaletteEntry
            {
                Archetype = CityStructureArchetype.Church,
                PresetId = CityStructurePresetId.Chapel,
                Districts = CityDistrictMask.Civic | CityDistrictMask.Sacred | CityDistrictMask.Mixed,
                Weight = 2,
                MinimumBuildableWidth = 48,
                MinimumBuildableDepth = 80,
            });

            config.Landmarks.Clear();
            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Temple,
                PresetId = CityStructurePresetId.ClassicalTemple,
                Districts = CityDistrictMask.All,
                MinimumBuildableWidth = 80,
                MinimumBuildableDepth = 72,
                EveryNthEligibleLot = 9,
                Priority = 10,
            });
            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Cathedral,
                PresetId = CityStructurePresetId.SimpleCathedral,
                Districts = CityDistrictMask.Civic | CityDistrictMask.Sacred,
                MinimumBuildableWidth = 88,
                MinimumBuildableDepth = 80,
                EveryNthEligibleLot = 13,
                Priority = 20,
            });
            return config;
        }

        public static int Author(
            IStructureAuthoringSession authoring,
            ulong citySeed,
            int3 cityOrigin,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            CityConfig config = BuildConfig();
            int authored = 0;

            for (int i = 0; i < config.CandidateCount; i++)
            {
                if (CityPlanner.ResolveCandidate(in config, citySeed, cityOrigin, i,
                        out CityPlacement placement) != CityCandidateResult.Placed)
                    continue;

                if (AuthorPlacement(authoring, in placement, in palette))
                    authored++;
            }

            return authored;
        }

        private static bool AuthorPlacement(
            IStructureAuthoringSession authoring,
            in CityPlacement placement,
            in StructureMaterialPalette palette)
        {
            switch (placement.Archetype)
            {
                case CityStructureArchetype.Shed:
                    if (!CityStructurePresetLibrary.TryResolveShed(
                            placement.PresetId, in palette, out ShedConfig shed))
                        return false;
                    // Shed configs use a min-corner local origin; center them in the planned lot.
                    var shedOrigin = new int3(
                        placement.StructureOrigin.x - shed.Width / 2,
                        placement.StructureOrigin.y,
                        placement.StructureOrigin.z - shed.Depth / 2);
                    ShedAuthoring.Author(authoring, shedOrigin, in shed);
                    return true;

                case CityStructureArchetype.Church:
                    if (!CityStructurePresetLibrary.TryResolveChurch(
                            placement.PresetId, in palette, out ChurchConfig church))
                        return false;
                    church.EntryFacing = placement.Facing;
                    ChurchAuthoring.Author(authoring, placement.StructureOrigin, in church);
                    return true;

                case CityStructureArchetype.Temple:
                    if (!CityStructurePresetLibrary.TryResolveTemple(
                            placement.PresetId, in palette, out TempleConfig temple))
                        return false;
                    temple.EntryFacing = placement.Facing;
                    TempleAuthoring.Author(authoring, placement.StructureOrigin, in temple);
                    return true;

                case CityStructureArchetype.Cathedral:
                    if (!CityStructurePresetLibrary.TryResolveCathedral(
                            placement.PresetId, in palette, out CathedralWorldbuildingConfig cathedral))
                        return false;
                    cathedral.Cathedral.Church.EntryFacing = placement.Facing;
                    CathedralWorldbuildingAuthoring.Author(
                        authoring, placement.StructureOrigin, in cathedral);
                    return true;

                default:
                    return false;
            }
        }
    }
}
