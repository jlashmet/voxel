using System.IO;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Tests
{
    /// <summary>
    /// Human-inspectable visual regression outputs. These tests intentionally write PNG artifacts
    /// rather than comparing pixels: their first job is to make generated geometry visible during
    /// development and review. Structural/determinism invariants remain covered by ordinary tests.
    /// Output: TestResults/WorldbuildingVisuals/*.png
    /// </summary>
    public sealed class WorldbuildingVisualRegressionTests
    {
        [Test]
        public void StorageShed_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ShedConfig config = ShedPresets.Storage(in palette);
            var capture = new VisualStructureCapture(new int3(-48, -8, -48), new int3(96, 80, 96));
            ShedAuthoring.Author(capture, int3.zero, in config);
            AssertVisual(capture.RenderPng("shed-storage"));
        }

        [Test]
        public void ParishChurch_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ChurchConfig config = ChurchPresets.ParishChurch(in palette);
            var capture = new VisualStructureCapture(new int3(-80, -8, -120), new int3(160, 160, 240));
            ChurchAuthoring.Author(capture, int3.zero, in config);
            AssertVisual(capture.RenderPng("church-parish"));
        }

        [Test]
        public void GothicCathedral_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralWorldbuildingConfig config = CathedralWorldbuildingPresets.Gothic(in palette);
            var capture = new VisualStructureCapture(new int3(-130, -32, -205), new int3(260, 300, 410));
            CathedralWorldbuildingAuthoring.Author(capture, int3.zero, in config);
            AssertVisual(capture.RenderPng("cathedral-gothic", 1440, 1000));
        }

        [Test]
        public void ClassicalTemple_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig config = TemplePresets.ClassicalColumned(in palette);
            var capture = new VisualStructureCapture(new int3(-72, -16, -92), new int3(144, 112, 184));
            TempleAuthoring.Author(capture, int3.zero, in config);
            AssertVisual(capture.RenderPng("temple-classical"));
        }

        [Test]
        public void CourtyardTemple_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig config = TemplePresets.CourtyardTemple(in palette);
            var capture = new VisualStructureCapture(new int3(-84, -16, -104), new int3(168, 112, 208));
            TempleAuthoring.Author(capture, int3.zero, in config);
            AssertVisual(capture.RenderPng("temple-courtyard"));
        }

        [Test]
        public void WalledCastle_WritesRenderedGeometryPng()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            var plan = new CastlePlan
            {
                Centre = int3.zero,
                PlateauRadius = 190,
                PlateauHeight = 16,
                CliffDrop = 44,
                BaileyHalfX = 150,
                BaileyHalfZ = 132,
                WallHeight = 54,
                WallThickness = 8,
                TowerRadius = 20,
                TowerHeight = 78,
                GateTowerRadius = 24,
                GateTowerHeight = 92,
                KeepHalfX = 48,
                KeepHalfZ = 42,
                KeepHeight = 102,
                FloorHeight = 30,
                Floors = 3,
                Seed = 0xCA571Eu,
            };
            CastlePresetConfig preset = CastlePresets.WalledCastle(in plan, in palette);
            preset.Stages.Site = false;
            preset.Stages.Landscape = false;

            var capture = new VisualStructureCapture(new int3(-220, -20, -220), new int3(440, 260, 440));
            var build = new CastleAuthoringBuild(capture, in plan, preset, plan.Seed);
            while (!build.Step()) { }
            AssertVisual(capture.RenderPng("castle-walled", 1440, 1000));
        }

        [Test]
        public void BranchedCave_WritesCarvedVoidPng()
        {
            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 9;
            config.TunnelHeight = 11;
            config.SegmentLength = 12;
            config.MainSegmentCount = 12;
            config.MaxBranches = 4;
            config.MaxBranchDepth = 2;
            config.BranchSegmentCount = 4;
            config.MinBranchSeparation = 12;
            config.MinChamberRadius = 6;
            config.MaxChamberRadius = 12;
            config.MinChamberHeight = 8;
            config.MaxChamberHeight = 16;
            config.BoundsHalfExtents = new int3(110, 65, 110);
            config.MinVerticalOffset = -54;
            config.MaxVerticalOffset = 18;

            CaveGenerationRequest request = CaveGenerationRequest.Underground(
                0xC0FFEEUL, int3.zero, Facing.North, 9, 11, 10);
            var palette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.Stone,
                Accent = GameMaterialIds.Crystal,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };
            var capture = new VisualStructureCapture(
                new int3(-112, -68, -112), new int3(224, 136, 224), recordEmptyWrites: true);

            CaveAuthoringResult result = CaveAuthoring.Author(capture, in request, in config, in palette);
            Assert.That(result.SegmentsAuthored, Is.GreaterThan(0));
            AssertVisual(capture.RenderCarvedVoidPng("cave-branched-cutaway", 1440, 1000));
        }

        private static void AssertVisual(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Expected visual artifact at {path}");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1024),
                $"Visual artifact was unexpectedly small: {path}");
            TestContext.WriteLine($"Generated worldbuilding geometry: {path}");
        }
    }
}
