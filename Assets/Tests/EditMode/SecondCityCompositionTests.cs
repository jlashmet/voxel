using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Integration-shaped proof that the reusable world-building stack does not require Kentridge
    /// content. Brickport is test-only: its plan, style id, geometry policy and construction program
    /// are all composed from Core/Architecture/Voxel APIs shared by any future city package.
    /// </summary>
    public sealed class SecondCityCompositionTests
    {
        private const string StyleId = "test.brickport";
        private const uint Seed = 0x42524943u;

        [Test]
        public void AnotherCityCanPlanResolveAndAuthorArchitectureWithoutKentridge()
        {
            ArchitectureTheme theme = BuildTheme();
            BuildingPlot plot = SettlementPlotLayout.AlongHorizontalStreet(
                seed: Seed,
                salt: 1,
                roleId: 100,
                archetype: StructureArchetype.Townhouse,
                district: DistrictKind.Residential,
                streetId: "harbor-road",
                frontageXDm: 300,
                streetZDm: 100,
                frontage: FrontageDirection.South,
                roadWidthDm: 40,
                setbackDm: 8,
                jitterDm: 0,
                footprintDm: new Int3(110, 100, 100));

            var intent = new StructureIntent(
                plot,
                StyleId,
                new Int3(110, 100, 100));
            var style = new BrickportStyleCompiler();
            var styles = new ArchitectureStyleRegistry(style);
            StructureForm form = ArchitectureCompiler.Resolve(
                intent, theme, Seed, styles);
            StructureGeometryProfile geometry =
                styles.Require(StyleId).ResolveGeometry(intent, form);

            var builder = new ArchitectureShapeProgramBuilder(geometry, 1);
            ArchitectureVoxelPatterns.HollowShell(
                builder,
                10, 5, 10,
                form.WidthDm, 35, form.DepthDm,
                thickness: theme.WallThicknessDm,
                material: 1);
            ArchitectureVoxelPatterns.GlazedOpening(
                builder,
                30, 14, 10,
                12, 14, theme.WallThicknessDm + 1,
                glazingMaterial: 4);
            ArchitectureVoxelPatterns.GableRoof(
                builder,
                7, 40, 7,
                form.WidthDm + 6,
                form.RoofHeightDm,
                form.DepthDm + 6,
                material: 8);
            int[] code = builder.Finish();

            Assert.AreEqual(StyleId, style.StyleId);
            Assert.AreEqual(72, form.WidthDm);
            Assert.AreEqual(60, form.DepthDm);
            Assert.AreEqual(5, geometry.ShellCornerRadiusDm);
            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[0]);
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[10]);

            bool foundPlanarGlass = false;
            bool foundSmoothRoof = false;
            for (int pc = 0; pc < code.Length;)
            {
                ShapeOp op = (ShapeOp)code[pc];
                int length = ShapeOps.InstructionLength(op);
                Assert.GreaterOrEqual(length, 2);
                if (op == ShapeOp.EmitBox
                    && (byte)code[pc + 8] == 4
                    && (ushort)code[pc + 9] == SurfaceStyles.Planar)
                    foundPlanarGlass = true;
                else if (op == ShapeOp.EmitPrism
                    && (ushort)code[pc + 10] == SurfaceStyles.Smooth)
                    foundSmoothRoof = true;

                pc += length;
                if (op == ShapeOp.End) break;
            }

            Assert.IsTrue(foundPlanarGlass);
            Assert.IsTrue(foundSmoothRoof);
        }

        [Test]
        public void AnotherCityCanReuseAnonymousFrontagePacking()
        {
            SettlementFrontageSite[] sites = SettlementPlotLayout.PackFrontage(
                startDm: 0,
                endDm: 240,
                coveragePercent: 75,
                modulePitchDm: 80,
                hasGap: true,
                gapCentreDm: 120,
                gapWidthDm: 40);

            Assert.Greater(sites.Length, 0);
            for (int i = 0; i < sites.Length; i++)
            {
                Assert.AreEqual(i, sites[i].SiteIndex);
                Assert.That(sites[i].CentreAlongDm, Is.InRange(0, 240));
                Assert.That(sites[i].CentreAlongDm, Is.Not.InRange(100, 140));
            }
        }

        private static ArchitectureTheme BuildTheme()
        {
            return new ArchitectureTheme(
                id: StyleId,
                foundation: MaterialRole.FoundationStone,
                wall: MaterialRole.Masonry,
                frame: MaterialRole.Timber,
                window: MaterialRole.Glass,
                roof: MaterialRole.RoofTile,
                accentStone: MaterialRole.DarkMasonry,
                foundationHeightDm: 5,
                wallThicknessDm: 4,
                floorHeightDm: 30,
                doorHeightDm: 22,
                windowBaseDm: 9,
                windowHeightDm: 14,
                beamWidthDm: 3,
                roofOverhangDm: 3,
                typicalRoofHeightDm: 20,
                grandRoofHeightDm: 28,
                upperStoreyOverhangDm: 1);
        }

        private sealed class BrickportStyleCompiler : IArchitectureStyleCompiler
        {
            public string StyleId => SecondCityCompositionTests.StyleId;

            public StructureForm ResolveStructure(
                StructureIntent intent,
                ArchitectureTheme theme,
                uint seed)
            {
                return new StructureForm(
                    intent.RoleId,
                    intent.Archetype,
                    intent.District,
                    StructureGenerationMode.Generated,
                    FootprintForm.Rectangle,
                    RoofForm.Gable,
                    FrontageRhythm.TwoBay,
                    WindowTreatment.Glass,
                    widthDm: 72,
                    depthDm: 60,
                    storeys: 2,
                    doorOffsetDm: 0,
                    upperOverhangDm: 1,
                    roofHeightDm: 20,
                    wingWidthDm: 0,
                    wingDepthDm: 0,
                    wingOnRight: false,
                    chimneyOnRight: false);
            }

            public void ValidateStructure(
                StructureIntent intent,
                ArchitectureTheme theme,
                StructureForm form)
            {
            }

            public StructureGeometryProfile ResolveGeometry(
                StructureIntent intent,
                StructureForm form)
            {
                return new StructureGeometryProfile(
                    foundationCornerRadiusDm: 2,
                    shellCornerRadiusDm: 5,
                    openingCornerRadiusDm: 2,
                    detailCornerRadiusDm: 1,
                    foundationSurface: StructureSurfaceTreatment.Beveled,
                    shellSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                    openingSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                    detailSurface: StructureSurfaceTreatment.Planar,
                    roofSurface: StructureSurfaceTreatment.Smooth);
            }

            public UrbanFabricForm ResolveUrbanFabric(
                UrbanFabricIntent intent,
                uint seed,
                int runIndex,
                int siteIndex)
            {
                return new UrbanFabricForm(
                    widthDm: 58,
                    depthDm: 52,
                    storeys: intent.MinStoreys,
                    upperOverhangDm: 0,
                    roofHeightDm: 18,
                    roof: RoofForm.Gable,
                    frontageRhythm: FrontageRhythm.TwoBay,
                    windowTreatment: WindowTreatment.Glass,
                    hasAwning: false,
                    chimneyOnRight: false,
                    annexOnRight: false);
            }

            public void ValidateUrbanFabric(UrbanFabricIntent intent, UrbanFabricForm form)
            {
            }
        }
    }
}
