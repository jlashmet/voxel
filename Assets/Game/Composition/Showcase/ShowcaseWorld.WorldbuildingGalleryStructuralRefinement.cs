using System.Diagnostics;
using Game.Materials.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final authoritative-voxel art pass for the typed structural proof district. This is deliberately
    /// another set of bounded feature catalogues, not a mesh-only decoration layer: the same voxel
    /// storage, collision, destruction and rendering path owns every refinement below.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private bool _structuralRefinementAuthored;

        public void EnsureWorldbuildingGalleryStructuralRefinementBlocking()
        {
            if (_structuralRefinementAuthored) return;

            EnsureWorldbuildingGalleryStructuralPresentationBlocking();
            var timer = Stopwatch.StartNew();

            BridgeSite bridge = FindBridgeSite();
            AuthorBridgeRefinement(bridge);

            int3 castleOrigin = new(-2900,
                TerrainQuery.HeightAt(-2900, 120, Seed) + 2, 120);
            AuthorCastleRefinement(castleOrigin);

            CliffSite cliff = FindCliffSite();
            AuthorCliffRefinement(cliff);

            int facadeY = TerrainQuery.HeightAt(-2500, 1180, Seed) + 2;
            AuthorFacadeRefinement(new int3(-2520, facadeY, 1170), false, 0x53544C31u);
            AuthorFacadeRefinement(new int3(-2220, facadeY, 1170), true, 0x53544C32u);

            timer.Stop();
            _structuralPresentationAuthoringMs += timer.Elapsed.TotalMilliseconds;
            _structuralAuthoringMs += timer.Elapsed.TotalMilliseconds;
            _structuralRefinementAuthored = true;
            UnityEngine.Debug.Log(
                $"STRUCTURAL_REFINEMENT authored=True elapsedMs={timer.Elapsed.TotalMilliseconds:0.###}");
        }

        /// <summary>
        /// Preloads a narrow line-of-sight strip for deterministic evidence framing. This is intentionally
        /// bounded: five samples with one neighbour on each side, rather than a second streaming radius.
        /// </summary>
        public void PrepareWorldbuildingGalleryStructuralEvidence(Vector3 camera, Vector3 target)
        {
            const int samples = 4;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(camera, target, i / (float)samples);
                int3 region = RegionAt(p);
                GenerateRegionBlocking(region);
                GenerateRegionBlocking(region + new int3(0, 0, -1));
                GenerateRegionBlocking(region + new int3(0, 0, 1));
            }
        }

        private void AuthorBridgeRefinement(BridgeSite site)
        {
            byte stone = GameMaterialIds.MasonryLarge;
            byte detail = GameMaterialIds.DarkStone;
            byte path = GameMaterialIds.MasonrySmall;

            int riverY = int.MaxValue;
            for (int i = 2; i <= 6; i++)
            {
                int x = site.X + 1220 * i / 8;
                riverY = math.min(riverY, TerrainQuery.HeightAt(x, site.Z + 40, Seed));
            }
            if (riverY == int.MaxValue) riverY = site.DeckY - 120;
            int contextY = math.min(riverY - 4, site.DeckY - 220);
            int deckLocalY = site.DeckY - contextY;
            int bankHeight = math.clamp(deckLocalY - 8, 96, 210);

            AuthorBridgeBankRefinement(
                "bridge-refined-bank-l-n", new int3(site.X - 20, contextY, site.Z - 320),
                bankHeight, false, stone, detail, path, 0x53544C01u);
            AuthorBridgeBankRefinement(
                "bridge-refined-bank-l-s", new int3(site.X - 20, contextY, site.Z + 80),
                bankHeight, false, stone, detail, path, 0x53544C02u);
            AuthorBridgeBankRefinement(
                "bridge-refined-bank-r-n", new int3(site.X + 1090, contextY, site.Z - 320),
                bankHeight, true, stone, detail, path, 0x53544C03u);
            AuthorBridgeBankRefinement(
                "bridge-refined-bank-r-s", new int3(site.X + 1090, contextY, site.Z + 80),
                bankHeight, true, stone, detail, path, 0x53544C04u);

            var truss = new ProgramWriter()
                .Box(new int3(0, 20, 30), new int3(1220, 8, 12), detail)
                .Box(new int3(0, 20, 98), new int3(1220, 8, 12), detail)
                .Box(new int3(0, 52, 32), new int3(1220, 6, 10), stone)
                .Box(new int3(0, 52, 98), new int3(1220, 6, 10), stone);
            for (int x = 70; x <= 1060; x += 180)
            {
                truss.Ramp(new int3(x, 28, 30), new int3(110, 24, 12), 0, stone);
                truss.Ramp(new int3(x, 28, 98), new int3(110, 24, 12), 0, stone);
                truss.Box(new int3(x + 48, 28, 30), new int3(10, 28, 12), detail);
                truss.Box(new int3(x + 48, 28, 98), new int3(10, 28, 12), detail);
            }
            AuthorPresentationCatalogue(
                "bridge-refined-understructure", new int3(site.X, site.DeckY - 40, site.Z - 30),
                new int3(1220, 90, 140), 0x53544C05u,
                StructuralSocketRole.BridgeSpan | StructuralSocketRole.Support,
                BridgeTag, truss, 40, stone);

            AuthorBridgeEntryRefinement(
                "bridge-refined-entry-left", new int3(site.X - 10, site.DeckY - 34, site.Z - 40),
                false, stone, detail, 0x53544C06u);
            AuthorBridgeEntryRefinement(
                "bridge-refined-entry-right", new int3(site.X + 1080, site.DeckY - 34, site.Z - 40),
                true, stone, detail, 0x53544C07u);
        }

        private void AuthorBridgeBankRefinement(
            string name, int3 origin, int height, bool right,
            byte stone, byte detail, byte path, uint pieceId)
        {
            int baseX = right ? 30 : 0;
            int midX = right ? 14 : 36;
            int upperX = right ? 0 : 70;
            int upperY = math.max(40, height - 44);
            var writer = new ProgramWriter()
                .Box(new int3(baseX, 0, 0), new int3(120, math.max(48, height - 18), 320), stone)
                .Box(new int3(midX, 18, 30), new int3(100, math.max(36, height - 54), 260), detail)
                .Box(new int3(upperX, upperY, 54), new int3(80, 44, 212), stone)
                .Box(new int3(upperX, height - 8, 78), new int3(80, 8, 164), path);
            for (int z = 44; z <= 252; z += 52)
                writer.Box(new int3(right ? 8 : 124, 24, z), new int3(18, math.max(28, height - 44), 24), detail);

            AuthorPresentationCatalogue(
                name, origin, new int3(150, height, 320), pieceId,
                StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                BridgeTag, writer, 16, stone);
        }

        private void AuthorBridgeEntryRefinement(
            string name, int3 origin, bool right, byte stone, byte detail, uint pieceId)
        {
            var writer = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(150, 12, 160), stone)
                .Box(new int3(10, 12, 8), new int3(28, 70, 30), detail)
                .Box(new int3(10, 12, 122), new int3(28, 70, 30), detail)
                .Box(new int3(6, 82, 4), new int3(36, 10, 38), stone)
                .Box(new int3(6, 82, 118), new int3(36, 10, 38), stone)
                .Box(new int3(18, 92, 14), new int3(12, 20, 18), GameMaterialIds.Gold)
                .Box(new int3(18, 92, 128), new int3(12, 20, 18), GameMaterialIds.Gold);
            if (right)
            {
                writer.Box(new int3(112, 12, 8), new int3(28, 42, 30), stone)
                    .Box(new int3(112, 12, 122), new int3(28, 42, 30), stone);
            }
            else
            {
                writer.Box(new int3(112, 12, 8), new int3(28, 42, 30), stone)
                    .Box(new int3(112, 12, 122), new int3(28, 42, 30), stone);
            }

            AuthorPresentationCatalogue(
                name, origin, new int3(150, 120, 160), pieceId,
                StructuralSocketRole.BridgeSpan | StructuralSocketRole.Support | StructuralSocketRole.Traversal,
                BridgeTag, writer, 16, stone);
        }

        private void AuthorCastleRefinement(int3 origin)
        {
            byte stone = GameMaterialIds.MasonryMedium;
            byte detail = GameMaterialIds.DarkStone;
            int3 crownOrigin = origin + new int3(-320, 0, -20);

            var gate = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(200, 12, 70), stone)
                .Box(new int3(0, 12, 0), new int3(28, 94, 26), detail)
                .Box(new int3(172, 12, 0), new int3(28, 94, 26), detail)
                .Box(new int3(18, 84, 0), new int3(164, 12, 28), stone)
                .Box(new int3(10, 100, 0), new int3(180, 10, 32), detail)
                .Box(new int3(36, 54, 0), new int3(18, 28, 8), GameMaterialIds.LitWindow)
                .Box(new int3(146, 54, 0), new int3(18, 28, 8), GameMaterialIds.LitWindow);
            for (int x = 10; x <= 170; x += 32)
                gate.Box(new int3(x, 110, 0), new int3(18, 20, 28), detail);
            AuthorPresentationCatalogue(
                "castle-refined-gatehouse", crownOrigin + new int3(300, 0, 0),
                new int3(200, 132, 80), 0x53544C11u,
                StructuralSocketRole.Gate | StructuralSocketRole.Wall | StructuralSocketRole.Support,
                WallTag, gate, 24, stone);

            AuthorCastleTowerRefinement(
                "castle-refined-left-tower", crownOrigin, false, stone, detail, 0x53544C12u);
            AuthorCastleTowerRefinement(
                "castle-refined-right-tower", crownOrigin + new int3(680, 0, 58), true,
                stone, detail, 0x53544C13u);

            AuthorCastleWallRefinement(
                "castle-refined-left-wall", crownOrigin + new int3(112, 0, 42),
                stone, detail, 0x53544C14u);
            AuthorCastleWallRefinement(
                "castle-refined-right-wall", crownOrigin + new int3(496, 0, 42),
                stone, detail, 0x53544C15u);
        }

        private void AuthorCastleTowerRefinement(
            string name, int3 origin, bool right, byte stone, byte detail, uint pieceId)
        {
            int frontZ = right ? 0 : 18;
            var writer = new ProgramWriter()
                .Box(new int3(0, 0, frontZ), new int3(120, 10, 124), stone)
                .Box(new int3(0, 52, frontZ), new int3(120, 8, 124), detail)
                .Box(new int3(0, 104, frontZ), new int3(120, 8, 124), detail)
                .Box(new int3(0, 132, frontZ), new int3(120, 10, 124), stone)
                .Box(new int3(14, 26, frontZ), new int3(14, 34, 8), detail)
                .Box(new int3(92, 26, frontZ), new int3(14, 34, 8), detail)
                .Box(new int3(50, 72, frontZ), new int3(20, 26, 8), GameMaterialIds.LitWindow);
            for (int x = 4; x <= 100; x += 32)
                writer.Box(new int3(x, 142, frontZ), new int3(16, 24, 20), detail);

            AuthorPresentationCatalogue(
                name, origin, new int3(120, 176, 150), pieceId,
                StructuralSocketRole.Tower | StructuralSocketRole.Support,
                WallTag, writer, 20, stone);
        }

        private void AuthorCastleWallRefinement(
            string name, int3 origin, byte stone, byte detail, uint pieceId)
        {
            var writer = new ProgramWriter()
                .Box(new int3(0, 0, 0), new int3(190, 8, 44), stone)
                .Box(new int3(0, 48, 0), new int3(190, 8, 44), detail)
                .Box(new int3(0, 58, 2), new int3(190, 8, 40), stone);
            for (int x = 18; x <= 156; x += 46)
            {
                writer.Box(new int3(x, 18, 0), new int3(14, 26, 8), detail);
                writer.Box(new int3(x - 4, 12, 0), new int3(22, 6, 10), stone);
            }
            AuthorPresentationCatalogue(
                name, origin, new int3(190, 72, 50), pieceId,
                StructuralSocketRole.Wall | StructuralSocketRole.Support,
                WallTag, writer, 16, stone);
        }

        private void AuthorCliffRefinement(CliffSite site)
        {
            byte stone = GameMaterialIds.MasonryLarge;
            byte detail = GameMaterialIds.DarkStone;
            byte timber = GameMaterialIds.Wood;
            int3 context = new(site.X - 20, site.LowY, site.Z - 20);
            int upperY = site.Rise + 8;

            var pedestal = new ProgramWriter()
                .Box(new int3(72, 0, 20), new int3(218, math.max(20, upperY - 6), 130), stone)
                .Box(new int3(96, 8, 14), new int3(194, math.max(16, upperY - 18), 142), detail)
                .Box(new int3(116, math.max(8, upperY - 24), 8), new int3(174, 24, 154), stone)
                .Box(new int3(104, upperY - 6, 28), new int3(186, 6, 120), GameMaterialIds.MasonrySmall);
            for (int z = 30; z <= 126; z += 32)
                pedestal.Box(new int3(54, 8, z), new int3(24, math.max(16, upperY - 16), 18), detail);
            AuthorPresentationCatalogue(
                "cliff-refined-rock-pedestal", context + new int3(350, 0, 0),
                new int3(310, math.max(80, upperY + 20), 170), 0x53544C21u,
                StructuralSocketRole.Platform | StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                CliffTag, pedestal, 20, stone);

            var ramp = new ProgramWriter();
            for (int i = 0; i <= 5; i++)
            {
                int x = 8 + i * 52;
                int y = 10 + (site.Rise + 4) * i / 5;
                ramp.Box(new int3(x, 0, 26), new int3(14, math.max(12, y), 14), stone);
                ramp.Box(new int3(x, 0, 116), new int3(14, math.max(12, y), 14), stone);
                ramp.Box(new int3(x - 4, 0, 22), new int3(22, 8, 22), detail);
                ramp.Box(new int3(x - 4, 0, 112), new int3(22, 8, 22), detail);
            }
            ramp.Box(new int3(0, 4, 68), new int3(300, 8, 20), timber);
            AuthorPresentationCatalogue(
                "cliff-refined-ramp-supports", context + new int3(190, 0, 0),
                new int3(300, site.Rise + 72, 150), 0x53544C22u,
                StructuralSocketRole.VerticalConnection | StructuralSocketRole.Support,
                CliffTag, ramp, 32, stone);
        }

        private void AuthorFacadeRefinement(int3 origin, bool ornate, uint pieceId)
        {
            byte trim = ornate ? GameMaterialIds.Gold : GameMaterialIds.MasonryLarge;
            byte contrast = ornate ? GameMaterialIds.DarkStone : GameMaterialIds.Wood;
            byte roof = ornate ? GameMaterialIds.Slate : GameMaterialIds.Tile;
            const int x = 20;
            const int frontZ = 132;
            var writer = new ProgramWriter();

            for (int y = 14; y <= 110; y += 24)
            {
                writer.Box(new int3(x + 2, y, frontZ), new int3(12, 14, 8), trim);
                writer.Box(new int3(x + 166, y, frontZ), new int3(12, 14, 8), trim);
            }

            int[] wx = { x + 28, x + 78, x + 128 };
            int[] wy = { 30, 72 };
            for (int yi = 0; yi < wy.Length; yi++)
            for (int xi = 0; xi < wx.Length; xi++)
            {
                writer.Box(new int3(wx[xi] - 3, wy[yi] - 5, frontZ), new int3(30, 5, 8), contrast);
                writer.Box(new int3(wx[xi] - 3, wy[yi] + 26, frontZ), new int3(30, 5, 8), contrast);
            }

            writer.Box(new int3(x + 56, 0, frontZ), new int3(10, 48, 8), trim)
                .Box(new int3(x + 114, 0, frontZ), new int3(10, 48, 8), trim)
                .Box(new int3(x + 56, 44, frontZ), new int3(68, 8, 10), trim)
                .Box(new int3(x - 8, 144, 124), new int3(196, 8, 20), contrast)
                .Box(new int3(x + 38, 198, 38), new int3(104, 8, 70), trim)
                .Box(new int3(x + 142, 198, 46), new int3(20, 38, 20), contrast)
                .Box(new int3(x + 138, 232, 42), new int3(28, 8, 28), roof);

            if (ornate)
            {
                writer.Box(new int3(x + 76, 128, frontZ), new int3(28, 18, 8), GameMaterialIds.Gold)
                    .Box(new int3(x + 84, 208, 36), new int3(12, 28, 12), GameMaterialIds.Gold);
            }
            else
            {
                writer.Box(new int3(x + 68, 128, frontZ), new int3(44, 10, 8), GameMaterialIds.Wood)
                    .Box(new int3(x + 12, 136, 4), new int3(156, 8, 132), GameMaterialIds.Tile);
            }

            AuthorPresentationCatalogue(
                ornate ? "facade-refined-ornate" : "facade-refined-civic",
                origin, new int3(220, 240, 180), pieceId,
                StructuralSocketRole.Building | StructuralSocketRole.Facade | StructuralSocketRole.Roof,
                FacadeTag | RoofTag, writer, 64, trim);
        }
    }
}
