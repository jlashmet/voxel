using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Tour metadata and additional authored exhibits for the worldbuilding gallery. These stops use
    /// the same production structure authorers as generated world content; only placement/evidence is curated.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const int GalleryBaseTourStopCount = 9;
        private const int GalleryTownViewsPerDistrict = 3;

        private static readonly int2 s_GalleryAdventurersGuildOriginXZ = new(-1340, -260);
        private static readonly int2 s_GalleryWizardGuildOriginXZ = new(-1340, 120);

        // Town-architecture districts form a separated 3x2 walkable grid south of the original gallery.
        // Each centre is far enough from its neighbours for the shared 164x132-voxel district footprint.
        private static readonly int2[] s_GalleryTownDistrictCentres =
        {
            new(-1140, -520),
            new(-920, -520),
            new(-700, -520),
            new(-1140, -720),
            new(-920, -720),
            new(-700, -720),
        };

        private static readonly string[] s_GalleryTownStyleIds =
        {
            WorldBuilderTownArchitectureIds.Kentridge,
            WorldBuilderTownArchitectureIds.Hightown,
            WorldBuilderTownArchitectureIds.Moordell,
            WorldBuilderTownArchitectureIds.Rossdam,
            WorldBuilderTownArchitectureIds.FairyVillage,
            WorldBuilderTownArchitectureIds.OrcVillage,
        };

        // Explicit fixed seeds are evidence data, not incidental random state. Reloading the gallery therefore
        // reproduces the same style/detail placement independently of the surrounding showcase world seed.
        private static readonly uint[] s_GalleryTownSeeds =
        {
            0x4B454E54u,
            0x48494748u,
            0x4D4F4F52u,
            0x524F5353u,
            0x46414952u,
            0x4F524353u,
        };

        private static readonly string[] s_GalleryBaseTourNames =
        {
            "Storage shed",
            "Workshop shed",
            "Parish church",
            "Gothic cathedral",
            "Classical temple",
            "Courtyard temple",
            "Cave entrance",
            "Adventurers guild hall",
            "Wizard guild tower",
        };

        private static readonly int2[] s_GalleryBaseTourTargetXZ =
        {
            new(-1120, -260),
            new(-1120, -80),
            new(-900, -290),
            new(-650, -210),
            new(-900, 170),
            new(-650, 180),
            new(-1120, 220),
            new(-1298, -224),
            new(-1306, 154),
        };

        private static readonly int[] s_GalleryBaseTourLookHeightVoxels =
        {
            25, 25, 48, 90, 48, 48, 24, 48, 68,
        };

        public int WorldbuildingGalleryTourStopCount =>
            GalleryBaseTourStopCount + s_GalleryTownDistrictCentres.Length * GalleryTownViewsPerDistrict;

        public int WorldbuildingGalleryTownDistrictCount => s_GalleryTownDistrictCentres.Length;

        public string WorldbuildingGalleryTownStyleId(int districtIndex) =>
            s_GalleryTownStyleIds[NormalizeTownDistrictIndex(districtIndex)];

        public uint WorldbuildingGalleryTownSeed(int districtIndex) =>
            s_GalleryTownSeeds[NormalizeTownDistrictIndex(districtIndex)];

        public int2 WorldbuildingGalleryTownDistrictCentre(int districtIndex) =>
            s_GalleryTownDistrictCentres[NormalizeTownDistrictIndex(districtIndex)];

        public string WorldbuildingGalleryTownAuditSummary(int districtIndex)
        {
            int i = NormalizeTownDistrictIndex(districtIndex);
            TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(s_GalleryTownStyleIds[i], s_GalleryTownSeeds[i]);
            int2 centre = s_GalleryTownDistrictCentres[i];
            return program.DisplayName + " anchor=(" + centre.x + "," + centre.y + ") footprint=" +
                   TownArchitectureDistrictBounds.WidthVoxels + "x" +
                   TownArchitectureDistrictBounds.DepthVoxels + " " +
                   WorldBuilderTownArchitecture.Describe(program);
        }

        public string WorldbuildingGalleryTourStopName(int index)
        {
            int normalized = NormalizeGalleryTourIndex(index);
            if (normalized < GalleryBaseTourStopCount)
                return s_GalleryBaseTourNames[normalized];

            GetTownView(normalized, out int district, out int view);
            string viewName = view == 0 ? "wide/elevated" : view == 1 ? "player facade" : "close detail";
            return WorldBuilderTownArchitecture.Resolve(s_GalleryTownStyleIds[district], s_GalleryTownSeeds[district]).DisplayName +
                   " district — " + viewName;
        }

        public float3 WorldbuildingGalleryTourSpawnPosition(int index)
        {
            int normalized = NormalizeGalleryTourIndex(index);
            if (normalized < GalleryBaseTourStopCount)
            {
                int2 target = s_GalleryBaseTourTargetXZ[normalized];
                int baseApproach = normalized == 3 ? 120 : 76;
                int2 spawn = target + new int2(0, -baseApproach);
                int baseY = TerrainQuery.HeightAt(spawn.x, spawn.y, Seed) + 5;
                return new float3(spawn.x, baseY, spawn.y) * VoxelSize;
            }

            GetTownView(normalized, out int district, out int view);
            return WorldbuildingGalleryTownAuditSpawnPosition(district, view);
        }

        public float3 WorldbuildingGalleryTourLookTarget(int index)
        {
            int normalized = NormalizeGalleryTourIndex(index);
            if (normalized < GalleryBaseTourStopCount)
            {
                int2 target = s_GalleryBaseTourTargetXZ[normalized];
                int baseY = TerrainQuery.HeightAt(target.x, target.y, Seed) + s_GalleryBaseTourLookHeightVoxels[normalized];
                return new float3(target.x, baseY, target.y) * VoxelSize;
            }

            GetTownView(normalized, out int district, out int view);
            return WorldbuildingGalleryTownAuditLookTarget(district, view);
        }

        /// <summary>
        /// Adds larger semantic-building examples and six reference-driven town districts beside the original
        /// gallery collection. Generated startup and offline bake creation both reach the six towns through
        /// <see cref="EnsureWorldbuildingGalleryTownArchitectureBlocking"/>, which is also the compatibility
        /// path for a checked-in gallery bake that predates this feature.
        /// </summary>
        public void GenerateWorldbuildingGalleryTourExpansionBlocking()
        {
            var regions = new HashSet<int3>();
            AddGalleryRegionNeighbourhood(regions, s_GalleryAdventurersGuildOriginXZ, 1);
            AddGalleryRegionNeighbourhood(regions, s_GalleryWizardGuildOriginXZ, 1);
            for (int i = 0; i < s_GalleryTownDistrictCentres.Length; i++)
                AddGalleryRegionNeighbourhood(regions, s_GalleryTownDistrictCentres[i], 1);

            foreach (int3 region in regions)
                GenerateRegionBlocking(region);

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget: 18_000_000);

            AuthorGalleryGuildHouse(
                authoring,
                s_GalleryAdventurersGuildOriginXZ,
                84,
                72,
                GuildHouseKind.Adventurers,
                DecorationRegionTheme.Kentridge,
                0x574247A1u,
                requestedRooms: 8);

            AuthorGalleryGuildHouse(
                authoring,
                s_GalleryWizardGuildOriginXZ,
                68,
                68,
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Hightown,
                0x574247A2u,
                requestedRooms: 8);

            if (authoring.BudgetExceeded)
                throw new System.InvalidOperationException(
                    $"Worldbuilding gallery guild expansion exceeded its {authoring.WriteBudget:N0}-write budget.");

            EnsureWorldbuildingGalleryTownArchitectureBlocking();
        }

        private static void GetTownView(int normalizedTourIndex, out int district, out int view)
        {
            int townViewIndex = normalizedTourIndex - GalleryBaseTourStopCount;
            district = townViewIndex / GalleryTownViewsPerDistrict;
            view = townViewIndex % GalleryTownViewsPerDistrict;
        }

        private static TownArchitectureVoxelPalette GalleryTownPalette(string styleId)
        {
            switch (styleId)
            {
                case WorldBuilderTownArchitectureIds.Kentridge:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryMedium, GameMaterialIds.Tile, GameMaterialIds.Wood,
                        GameMaterialIds.Grass, GameMaterialIds.DarkStone, GameMaterialIds.LitWindow);
                case WorldBuilderTownArchitectureIds.Hightown:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryLarge, GameMaterialIds.Slate, GameMaterialIds.DarkStone,
                        GameMaterialIds.MasonrySmall, GameMaterialIds.Stone, GameMaterialIds.LitWindow);
                case WorldBuilderTownArchitectureIds.Moordell:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.Stone, GameMaterialIds.Slate, GameMaterialIds.Wood,
                        GameMaterialIds.Dirt, GameMaterialIds.Moss, GameMaterialIds.Gold);
                case WorldBuilderTownArchitectureIds.Rossdam:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryLarge, GameMaterialIds.Tile, GameMaterialIds.DarkStone,
                        GameMaterialIds.MasonryMedium, GameMaterialIds.Gold, GameMaterialIds.Cloth);
                case WorldBuilderTownArchitectureIds.FairyVillage:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.Wood, GameMaterialIds.Moss, GameMaterialIds.Wood,
                        GameMaterialIds.Grass, GameMaterialIds.FlowerWhite, GameMaterialIds.Crystal);
                case WorldBuilderTownArchitectureIds.OrcVillage:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.DarkStone, GameMaterialIds.Wood, GameMaterialIds.Wood,
                        GameMaterialIds.Dirt, GameMaterialIds.Slate, GameMaterialIds.Cloth);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(styleId), styleId, "Unsupported gallery town style.");
            }
        }

        private void AuthorGalleryGuildHouse(
            IStructureAuthoringSession authoring,
            int2 originXZ,
            int width,
            int depth,
            GuildHouseKind kind,
            DecorationRegionTheme region,
            uint structureId,
            int requestedRooms)
        {
            int3 origin = Grounded(originXZ);
            authoring.Box(
                new int3(origin.x - 4, origin.y - 5, origin.z - 4),
                new int3(width + 8, 6, depth + 8),
                GameMaterialIds.MasonrySmall);

            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                kind,
                region,
                Seed,
                structureId,
                origin,
                width,
                depth,
                requestedRooms);

            if (!GuildHouseFurnishedPrototypeAuthoring.TryAuthor(authoring, in prototype))
                GuildHousePrototypeAuthoring.Author(authoring, in prototype);
        }

        private static int NormalizeGalleryTourIndex(int index)
        {
            int count = GalleryBaseTourStopCount + s_GalleryTownDistrictCentres.Length * GalleryTownViewsPerDistrict;
            int normalized = index % count;
            return normalized < 0 ? normalized + count : normalized;
        }

        private static int NormalizeTownDistrictIndex(int index)
        {
            int count = s_GalleryTownDistrictCentres.Length;
            int normalized = index % count;
            return normalized < 0 ? normalized + count : normalized;
        }
    }
}
