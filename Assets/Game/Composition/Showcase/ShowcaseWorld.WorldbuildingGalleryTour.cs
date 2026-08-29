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
    /// the same production structure authorers as generated world content; only placement is curated.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
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

        private static readonly string[] s_GalleryTourNames =
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
            "Kentridge district",
            "Hightown district",
            "Moordell district",
            "Rossdam district",
            "Fairy Village district",
            "Orc Village district",
        };

        private static readonly int2[] s_GalleryTourTargetXZ =
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
            s_GalleryTownDistrictCentres[0],
            s_GalleryTownDistrictCentres[1],
            s_GalleryTownDistrictCentres[2],
            s_GalleryTownDistrictCentres[3],
            s_GalleryTownDistrictCentres[4],
            s_GalleryTownDistrictCentres[5],
        };

        private static readonly int[] s_GalleryTourLookHeightVoxels =
        {
            25,
            25,
            48,
            90,
            48,
            48,
            24,
            48,
            68,
            42,
            62,
            38,
            62,
            56,
            50,
        };

        public int WorldbuildingGalleryTourStopCount => s_GalleryTourNames.Length;

        public string WorldbuildingGalleryTourStopName(int index) =>
            s_GalleryTourNames[NormalizeGalleryTourIndex(index)];

        public float3 WorldbuildingGalleryTourSpawnPosition(int index)
        {
            int normalized = NormalizeGalleryTourIndex(index);
            int2 target = s_GalleryTourTargetXZ[normalized];
            int approach = normalized == 3 ? 120 : normalized >= 9 ? 118 : 76;
            int2 spawn = target + new int2(0, -approach);
            int y = TerrainQuery.HeightAt(spawn.x, spawn.y, Seed) + 5;
            return new float3(spawn.x, y, spawn.y) * VoxelSize;
        }

        public float3 WorldbuildingGalleryTourLookTarget(int index)
        {
            int normalized = NormalizeGalleryTourIndex(index);
            int2 target = s_GalleryTourTargetXZ[normalized];
            int y = TerrainQuery.HeightAt(target.x, target.y, Seed) +
                    s_GalleryTourLookHeightVoxels[normalized];
            return new float3(target.x, y, target.y) * VoxelSize;
        }

        /// <summary>
        /// Adds larger semantic-building examples and six reference-driven town districts beside the
        /// original gallery collection. The same method is invoked by generated startup and bake creation,
        /// keeping the production authoring path identical in both modes.
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

            for (int i = 0; i < s_GalleryTownStyleIds.Length; i++)
            {
                string styleId = s_GalleryTownStyleIds[i];
                TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);
                TownArchitectureVoxelPalette palette = GalleryTownPalette(styleId);
                WorldBuilderTownArchitectureVoxelAuthoring.Author(
                    authoring,
                    s_GalleryTownDistrictCentres[i],
                    (x, z) => TerrainQuery.HeightAt(x, z, Seed),
                    program,
                    in palette);
            }
        }

        private static TownArchitectureVoxelPalette GalleryTownPalette(string styleId)
        {
            switch (styleId)
            {
                case WorldBuilderTownArchitectureIds.Kentridge:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryMedium,
                        GameMaterialIds.Tile,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Grass,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.LitWindow);
                case WorldBuilderTownArchitectureIds.Hightown:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryLarge,
                        GameMaterialIds.Slate,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.MasonrySmall,
                        GameMaterialIds.Stone,
                        GameMaterialIds.LitWindow);
                case WorldBuilderTownArchitectureIds.Moordell:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.Stone,
                        GameMaterialIds.Slate,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Dirt,
                        GameMaterialIds.Moss,
                        GameMaterialIds.Gold);
                case WorldBuilderTownArchitectureIds.Rossdam:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.MasonryLarge,
                        GameMaterialIds.Tile,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.MasonryMedium,
                        GameMaterialIds.Gold,
                        GameMaterialIds.Cloth);
                case WorldBuilderTownArchitectureIds.FairyVillage:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.Wood,
                        GameMaterialIds.Moss,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Grass,
                        GameMaterialIds.FlowerWhite,
                        GameMaterialIds.Crystal);
                case WorldBuilderTownArchitectureIds.OrcVillage:
                    return new TownArchitectureVoxelPalette(
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Dirt,
                        GameMaterialIds.Slate,
                        GameMaterialIds.Cloth);
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

            // A shallow exhibit plinth keeps the authored shell readable on uneven showcase terrain
            // without changing the guild-house production authorer itself.
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
            int count = s_GalleryTourNames.Length;
            int normalized = index % count;
            return normalized < 0 ? normalized + count : normalized;
        }
    }
}
