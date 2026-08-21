using System.Collections.Generic;
using Game.Composition.WorldObjects.Runtime;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Curated worldbuilding district for the dedicated gallery scene. The gallery writes through
    /// the same authoritative voxel store and structure authoring session as the normal showcase;
    /// only exhibit placement is showcase-specific.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const int GalleryCentreX = -920;
        private const int GalleryCentreZ = -80;
        private const uint GalleryDecorationParent = 0x57424744u; // WBGD
        private const uint GalleryCaveParent = 0x57424356u;       // WBCV

        private static readonly int2[] s_GalleryExhibitXZ =
        {
            new(-1120, -260), // storage shed
            new(-1120, -80),  // workshop / lean-to
            new(-900, -290),  // parish church
            new(-650, -210),  // gothic cathedral
            new(-900, 170),   // classical temple
            new(-650, 180),   // courtyard temple
            new(-1120, 220),  // cave entrance
        };

        /// <summary>
        /// Voxel coordinate the gallery cave's main path ends at, which is where its chamber — and
        /// therefore its world-object scene — sits. Authoring produces this; a bake carries it so a
        /// restored gallery can bind the same chamber without re-running cave generation.
        /// </summary>
        public int3 GalleryCavePathEnd { get; private set; }

        /// <summary>True once this world holds gallery content, whether authored or restored.</summary>
        public bool HasGalleryContent { get; private set; }

        /// <summary>Adopts the gallery identity carried by a restored bake.</summary>
        private void RestoreGalleryMetadata(ShowcaseWorldBake bake)
        {
            HasGalleryContent = bake.HasGallery;
            GalleryCavePathEnd = bake.GalleryCavePathEnd;
        }

        /// <summary>
        /// Brings the gallery world up, from the baked image when there is one.
        ///
        /// This is the only startup entry the gallery scene needs. Authoring the district during
        /// play costs a castle plus a 48-million-voxel structure pass before the first frame; the
        /// baked path installs the same voxels from storage snapshots and binds presentation.
        /// </summary>
        public void StartWorldbuildingGalleryBlocking(WorldObjectRuntimeComposition worldObjects)
        {
            if (_startupSource == ShowcaseStartupSource.Generate)
            {
                GenerateWorldbuildingGalleryBlocking(worldObjects);
                GenerateWorldbuildingGalleryTourExpansionBlocking();
                return;
            }

            LoadBake(LoadBakeResource(
                ShowcaseWorldBakeCodec.GalleryResourcePath,
                "Worldbuilding Gallery",
                "Bake Worldbuilding Gallery World"));

            if (!HasGalleryContent)
                throw new System.InvalidOperationException(
                    "The Worldbuilding Gallery bake carries no gallery district. It was probably " +
                    "produced by the Voxel Showcase baker. Re-run " +
                    "Tools > Voxel Engine > Bake Worldbuilding Gallery World.");

            EnsureCastleWorldObjectSceneLoaded();
            RestoreWorldbuildingGalleryFromBake(worldObjects);
        }

        /// <summary>
        /// Binds the gallery's world-object scenes to an already-populated world.
        ///
        /// Voxels come from the bake, so nothing here may emit geometry. Decoration placements are
        /// recomputed rather than stored: they are a pure function of the gallery centre and the
        /// terrain height under it, so carrying them in the bake would duplicate state that is
        /// cheaper to derive than to serialize.
        /// </summary>
        public void RestoreWorldbuildingGalleryFromBake(WorldObjectRuntimeComposition worldObjects)
        {
            if (!HasGalleryContent)
                throw new System.InvalidOperationException(
                    "The gallery bake must be loaded before its world objects can be restored.");
            if (worldObjects == null) return;

            worldObjects.LoadDecorations(
                GalleryDecorationParent,
                BuildGalleryDecorationPlacements());

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget: 1);

            worldObjects.LoadMineCaveWithoutGeometry(
                authoring,
                Seed,
                GalleryCaveParent,
                GalleryCaveChamber(GalleryCavePathEnd));
        }

        private static DecorationBounds GalleryCaveChamber(int3 mainPathEnd) => new DecorationBounds
        {
            Min = mainPathEnd + new int3(-18, 0, -18),
            MaxExclusive = mainPathEnd + new int3(19, 18, 19),
        };

        /// <summary>
        /// Builds the entire gallery synchronously once at scene startup. This is deliberately a
        /// showcase bootstrap operation: all normal frame-to-frame terrain streaming remains budgeted.
        /// </summary>
        public void GenerateWorldbuildingGalleryBlocking(WorldObjectRuntimeComposition worldObjects)
        {
            // Keep the original showcase castle as the eastern landmark and as a live example of
            // castle interiors, dungeon/cave composition, decorations, and interactable gates.
            GenerateCastleOriginBlocking();

            PreloadGalleryRegions();

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget: 48_000_000);

            StructureMaterialPalette palette = GalleryPalette();

            int3 storageShed = Grounded(s_GalleryExhibitXZ[0]);
            ShedConfig storage = ShedPresets.Storage(in palette);
            ShedAuthoring.Author(authoring, storageShed, in storage);

            int3 workshopShed = Grounded(s_GalleryExhibitXZ[1]);
            ShedConfig workshop = ShedPresets.Workshop(in palette);
            ShedAuthoring.Author(authoring, workshopShed, in workshop);

            int3 parishOrigin = Grounded(s_GalleryExhibitXZ[2]);
            ChurchConfig parish = ChurchPresets.ParishChurch(in palette);
            parish.EntryFacing = Facing.South;
            ChurchAuthoring.Author(authoring, parishOrigin, in parish);

            int3 cathedralOrigin = Grounded(s_GalleryExhibitXZ[3]);
            CathedralConfig cathedral = CathedralPresets.Gothic(in palette);
            cathedral.Church.EntryFacing = Facing.South;
            CathedralAuthoring.Author(authoring, cathedralOrigin, in cathedral);

            int3 classicalTempleOrigin = Grounded(s_GalleryExhibitXZ[4]);
            TempleConfig classicalTemple = TemplePresets.ClassicalColumned(in palette);
            classicalTemple.EntryFacing = Facing.South;
            TempleAuthoring.Author(authoring, classicalTempleOrigin, in classicalTemple);

            int3 courtyardTempleOrigin = Grounded(s_GalleryExhibitXZ[5]);
            TempleConfig courtyardTemple = TemplePresets.CourtyardTemple(in palette);
            courtyardTemple.EntryFacing = Facing.South;
            TempleAuthoring.Author(authoring, courtyardTempleOrigin, in courtyardTemple);

            AuthorGalleryPromenade(authoring);
            CaveAuthoringResult caveResult = AuthorGalleryCave(authoring);
            GalleryCavePathEnd = caveResult.MainPathEnd;
            HasGalleryContent = true;

            if (worldObjects != null)
            {
                worldObjects.LoadDecorations(
                    GalleryDecorationParent,
                    BuildGalleryDecorationPlacements());

                worldObjects.LoadMineCave(
                    authoring,
                    Seed,
                    GalleryCaveParent,
                    GalleryCaveChamber(caveResult.MainPathEnd));
            }
        }

        /// <summary>
        /// Player start for the gallery, in metres.
        ///
        /// Beside the promenade rather than on it. The promenade is a slab poured at one altitude
        /// taken from the district centre, and the terrain falls away toward its southern end, so
        /// the old start — dead centre on the axis — resolved onto the underside of an overhanging
        /// masonry deck and opened the scene looking into solid stone. Standing clear of the slab
        /// on natural ground gives the same view up the axis with nothing between the camera and
        /// the exhibits.
        /// </summary>
        public float3 WorldbuildingGallerySpawnPosition()
        {
            const int SpawnX = GalleryCentreX - 60;
            const int SpawnZ = GalleryCentreZ - 300;
            int y = TerrainQuery.HeightAt(SpawnX, SpawnZ, Seed) + 5;
            return new float3(SpawnX, y, SpawnZ) * VoxelSize;
        }

        /// <summary>
        /// Ground-level centre of the gallery district, in metres. This is the gallery's landmark:
        /// the fixed point scripted survey and recede runs orbit and back away from, the way the
        /// original showcase uses its castle.
        /// </summary>
        public float3 WorldbuildingGalleryCentreMetres()
        {
            int y = TerrainQuery.HeightAt(GalleryCentreX, GalleryCentreZ, Seed);
            return new float3(GalleryCentreX, y, GalleryCentreZ) * VoxelSize;
        }

        /// <summary>Initial camera target for a broad view down the gallery promenade.</summary>
        public float3 WorldbuildingGalleryLookTarget()
        {
            int y = TerrainQuery.HeightAt(GalleryCentreX, GalleryCentreZ, Seed) + 70;
            return new float3(GalleryCentreX, y, GalleryCentreZ) * VoxelSize;
        }

        private void PreloadGalleryRegions()
        {
            var regions = new HashSet<int3>();
            AddGalleryRegionNeighbourhood(regions, new int2(GalleryCentreX, GalleryCentreZ), 2);
            for (int i = 0; i < s_GalleryExhibitXZ.Length; i++)
                AddGalleryRegionNeighbourhood(regions, s_GalleryExhibitXZ[i], 1);

            foreach (int3 region in regions)
                GenerateRegionBlocking(region);
        }

        private static void AddGalleryRegionNeighbourhood(
            HashSet<int3> regions,
            int2 voxelXZ,
            int radius)
        {
            float3 metres = new float3(voxelXZ.x * VoxelSize, BaseHeight * VoxelSize,
                                       voxelXZ.y * VoxelSize);
            int3 centre = RegionAt(metres);
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
                regions.Add(centre + new int3(x, 0, z));
        }

        private int3 Grounded(int2 xz)
        {
            int y = TerrainQuery.HeightAt(xz.x, xz.y, Seed) + 1;
            return new int3(xz.x, y, xz.y);
        }

        private void AuthorGalleryPromenade(IStructureAuthoringSession authoring)
        {
            int y = TerrainQuery.HeightAt(GalleryCentreX, GalleryCentreZ, Seed) + 3;

            // A raised masonry promenade makes the collection read as an intentional exhibition
            // garden even on uneven procedural terrain. Short cross-plazas lead toward each row.
            authoring.Box(
                new int3(GalleryCentreX - 24, y - 3, GalleryCentreZ - 330),
                new int3(48, 4, 650),
                GameMaterialIds.MasonrySmall);
            authoring.Box(
                new int3(GalleryCentreX - 245, y - 3, GalleryCentreZ - 22),
                new int3(490, 4, 44),
                GameMaterialIds.MasonrySmall);

            // Trim bands and low planters break up the long central axis.
            for (int z = GalleryCentreZ - 280; z <= GalleryCentreZ + 280; z += 80)
            {
                authoring.Box(
                    new int3(GalleryCentreX - 34, y, z - 8),
                    new int3(8, 10, 16),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(GalleryCentreX + 26, y, z - 8),
                    new int3(8, 10, 16),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(GalleryCentreX - 32, y + 8, z - 6),
                    new int3(4, 4, 12),
                    GameMaterialIds.Grass);
                authoring.Box(
                    new int3(GalleryCentreX + 28, y + 8, z - 6),
                    new int3(4, 4, 12),
                    GameMaterialIds.Grass);
            }
        }

        private CaveAuthoringResult AuthorGalleryCave(IStructureAuthoringSession authoring)
        {
            int3 entrance = Grounded(s_GalleryExhibitXZ[6]);
            CaveConfig config = CaveConfig.Default;
            config.MainSegmentCount = 14;
            config.MaxBranches = 4;
            config.MaxBranchDepth = 2;
            config.BranchSegmentCount = 5;
            config.ChamberChancePercent = 42;
            config.BoundsHalfExtents = new int3(240, 112, 240);

            // The mouth matches the tunnel behind it: 2.4 m across and 2.6 m high. It used to be
            // 1.1 m by 1.3 m, which is an opening the player cannot fit through, so the cave
            // exhibit could be looked at but never entered.
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x5742474341564501ul,
                Seed,
                entrance,
                Facing.North,
                24,
                26,
                8);

            CaveMaterialPalette palette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.Crystal,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };

            return VoxelEngine.Structures.Runtime.CaveAuthoring.Author(
                authoring,
                in request,
                in config,
                in palette);
        }

        private DecorationPlacement[] BuildGalleryDecorationPlacements()
        {
            int plazaY = TerrainQuery.HeightAt(GalleryCentreX, GalleryCentreZ, Seed) + 7;
            uint sceneId = 0x57424750u; // WBGP

            return new[]
            {
                GalleryProp(1, sceneId, DecorationPropFamily.Bench,
                    new int3(GalleryCentreX - 70, plazaY, GalleryCentreZ - 170),
                    new int3(22, 10, 8), new int3(0, 0, 1),
                    DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible),
                GalleryProp(2, sceneId, DecorationPropFamily.Bench,
                    new int3(GalleryCentreX + 70, plazaY, GalleryCentreZ - 170),
                    new int3(22, 10, 8), new int3(0, 0, -1),
                    DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible),
                GalleryProp(3, sceneId, DecorationPropFamily.Lantern,
                    new int3(GalleryCentreX - 42, plazaY + 12, GalleryCentreZ - 90),
                    new int3(6, 18, 6), new int3(0, 0, 1),
                    DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable |
                    DecorationInteractionFlags.EmitsLight),
                GalleryProp(4, sceneId, DecorationPropFamily.Lantern,
                    new int3(GalleryCentreX + 42, plazaY + 12, GalleryCentreZ - 90),
                    new int3(6, 18, 6), new int3(0, 0, 1),
                    DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable |
                    DecorationInteractionFlags.EmitsLight),
                GalleryProp(5, sceneId, DecorationPropFamily.Chest,
                    new int3(GalleryCentreX - 90, plazaY, GalleryCentreZ + 10),
                    new int3(16, 12, 10), new int3(1, 0, 0),
                    DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable |
                    DecorationInteractionFlags.Movable | DecorationInteractionFlags.Destructible),
                GalleryProp(6, sceneId, DecorationPropFamily.Barrel,
                    new int3(GalleryCentreX - 68, plazaY, GalleryCentreZ + 10),
                    new int3(9, 14, 9), new int3(1, 0, 0),
                    DecorationInteractionFlags.Container | DecorationInteractionFlags.Movable |
                    DecorationInteractionFlags.Destructible),
                GalleryProp(7, sceneId, DecorationPropFamily.Crate,
                    new int3(GalleryCentreX - 50, plazaY, GalleryCentreZ + 10),
                    new int3(12, 12, 12), new int3(1, 0, 0),
                    DecorationInteractionFlags.Container | DecorationInteractionFlags.Movable |
                    DecorationInteractionFlags.Destructible),
                GalleryProp(8, sceneId, DecorationPropFamily.WeaponRack,
                    new int3(GalleryCentreX + 82, plazaY, GalleryCentreZ + 10),
                    new int3(20, 22, 8), new int3(-1, 0, 0),
                    DecorationInteractionFlags.Lootable | DecorationInteractionFlags.Destructible),
                GalleryProp(9, sceneId, DecorationPropFamily.Banner,
                    new int3(GalleryCentreX - 18, plazaY + 18, GalleryCentreZ + 88),
                    new int3(10, 28, 2), new int3(0, 0, -1),
                    DecorationInteractionFlags.Destructible),
                GalleryProp(10, sceneId, DecorationPropFamily.Banner,
                    new int3(GalleryCentreX + 18, plazaY + 18, GalleryCentreZ + 88),
                    new int3(10, 28, 2), new int3(0, 0, -1),
                    DecorationInteractionFlags.Destructible),
                GalleryProp(11, sceneId, DecorationPropFamily.Table,
                    new int3(GalleryCentreX, plazaY, GalleryCentreZ + 155),
                    new int3(24, 12, 16), new int3(0, 0, -1),
                    DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Movable |
                    DecorationInteractionFlags.Destructible),
                GalleryProp(12, sceneId, DecorationPropFamily.Chair,
                    new int3(GalleryCentreX - 24, plazaY, GalleryCentreZ + 155),
                    new int3(8, 14, 8), new int3(1, 0, 0),
                    DecorationInteractionFlags.Movable | DecorationInteractionFlags.Destructible),
                GalleryProp(13, sceneId, DecorationPropFamily.Chair,
                    new int3(GalleryCentreX + 24, plazaY, GalleryCentreZ + 155),
                    new int3(8, 14, 8), new int3(-1, 0, 0),
                    DecorationInteractionFlags.Movable | DecorationInteractionFlags.Destructible),
                GalleryProp(14, sceneId, DecorationPropFamily.Campfire,
                    new int3(GalleryCentreX, plazaY, GalleryCentreZ + 235),
                    new int3(12, 8, 12), new int3(0, 0, 1),
                    DecorationInteractionFlags.EmitsLight | DecorationInteractionFlags.EmitsParticles |
                    DecorationInteractionFlags.Destructible),
                GalleryProp(15, sceneId, DecorationPropFamily.Altar,
                    new int3(GalleryCentreX, plazaY, GalleryCentreZ + 285),
                    new int3(20, 18, 12), new int3(0, 0, -1),
                    DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible),
            };
        }

        private static DecorationPlacement GalleryProp(
            uint slot,
            uint sceneId,
            DecorationPropFamily family,
            int3 baseCentre,
            int3 size,
            int3 facing,
            DecorationInteractionFlags interaction)
        {
            int3 min = new int3(
                baseCentre.x - size.x / 2,
                baseCentre.y,
                baseCentre.z - size.z / 2);

            return new DecorationPlacement
            {
                // Gallery props are authored directly rather than resolved from a DecorationContext,
                // so the identity is folded here from the same scene/slot pair a resolver would use,
                // tagged "WORLDBUI" to keep it clear of generated decoration ids.
                Id = new GeneratedPropId(0x574F524C44425549ul ^ (((ulong)sceneId << 32) | slot)),
                SceneId = sceneId,
                SlotId = slot,
                AnchorSlotId = 0,
                Family = family,
                Variant = slot - 1,
                Bounds = new DecorationBounds
                {
                    Min = min,
                    MaxExclusive = min + size,
                },
                Facing = facing,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = interaction,
            };
        }

        private static StructureMaterialPalette GalleryPalette() => new StructureMaterialPalette
        {
            Foundation = GameMaterialIds.DarkStone,
            PrimaryWall = GameMaterialIds.Stone,
            SecondaryWall = GameMaterialIds.MasonryMedium,
            Trim = GameMaterialIds.DarkStone,
            Roof = GameMaterialIds.Slate,
            Floor = GameMaterialIds.Wood,
            Column = GameMaterialIds.MasonryLarge,
            Accent = GameMaterialIds.Gold,
            Underground = GameMaterialIds.DarkStone,
            Opening = GameMaterialIds.Empty,
            Glass = GameMaterialIds.LitWindow,
            Detail = GameMaterialIds.Cloth,
        };
    }
}
