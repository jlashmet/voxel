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

            UnityEngine.Debug.Log($"STRUCTURAL_PRESENTATION authored=True elapsedMs={_structuralPresentationAuthoringMs:0.###}");
        }

        private void AuthorBridgePresentation(BridgeSite site)
        {
            byte stone = GameMaterialIds.MasonryLarge;
            byte detail = GameMaterialIds.DarkStone;
            byte path = GameMaterialIds.MasonrySmall;

            int3 shellOrigin = new(site.X, site.DeckY - 20, site.Z - 30);
            var shell = new ProgramWriter()
                .Box(new int3(0, 46, 36), new int3(1220, 2, 68), path)
                .Box(new int3(0, 30, 38), new int3(1220, 8, 8), detail)
                .Box(new int3(0, 30, 94), new int3(1220, 8, 8), detail)
                .Box(new int3(0, 20, 34), new int3(1220, 10, 12), stone)
                .Box(new int3(0, 20, 94), new int3(1220, 10, 12), stone)
                .Box(new int3(0, 66, 34), new int3(1220, 6, 8), detail)
                .Box(new int3(0, 66, 98), new int3(1220, 6, 8), detail)
                .Box(new int3(0, 72, 32), new int3(1220, 5, 12), stone)
                .Box(new int3(0, 72, 96), new int3(1220, 5, 12), stone)
                .Box(new int3(0, 0, 12), new int3(100, 36, 116), stone)
                .Box(new int3(8, 36, 22), new int3(92, 12, 96), detail)
                .Box(new int3(0, 6, 0), new int3(72, 26, 34), stone)
                .Box(new int3(0, 6, 106), new int3(72, 26, 34), stone)
                .Box(new int3(1120, 0, 12), new int3(100, 36, 116), stone)
                .Box(new int3(1120, 36, 22), new int3(92, 12, 96), detail)
                .Box(new int3(1148, 6, 0), new int3(72, 26, 34), stone)
                .Box(new int3(1148, 6, 106), new int3(72, 26, 34), stone);

            for (int x = 12; x <= 1200; x += 64)
            {
                shell.Box(new int3(x, 46, 34), new int3(7, 24, 8), detail);
                shell.Box(new int3(x, 46, 98), new int3(7, 24, 8), detail);
            }
            for (int x = 40; x <= 1160; x += 160)
                shell.Box(new int3(x, 24, 36), new int3(8, 8, 68), detail);
            for (int x = 100; x <= 1100; x += 250)
                shell.Box(new int3(x, 10, 30), new int3(14, 20, 80), stone);

            AuthorPresentationCatalogue(
                "bridge-architectural-shell", shellOrigin, new int3(1220, 82, 140),
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

            var river = new ProgramWriter()
                .Box(new int3(50, 8, 0), new int3(310, 4, 720), GameMaterialIds.Water)
                .Box(new int3(20, 6, 0), new int3(30, 8, 720), path)
                .Box(new int3(360, 6, 0), new int3(30, 8, 720), path);
            AuthorPresentationCatalogue(
                "bridge-river-channel", new int3(site.X + 405, riverY - 6, site.Z - 320),
                new int3(410, 16, 720), 0x53544602u,
                StructuralSocketRole.TerrainAnchor, BridgeTag, river, 8, path);

            int shoulderLower = math.clamp(deckLocalY - 52, 64, 180);
            int shoulderUpper = math.clamp(deckLocalY - 18, 48, 200);
            AuthorBridgeShoulderPresentation(
                "bridge-bank-l-n", new int3(site.X, contextY, site.Z - 320),
                shoulderLower, shoulderUpper, false, stone, detail, path, 0x53544701u);
            AuthorBridgeShoulderPresentation(
                "bridge-bank-l-s", new int3(site.X, contextY, site.Z + 114),
                shoulderLower, shoulderUpper, false, stone, detail, path, 0x53544702u);
            AuthorBridgeShoulderPresentation(
                "bridge-bank-r-n", new int3(site.X + 1102, contextY, site.Z - 320),
                shoulderLower, shoulderUpper, true, stone, detail, path, 0x53544703u);
            AuthorBridgeShoulderPresentation(
                "bridge-bank-r-s", new int3(site.X + 1102, contextY, site.Z + 114),
                shoulderLower, shoulderUpper, true, stone, detail, path, 0x53544704u);

            int[] supportXs = { 170, 335, 530, 610, 690, 885, 1050 };
            for (int i = 0; i < supportXs.Length; i++)
            {
                int globalX = site.X + supportXs[i];
                int terrain = TerrainQuery.HeightAt(globalX, site.Z + 40, Seed);
                int bottom = math.max(0, terrain - contextY - 4);
                int top = deckLocalY + 20;
                int shaftHeight = math.max(12, top - bottom - 28);
                int pierTop = bottom + 28 + shaftHeight;
                var pier = new ProgramWriter()
                    .Box(new int3(6, bottom, 6), new int3(60, 14, 60), stone)
                    .Box(new int3(19, bottom + 14, 18), new int3(34, shaftHeight, 36), stone)
                    .Box(new int3(7, bottom + 14 + shaftHeight, 6), new int3(58, 14, 60), detail);
                AuthorPresentationCatalogue(
                    $"bridge-pier-{i}", new int3(globalX - 36, contextY, site.Z + 4),
                    new int3(72, pierTop, 72), 0x53544800u + (uint)i,
                    StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                    BridgeTag, pier, 8, stone);
            }
        }

        private void AuthorBridgeShoulderPresentation(
            string name,
            int3 origin,
            int shoulderLower,
            int shoulderUpper,
            bool right,
            byte stone,
            byte detail,
            byte path,
            uint pieceId)
        {
            int stoneX = right ? 90 : 0;
            int detailX = right ? 72 : 28;
            int pathX = right ? 0 : 28;
            var shoulder = new ProgramWriter()
                .Box(new int3(stoneX, 0, 0), new int3(28, shoulderLower, 286), stone)
                .Box(new int3(detailX, 0, 72), new int3(18, shoulderUpper, 210), detail)
                .Box(new int3(pathX, shoulderUpper, 92), new int3(90, 10, 164), path);

            AuthorPresentationCatalogue(
                name, origin,
                new int3(118, math.max(shoulderLower, shoulderUpper + 10), 286),
                pieceId, StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                BridgeTag, shoulder, 8, stone);
        }

        private void AuthorCastlePresentation(int3 origin)
        {
            byte stone = GameMaterialIds.MasonryMedium;
            byte detail = GameMaterialIds.DarkStone;
            int3 crownOrigin = origin + new int3(-320, 0, -20);

            var front = new ProgramWriter()
                .Box(new int3(320, 72, 14), new int3(160, 8, 12), detail)
                .Box(new int3(312, 88, 12), new int3(176, 8, 16), detail)
                .Box(new int3(360, 54, 8), new int3(80, 10, 20), stone)
                .Box(new int3(370, 0, 12), new int3(12, 58, 14), detail)
                .Box(new int3(418, 0, 12), new int3(12, 58, 14), detail)
                .Box(new int3(370, 50, 12), new int3(60, 12, 14), detail)
                .Box(new int3(378, 28, 8), new int3(16, 20, 6), GameMaterialIds.LitWindow)
                .Box(new int3(406, 28, 8), new int3(16, 20, 6), GameMaterialIds.LitWindow)
                .Box(new int3(394, 62, 10), new int3(14, 28, 5), GameMaterialIds.Cloth)
                .Box(new int3(390, 88, 9), new int3(22, 5, 7), GameMaterialIds.Gold);
            for (int x = 324; x <= 464; x += 24)
                front.Box(new int3(x, 96, 12), new int3(14, 18, 18), detail);
            for (int x = 104; x <= 302; x += 30)
                front.Box(new int3(x, 64, 56), new int3(16, 18, 16), detail);
            for (int x = 486; x <= 684; x += 30)
                front.Box(new int3(x, 64, 56), new int3(16, 18, 16), detail);
            AuthorPresentationCatalogue(
                "castle-front-crown", crownOrigin, new int3(800, 120, 80),
                0x53544901u, StructuralSocketRole.Wall | StructuralSocketRole.Gate,
                WallTag, front, 48, stone);

            var leftTop = new ProgramWriter()
                .Box(new int3(12, 136, 32), new int3(96, 10, 96), detail);
            for (int x = 20; x <= 92; x += 24)
            {
                leftTop.Box(new int3(x, 146, 28), new int3(16, 22, 16), detail);
                leftTop.Box(new int3(x, 146, 112), new int3(16, 22, 16), detail);
            }
            AuthorPresentationCatalogue(
                "castle-left-crown", crownOrigin, new int3(120, 176, 140),
                0x53544902u, StructuralSocketRole.Tower, WallTag, leftTop, 16, detail);

            var rightTop = new ProgramWriter()
                .Box(new int3(12, 136, 14), new int3(96, 10, 96), detail);
            for (int x = 20; x <= 92; x += 24)
            {
                rightTop.Box(new int3(x, 146, 10), new int3(16, 22, 16), detail);
                rightTop.Box(new int3(x, 146, 94), new int3(16, 22, 16), detail);
            }
            AuthorPresentationCatalogue(
                "castle-right-crown", crownOrigin + new int3(680, 0, 58),
                new int3(120, 176, 122), 0x53544903u,
                StructuralSocketRole.Tower, WallTag, rightTop, 16, detail);

            var leftTower = new ProgramWriter()
                .Box(new int3(10, 0, 26), new int3(18, 136, 18), detail)
                .Box(new int3(92, 0, 26), new int3(18, 136, 18), detail)
                .Box(new int3(0, 0, 18), new int3(120, 18, 124), stone)
                .Box(new int3(38, 48, 24), new int3(22, 30, 5), GameMaterialIds.LitWindow);
            AuthorPresentationCatalogue(
                "castle-left-base", crownOrigin, new int3(120, 150, 150),
                0x53544904u, StructuralSocketRole.Tower | StructuralSocketRole.Support,
                WallTag, leftTower, 12, stone);

            var rightTower = new ProgramWriter()
                .Box(new int3(10, 0, 8), new int3(18, 136, 18), detail)
                .Box(new int3(92, 0, 8), new int3(18, 136, 18), detail)
                .Box(new int3(0, 0, 0), new int3(120, 18, 122), stone)
                .Box(new int3(40, 48, 6), new int3(22, 30, 5), GameMaterialIds.LitWindow);
            AuthorPresentationCatalogue(
                "castle-right-base", crownOrigin + new int3(680, 0, 58),
                new int3(120, 150, 122), 0x53544905u,
                StructuralSocketRole.Tower | StructuralSocketRole.Support,
                WallTag, rightTower, 12, stone);

            var gateGround = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(18, 92, 22), detail)
                .Box(new int3(170, 0, 0), new int3(18, 92, 22), detail);
            AuthorPresentationCatalogue(
                "castle-gate-ground", crownOrigin + new int3(306, 0, 8),
                new int3(188, 100, 40), 0x53544906u,
                StructuralSocketRole.Gate | StructuralSocketRole.Support,
                WallTag, gateGround, 8, stone);

            var leftWall = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(182, 10, 36), stone);
            for (int x = 5; x <= 170; x += 55)
            {
                leftWall.Box(new int3(x, 0, 6), new int3(14, 52, 20), detail);
                leftWall.Box(new int3(x - 5, 0, 2), new int3(24, 12, 28), stone);
            }
            AuthorPresentationCatalogue(
                "castle-left-wall-base", crownOrigin + new int3(112, 0, 42),
                new int3(190, 60, 50), 0x53544907u,
                StructuralSocketRole.Wall | StructuralSocketRole.Support,
                WallTag, leftWall, 16, stone);

            var rightWall = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(182, 10, 36), stone);
            for (int x = 5; x <= 170; x += 55)
            {
                rightWall.Box(new int3(x, 0, 6), new int3(14, 52, 20), detail);
                rightWall.Box(new int3(x - 5, 0, 2), new int3(24, 12, 28), stone);
            }
            AuthorPresentationCatalogue(
                "castle-right-wall-base", crownOrigin + new int3(496, 0, 42),
                new int3(190, 60, 50), 0x53544908u,
                StructuralSocketRole.Wall | StructuralSocketRole.Support,
                WallTag, rightWall, 16, stone);
        }

        private void AuthorCliffPresentation(CliffSite site)
        {
            byte stone = GameMaterialIds.MasonrySmall;
            byte timber = GameMaterialIds.Wood;
            byte slate = GameMaterialIds.Slate;
            int3 contextOrigin = new(site.X - 20, site.LowY, site.Z - 20);

            var lower = new ProgramWriter()
                .Box(new int3(20, 0, 20), new int3(180, 12, 120), stone)
                .Box(new int3(12, 0, 12), new int3(196, 8, 136), GameMaterialIds.DarkStone)
                .Box(new int3(20, 12, 20), new int3(180, 6, 8), GameMaterialIds.DarkStone)
                .Box(new int3(20, 12, 132), new int3(180, 6, 8), GameMaterialIds.DarkStone)
                .Box(new int3(36, 18, 98), new int3(8, 44, 8), timber)
                .Box(new int3(152, 18, 98), new int3(8, 44, 8), timber)
                .Box(new int3(28, 58, 90), new int3(142, 8, 46), slate)
                .Box(new int3(40, 66, 96), new int3(118, 8, 34), slate);
            for (int x = 28; x <= 184; x += 32)
            {
                lower.Box(new int3(x, 18, 18), new int3(6, 24, 6), timber);
                lower.Box(new int3(x, 18, 134), new int3(6, 24, 6), timber);
            }
            lower.Box(new int3(24, 38, 18), new int3(168, 5, 6), timber)
                .Box(new int3(24, 38, 134), new int3(168, 5, 6), timber);
            AuthorPresentationCatalogue(
                "cliff-lower-terrace", contextOrigin, new int3(210, 80, 150),
                0x53544A01u, StructuralSocketRole.Platform | StructuralSocketRole.Support,
                CliffTag, lower, 32, stone);

            var ramp = new ProgramWriter();
            for (int i = 0; i <= 5; i++)
            {
                int x = 10 + i * 52;
                int y = 20 + (site.Rise + 4) * i / 5;
                ramp.Box(new int3(x, y, 36), new int3(6, 24, 6), timber);
                ramp.Box(new int3(x, y, 112), new int3(6, 24, 6), timber);
                if (i < 5)
                {
                    ramp.Box(new int3(x, y + 20, 36), new int3(58, 5, 6), timber);
                    ramp.Box(new int3(x, y + 20, 112), new int3(58, 5, 6), timber);
                }
            }
            AuthorPresentationCatalogue(
                "cliff-ramp-detail", contextOrigin + new int3(190, 0, 0),
                new int3(300, site.Rise + 80, 150), 0x53544A02u,
                StructuralSocketRole.VerticalConnection | StructuralSocketRole.Support,
                CliffTag, ramp, 32, timber);

            int upperBaseY = site.Rise + 8;
            int upperTopY = upperBaseY + 12;
            var upper = new ProgramWriter()
                .Box(new int3(102, upperBaseY - 10, 28), new int3(180, 10, 120), GameMaterialIds.DarkStone)
                .Box(new int3(112, upperBaseY, 36), new int3(160, 8, 104), stone);
            for (int x = 122; x <= 262; x += 35)
            {
                upper.Box(new int3(x, upperTopY, 38), new int3(6, 24, 6), timber);
                upper.Box(new int3(x, upperTopY, 134), new int3(6, 24, 6), timber);
            }
            upper.Box(new int3(118, upperTopY + 20, 38), new int3(154, 5, 6), timber)
                .Box(new int3(118, upperTopY + 20, 134), new int3(154, 5, 6), timber);

            int cliffFaceHeight = math.max(40, site.Rise + 24);
            upper.Box(new int3(10, 0, 142), new int3(72, cliffFaceHeight, 24), stone)
                .Box(new int3(48, 20, 136), new int3(58, math.max(32, cliffFaceHeight - 20), 28), GameMaterialIds.DarkStone)
                .Box(new int3(84, 40, 132), new int3(50, math.max(24, cliffFaceHeight - 40), 32), stone)
                .Box(new int3(120, 60, 128), new int3(42, math.max(16, cliffFaceHeight - 60), 36), GameMaterialIds.DarkStone);

            int[] supportX = { site.X + 452, site.X + 602 };
            int[] supportZ = { site.Z + 32, site.Z + 128 };
            for (int xi = 0; xi < supportX.Length; xi++)
            for (int zi = 0; zi < supportZ.Length; zi++)
            {
                int terrain = TerrainQuery.HeightAt(supportX[xi], supportZ[zi], Seed);
                int bottom = math.max(0, terrain - contextOrigin.y);
                int height = math.max(8, upperBaseY - bottom);
                int localX = supportX[xi] - contextOrigin.x - 350;
                int localZ = supportZ[zi] - contextOrigin.z;
                upper.Box(new int3(localX - 8, bottom, localZ - 8), new int3(16, height, 16), stone)
                    .Box(new int3(localX - 14, bottom, localZ - 14), new int3(28, 8, 28), GameMaterialIds.DarkStone);
            }

            int houseX = 150;
            int houseY = site.Rise + 20;
            int houseZ = 60;
            upper.Box(new int3(houseX - 6, houseY - 4, houseZ - 6), new int3(112, 8, 92), GameMaterialIds.DarkStone)
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
                "cliff-upper-settlement", contextOrigin + new int3(350, 0, 0),
                new int3(310, site.Rise + 170, 170), 0x53544A03u,
                StructuralSocketRole.Platform | StructuralSocketRole.Building | StructuralSocketRole.Support,
                CliffTag, upper, 64, stone);
        }

        private void AuthorFacadePresentation(int3 firstOrigin)
        {
            int3 civicOrigin = firstOrigin + new int3(-20, 0, -10);
            var civic = new ProgramWriter();
            AddFacadeVariantPresentation(civic, 20, false);
            AuthorPresentationCatalogue(
                "facade-civic-detail", civicOrigin, new int3(220, 240, 180),
                0x53544B01u,
                StructuralSocketRole.Building | StructuralSocketRole.Facade | StructuralSocketRole.Roof,
                FacadeTag | RoofTag, civic, 48, GameMaterialIds.MasonryMedium);

            int3 ornateOrigin = civicOrigin + new int3(300, 0, 0);
            var ornate = new ProgramWriter();
            AddFacadeVariantPresentation(ornate, 20, true);
            AuthorPresentationCatalogue(
                "facade-ornate-detail", ornateOrigin, new int3(220, 240, 180),
                0x53544B02u,
                StructuralSocketRole.Building | StructuralSocketRole.Facade | StructuralSocketRole.Roof,
                FacadeTag | RoofTag, ornate, 48, GameMaterialIds.MasonryMedium);
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
            UnityEngine.Debug.Log($"STRUCTURAL_PRESENTATION_COST name={name} primitives={plan.PrimitiveCost} " +
                $"voxelBudget={plan.VoxelCost} regions={build.RegionsVisited} " +
                $"instances={build.InstancesRasterised} voxelsWritten={build.VoxelsWritten} " +
                $"bounds={plan.BoundsMin}..{plan.BoundsMax}");
        }
    }
}
