using System.Diagnostics;
using Game.Materials.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Authoritative voxel presentation layer for the typed structural gallery proofs. The typed
    /// structural graphs remain the source of composition/traversal truth; these bounded catalogue
    /// passes add construction hierarchy, material separation, grounding and environmental context
    /// without introducing presentation-only meshes or another structural solver.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private bool _structuralPresentationAuthored;
        private double _structuralPresentationAuthoringMs;

        public double WorldbuildingGalleryStructuralPresentationAuthoringMilliseconds =>
            _structuralPresentationAuthoringMs;

        public void EnsureWorldbuildingGalleryStructuralPresentationBlocking()
        {
            if (_structuralPresentationAuthored) return;

            EnsureWorldbuildingGalleryStructuralCompositionBlocking();
            var timer = Stopwatch.StartNew();

            BridgeSite bridge = FindBridgeSite();
            AuthorBridgePresentation(bridge);

            int3 castleOrigin = new(-2900,
                TerrainQuery.HeightAt(-2900, 120, Seed) + 2, 120);
            AuthorCastlePresentation(castleOrigin);

            CliffSite cliff = FindCliffSite();
            AuthorCliffPresentation(cliff);

            int facadeY = TerrainQuery.HeightAt(-2500, 1180, Seed) + 2;
            AuthorFacadePresentation(new int3(-2500, facadeY, 1180));

            timer.Stop();
            _structuralPresentationAuthoringMs = timer.Elapsed.TotalMilliseconds;
            _structuralAuthoringMs += _structuralPresentationAuthoringMs;
            _structuralPresentationAuthored = true;

            Debug.Log($"STRUCTURAL_PRESENTATION authored=True elapsedMs={_structuralPresentationAuthoringMs:0.###}");
        }

        private void AuthorBridgePresentation(BridgeSite site)
        {
            byte stone = GameMaterialIds.MasonryLarge;
            byte detail = GameMaterialIds.DarkStone;
            byte path = GameMaterialIds.MasonrySmall;

            int3 shellOrigin = new(site.X - 80, site.DeckY - 20, site.Z - 30);
            var shell = new ProgramWriter()
                .Box(new int3(80, 46, 36), new int3(1220, 2, 68), path)
                .Box(new int3(80, 30, 38), new int3(1220, 8, 8), detail)
                .Box(new int3(80, 30, 94), new int3(1220, 8, 8), detail)
                .Box(new int3(80, 20, 34), new int3(1220, 10, 12), stone)
                .Box(new int3(80, 20, 94), new int3(1220, 10, 12), stone)
                .Box(new int3(80, 66, 34), new int3(1220, 6, 8), detail)
                .Box(new int3(80, 66, 98), new int3(1220, 6, 8), detail)
                .Box(new int3(80, 72, 32), new int3(1220, 5, 12), stone)
                .Box(new int3(80, 72, 96), new int3(1220, 5, 12), stone)
                .Box(new int3(20, 0, 12), new int3(120, 36, 116), stone)
                .Box(new int3(40, 36, 22), new int3(100, 12, 96), detail)
                .Box(new int3(0, 6, 0), new int3(90, 26, 34), stone)
                .Box(new int3(0, 6, 106), new int3(90, 26, 34), stone)
                .Box(new int3(1240, 0, 12), new int3(120, 36, 116), stone)
                .Box(new int3(1240, 36, 22), new int3(100, 12, 96), detail)
                .Box(new int3(1290, 6, 0), new int3(90, 26, 34), stone)
                .Box(new int3(1290, 6, 106), new int3(90, 26, 34), stone)
                .Box(new int3(0, 42, 36), new int3(80, 6, 68), path)
                .Box(new int3(1300, 42, 36), new int3(80, 6, 68), path);

            for (int x = 92; x <= 1280; x += 64)
            {
                shell.Box(new int3(x, 46, 34), new int3(7, 24, 8), detail);
                shell.Box(new int3(x, 46, 98), new int3(7, 24, 8), detail);
            }
            for (int x = 120; x <= 1260; x += 160)
                shell.Box(new int3(x, 24, 36), new int3(8, 8, 68), detail);
            for (int x = 180; x <= 1180; x += 250)
                shell.Box(new int3(x, 10, 30), new int3(14, 20, 80), stone);

            AuthorPresentationCatalogue(
                "bridge-architectural-shell", shellOrigin, new int3(1380, 82, 140),
                0x53544601u, StructuralSocketRole.BridgeSpan | StructuralSocketRole.Traversal,
                BridgeTag, shell, 80, stone);

            int riverY = int.MaxValue;
            for (int i = 2; i <= 6; i++)
            {
                int x = site.X + 1220 * i / 8;
                riverY = math.min(riverY, TerrainQuery.HeightAt(x, site.Z + 40, Seed));
            }
            if (riverY == int.MaxValue) riverY = site.DeckY - 120;
            int contextY = math.min(riverY - 4, site.DeckY - 220);
            int deckLocalY = site.DeckY - contextY;
            int3 supportOrigin = new(site.X - 80, contextY, site.Z - 320);
            var supports = new ProgramWriter();

            int waterLocalY = math.max(2, riverY - contextY + 2);
            supports.Box(new int3(535, waterLocalY, 0), new int3(310, 4, 720), GameMaterialIds.Water);
            supports.Box(new int3(505, waterLocalY - 2, 0), new int3(30, 8, 720), GameMaterialIds.MasonrySmall);
            supports.Box(new int3(845, waterLocalY - 2, 0), new int3(30, 8, 720), GameMaterialIds.MasonrySmall);

            int shoulderLower = math.max(64, deckLocalY - 52);
            int shoulderUpper = math.max(48, deckLocalY - 18);
            supports.Box(new int3(0, 0, 0), new int3(142, shoulderLower, 286), stone)
                .Box(new int3(0, 0, 434), new int3(142, shoulderLower, 286), stone)
                .Box(new int3(36, 0, 72), new int3(94, shoulderUpper, 210), detail)
                .Box(new int3(36, 0, 438), new int3(94, shoulderUpper, 210), detail)
                .Box(new int3(1238, 0, 0), new int3(142, shoulderLower, 286), stone)
                .Box(new int3(1238, 0, 434), new int3(142, shoulderLower, 286), stone)
                .Box(new int3(1250, 0, 72), new int3(94, shoulderUpper, 210), detail)
                .Box(new int3(1250, 0, 438), new int3(94, shoulderUpper, 210), detail)
                .Box(new int3(38, shoulderUpper, 92), new int3(90, 10, 164), path)
                .Box(new int3(38, shoulderUpper, 464), new int3(90, 10, 164), path)
                .Box(new int3(1252, shoulderUpper, 92), new int3(90, 10, 164), path)
                .Box(new int3(1252, shoulderUpper, 464), new int3(90, 10, 164), path);

            int[] supportXs = { 170, 335, 530, 610, 690, 885, 1050 };
            for (int i = 0; i < supportXs.Length; i++)
            {
                int globalX = site.X + supportXs[i];
                int terrain = TerrainQuery.HeightAt(globalX, site.Z + 40, Seed);
                int bottom = math.max(0, terrain - contextY - 4);
                int top = deckLocalY + 20;
                int shaftHeight = math.max(12, top - bottom - 28);
                int localX = globalX - supportOrigin.x;
                supports.Box(new int3(localX - 30, bottom, 330), new int3(60, 14, 60), stone);
                supports.Box(new int3(localX - 17, bottom + 14, 342), new int3(34, shaftHeight, 36), stone);
                supports.Box(new int3(localX - 29, bottom + 14 + shaftHeight, 330), new int3(58, 14, 60), detail);
            }

            AuthorPresentationCatalogue(
                "bridge-grounded-supports-and-river", supportOrigin,
                new int3(1380, deckLocalY + 58, 720),
                0x53544602u, StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                BridgeTag, supports, 48, stone);
        }

        private void AuthorCastlePresentation(int3 origin)
        {
            byte stone = GameMaterialIds.MasonryMedium;
            byte detail = GameMaterialIds.DarkStone;
            int3 crownOrigin = origin + new int3(-320, 0, -20);
            var crown = new ProgramWriter()
                .Box(new int3(320, 72, 14), new int3(160, 8, 12), detail)
                .Box(new int3(312, 88, 12), new int3(176, 8, 16), detail)
                .Box(new int3(360, 54, 8), new int3(80, 10, 20), stone)
                .Box(new int3(370, 0, 12), new int3(12, 58, 14), detail)
                .Box(new int3(418, 0, 12), new int3(12, 58, 14), detail)
                .Box(new int3(370, 50, 12), new int3(60, 12, 14), detail)
                .Box(new int3(378, 28, 8), new int3(16, 20, 6), GameMaterialIds.LitWindow)
                .Box(new int3(406, 28, 8), new int3(16, 20, 6), GameMaterialIds.LitWindow)
                .Box(new int3(394, 62, 10), new int3(14, 28, 5), GameMaterialIds.Cloth)
                .Box(new int3(390, 88, 9), new int3(22, 5, 7), GameMaterialIds.Gold)
                .Box(new int3(12, 136, 32), new int3(96, 10, 96), detail)
                .Box(new int3(692, 136, 72), new int3(96, 10, 96), detail);

            for (int x = 324; x <= 464; x += 24)
                crown.Box(new int3(x, 96, 12), new int3(14, 18, 18), detail);
            for (int x = 104; x <= 302; x += 30)
                crown.Box(new int3(x, 64, 56), new int3(16, 18, 16), detail);
            for (int x = 486; x <= 684; x += 30)
                crown.Box(new int3(x, 64, 56), new int3(16, 18, 16), detail);

            for (int x = 20; x <= 92; x += 24)
            {
                crown.Box(new int3(x, 146, 28), new int3(16, 22, 16), detail);
                crown.Box(new int3(x, 146, 112), new int3(16, 22, 16), detail);
            }
            for (int x = 700; x <= 772; x += 24)
            {
                crown.Box(new int3(x, 146, 68), new int3(16, 22, 16), detail);
                crown.Box(new int3(x, 146, 152), new int3(16, 22, 16), detail);
            }

            AuthorPresentationCatalogue(
                "castle-crown-and-gatehouse-detail", crownOrigin, new int3(800, 176, 180),
                0x53544611u, StructuralSocketRole.Wall | StructuralSocketRole.Tower | StructuralSocketRole.Gate,
                WallTag, crown, 56, stone);

            var grounded = new ProgramWriter()
                .Box(new int3(306, 0, 8), new int3(18, 92, 22), detail)
                .Box(new int3(476, 0, 8), new int3(18, 92, 22), detail)
                .Box(new int3(10, 0, 26), new int3(18, 136, 18), detail)
                .Box(new int3(92, 0, 26), new int3(18, 136, 18), detail)
                .Box(new int3(690, 0, 66), new int3(18, 136, 18), detail)
                .Box(new int3(772, 0, 66), new int3(18, 136, 18), detail)
                .Box(new int3(0, 0, 18), new int3(120, 18, 124), stone)
                .Box(new int3(680, 0, 58), new int3(120, 18, 122), stone)
                .Box(new int3(112, 0, 42), new int3(182, 10, 36), stone)
                .Box(new int3(496, 0, 42), new int3(182, 10, 36), stone)
                .Box(new int3(38, 48, 24), new int3(22, 30, 5), GameMaterialIds.LitWindow)
                .Box(new int3(720, 48, 64), new int3(22, 30, 5), GameMaterialIds.LitWindow);

            for (int x = 122; x <= 286; x += 55)
            {
                grounded.Box(new int3(x, 0, 48), new int3(14, 52, 20), detail);
                grounded.Box(new int3(x - 5, 0, 44), new int3(24, 12, 28), stone);
            }
            for (int x = 500; x <= 664; x += 55)
            {
                grounded.Box(new int3(x, 0, 48), new int3(14, 52, 20), detail);
                grounded.Box(new int3(x - 5, 0, 44), new int3(24, 12, 28), stone);
            }

            AuthorPresentationCatalogue(
                "castle-grounded-buttresses", crownOrigin, new int3(800, 160, 180),
                0x53544612u, StructuralSocketRole.Wall | StructuralSocketRole.Tower,
                WallTag, grounded, 40, stone);
        }

        private void AuthorCliffPresentation(CliffSite site)
        {
            byte stone = GameMaterialIds.MasonrySmall;
            byte timber = GameMaterialIds.Wood;
            byte slate = GameMaterialIds.Slate;
            int3 contextOrigin = new(site.X - 20, site.LowY, site.Z - 20);
            var writer = new ProgramWriter();

            int lowerX = 20;
            int lowerZ = 20;
            writer.Box(new int3(lowerX, 0, lowerZ), new int3(180, 12, 120), stone)
                .Box(new int3(lowerX - 8, 0, lowerZ - 8), new int3(196, 8, 136), GameMaterialIds.DarkStone)
                .Box(new int3(lowerX, 12, lowerZ), new int3(180, 6, 8), GameMaterialIds.DarkStone)
                .Box(new int3(lowerX, 12, lowerZ + 112), new int3(180, 6, 8), GameMaterialIds.DarkStone)
                .Box(new int3(lowerX + 16, 18, lowerZ + 78), new int3(8, 44, 8), timber)
                .Box(new int3(lowerX + 132, 18, lowerZ + 78), new int3(8, 44, 8), timber)
                .Box(new int3(lowerX + 8, 58, lowerZ + 70), new int3(142, 8, 46), slate)
                .Box(new int3(lowerX + 20, 66, lowerZ + 76), new int3(118, 8, 34), slate);

            for (int x = 28; x <= 184; x += 32)
            {
                writer.Box(new int3(x, 18, 18), new int3(6, 24, 6), timber);
                writer.Box(new int3(x, 18, 134), new int3(6, 24, 6), timber);
            }
            writer.Box(new int3(24, 38, 18), new int3(168, 5, 6), timber)
                .Box(new int3(24, 38, 134), new int3(168, 5, 6), timber);

            int rampStartX = 200;
            for (int i = 0; i <= 5; i++)
            {
                int x = rampStartX + i * 52;
                int y = 20 + (site.Rise + 4) * i / 5;
                writer.Box(new int3(x, y, 36), new int3(6, 24, 6), timber);
                writer.Box(new int3(x, y, 112), new int3(6, 24, 6), timber);
                if (i < 5)
                {
                    writer.Box(new int3(x, y + 20, 36), new int3(58, 5, 6), timber);
                    writer.Box(new int3(x, y + 20, 112), new int3(58, 5, 6), timber);
                }
            }

            int upperBaseY = site.Rise + 8;
            int upperTopY = upperBaseY + 12;
            writer.Box(new int3(452, upperBaseY - 10, 28), new int3(180, 10, 120), GameMaterialIds.DarkStone)
                .Box(new int3(462, upperBaseY, 36), new int3(160, 8, 104), stone);
            for (int x = 472; x <= 612; x += 35)
            {
                writer.Box(new int3(x, upperTopY, 38), new int3(6, 24, 6), timber);
                writer.Box(new int3(x, upperTopY, 134), new int3(6, 24, 6), timber);
            }
            writer.Box(new int3(468, upperTopY + 20, 38), new int3(154, 5, 6), timber)
                .Box(new int3(468, upperTopY + 20, 134), new int3(154, 5, 6), timber);

            int cliffFaceHeight = math.max(40, site.Rise + 24);
            writer.Box(new int3(360, 0, 142), new int3(72, cliffFaceHeight, 24), stone)
                .Box(new int3(398, 20, 136), new int3(58, math.max(32, cliffFaceHeight - 20), 28), GameMaterialIds.DarkStone)
                .Box(new int3(434, 40, 132), new int3(50, math.max(24, cliffFaceHeight - 40), 32), stone)
                .Box(new int3(470, 60, 128), new int3(42, math.max(16, cliffFaceHeight - 60), 36), GameMaterialIds.DarkStone);

            int[] supportX = { site.X + 452, site.X + 602 };
            int[] supportZ = { site.Z + 32, site.Z + 128 };
            for (int xi = 0; xi < supportX.Length; xi++)
            for (int zi = 0; zi < supportZ.Length; zi++)
            {
                int terrain = TerrainQuery.HeightAt(supportX[xi], supportZ[zi], Seed);
                int bottom = math.max(0, terrain - contextOrigin.y);
                int height = math.max(8, upperBaseY - bottom);
                int localX = supportX[xi] - contextOrigin.x;
                int localZ = supportZ[zi] - contextOrigin.z;
                writer.Box(new int3(localX - 8, bottom, localZ - 8), new int3(16, height, 16), stone)
                    .Box(new int3(localX - 14, bottom, localZ - 14), new int3(28, 8, 28), GameMaterialIds.DarkStone);
            }

            int houseX = 500;
            int houseY = site.Rise + 20;
            int houseZ = 60;
            writer.Box(new int3(houseX - 6, houseY - 4, houseZ - 6), new int3(112, 8, 92), GameMaterialIds.DarkStone)
                .Box(new int3(houseX, houseY, houseZ - 4), new int3(10, 78, 10), GameMaterialIds.DarkStone)
                .Box(new int3(houseX + 90, houseY, houseZ - 4), new int3(10, 78, 10), GameMaterialIds.DarkStone)
                .Box(new int3(houseX + 18, houseY + 26, houseZ - 5), new int3(24, 28, 5), GameMaterialIds.LitWindow)
                .Box(new int3(houseX + 58, houseY + 26, houseZ - 5), new int3(24, 28, 5), GameMaterialIds.LitWindow)
                .Box(new int3(houseX + 36, houseY, houseZ - 8), new int3(28, 46, 6), timber)
                .Box(new int3(houseX + 30, houseY + 42, houseZ - 12), new int3(40, 6, 16), slate)
                .Box(new int3(houseX - 10, houseY + 80, houseZ - 10), new int3(120, 8, 100), slate)
                .Box(new int3(houseX + 2, houseY + 88, houseZ), new int3(96, 10, 80), slate)
                .Box(new int3(houseX + 70, houseY + 98, houseZ + 48), new int3(16, 28, 16), GameMaterialIds.DarkStone)
                .Box(new int3(houseX - 16, houseY + 10, houseZ - 18), new int3(132, 6, 22), timber);

            AuthorPresentationCatalogue(
                "cliff-settlement-architectural-detail", contextOrigin,
                new int3(660, site.Rise + 170, 170),
                0x53544621u,
                StructuralSocketRole.Platform | StructuralSocketRole.VerticalConnection | StructuralSocketRole.Building,
                CliffTag, writer, 96, stone);
        }

        private void AuthorFacadePresentation(int3 firstOrigin)
        {
            int3 origin = firstOrigin + new int3(-20, 0, -10);
            var writer = new ProgramWriter();
            AddFacadeVariantPresentation(writer, 20, false);
            AddFacadeVariantPresentation(writer, 320, true);

            AuthorPresentationCatalogue(
                "facade-roof-production-detail", origin, new int3(540, 240, 180),
                0x53544631u,
                StructuralSocketRole.Building | StructuralSocketRole.Facade | StructuralSocketRole.Roof,
                FacadeTag | RoofTag, writer, 96, GameMaterialIds.MasonryMedium);
        }

        private static void AddFacadeVariantPresentation(ProgramWriter writer, int x, bool ornate)
        {
            byte trim = ornate ? GameMaterialIds.Gold : GameMaterialIds.Wood;
            byte roof = ornate ? GameMaterialIds.Slate : GameMaterialIds.Tile;
            byte plinth = ornate ? GameMaterialIds.DarkStone : GameMaterialIds.MasonryLarge;
            int frontZ = 130;

            writer.Box(new int3(x - 6, 0, 2), new int3(192, 12, 126), plinth)
                .Box(new int3(x + 8, 18, frontZ), new int3(12, 102, 8), trim)
                .Box(new int3(x + 160, 18, frontZ), new int3(12, 102, 8), trim)
                .Box(new int3(x + 8, 112, frontZ), new int3(164, 10, 10), trim)
                .Box(new int3(x + 2, 128, frontZ - 2), new int3(176, 8, 14), plinth)
                .Box(new int3(x + 70, 0, frontZ + 1), new int3(40, 48, 7), plinth);

            int[] windowX = { x + 28, x + 78, x + 128 };
            int[] windowY = { 30, 72 };
            for (int yi = 0; yi < windowY.Length; yi++)
            for (int xi = 0; xi < windowX.Length; xi++)
                writer.Box(new int3(windowX[xi], windowY[yi], frontZ + 2),
                    new int3(24, 26, 6), GameMaterialIds.LitWindow);

            writer.Box(new int3(x + 46, 54, frontZ + 10), new int3(88, 7, 34), trim)
                .Box(new int3(x + 46, 61, frontZ + 38), new int3(7, 24, 7), trim)
                .Box(new int3(x + 127, 61, frontZ + 38), new int3(7, 24, 7), trim)
                .Box(new int3(x + 48, 82, frontZ + 38), new int3(84, 6, 7), trim);
            for (int railX = x + 58; railX <= x + 118; railX += 20)
                writer.Box(new int3(railX, 61, frontZ + 39), new int3(5, 22, 5), trim);

            writer.Box(new int3(x - 8, 154, 0), new int3(196, 8, 140), roof)
                .Box(new int3(x + 4, 162, 8), new int3(172, 12, 124), roof)
                .Box(new int3(x + 18, 174, 18), new int3(144, 12, 104), roof)
                .Box(new int3(x + 34, 186, 30), new int3(112, ornate ? 22 : 12, 80), roof)
                .Box(new int3(x + 54, ornate ? 208 : 198, 44), new int3(72, 7, 52), trim)
                .Box(new int3(x + 66, 176, 128), new int3(48, 30, 8), plinth)
                .Box(new int3(x + 76, 184, 130), new int3(28, 18, 6), GameMaterialIds.LitWindow)
                .Box(new int3(x + 18, 148, frontZ - 4), new int3(144, 6, 18), plinth);

            if (ornate)
            {
                writer.Box(new int3(x + 20, 122, frontZ + 1), new int3(140, 7, 8), GameMaterialIds.Gold)
                    .Box(new int3(x + 84, 208, 48), new int3(12, 26, 18), GameMaterialIds.Gold)
                    .Box(new int3(x + 28, 196, 38), new int3(18, 28, 18), GameMaterialIds.DarkStone)
                    .Box(new int3(x + 134, 196, 38), new int3(18, 28, 18), GameMaterialIds.DarkStone)
                    .Box(new int3(x + 18, 18, frontZ - 2), new int3(8, 104, 12), GameMaterialIds.DarkStone)
                    .Box(new int3(x + 154, 18, frontZ - 2), new int3(8, 104, 12), GameMaterialIds.DarkStone);
            }
            else
            {
                writer.Box(new int3(x + 62, 122, frontZ + 2), new int3(56, 12, 8), GameMaterialIds.Wood)
                    .Box(new int3(x + 58, 134, frontZ - 12), new int3(64, 6, 28), GameMaterialIds.Tile)
                    .Box(new int3(x + 52, 128, frontZ - 10), new int3(6, 28, 6), GameMaterialIds.Wood)
                    .Box(new int3(x + 122, 128, frontZ - 10), new int3(6, 28, 6), GameMaterialIds.Wood)
                    .Box(new int3(x + 136, 198, 54), new int3(18, 34, 18), GameMaterialIds.MasonryLarge);
            }
        }

        private void AuthorPresentationCatalogue(
            string name,
            int3 origin,
            int3 footprint,
            uint pieceId,
            StructuralSocketRole role,
            ulong tag,
            ProgramWriter writer,
            int maxPrimitives,
            byte material)
        {
            int[] program = writer.Finish();
            using FeatureCatalogue catalogue = BuildCatalogue(
                new[]
                {
                    Def(name, footprint,
                        Piece(pieceId, role, tag, int3.zero, Facing.South),
                        program, maxPrimitives, material),
                },
                origin);

            StructuralCompositionReport plan = Plan(in catalogue, 0);
            RequireOk(name, in plan);
            FeatureCatalogueBuildResult build = BuildIfNeeded(in catalogue, in plan, author: true);
            Debug.Log($"STRUCTURAL_PRESENTATION_COST name={name} primitives={plan.PrimitiveCost} " +
                $"voxelBudget={plan.VoxelCost} regions={build.RegionsVisited} " +
                $"instances={build.InstancesRasterised} voxelsWritten={build.VoxelsWritten} " +
                $"bounds={plan.BoundsMin}..{plan.BoundsMax}");
        }
    }
}
