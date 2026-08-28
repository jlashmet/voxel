using System;
using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeHouseInteriorPropPlayModeTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int TimberMaterial = 2;
        private const int ClothMaterial = 9;
        private const int WarmMaterial = 15;

        [Test]
        public void ProductionSharedStructuresDecorateEveryGeneratedInteriorWithinBudget()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                int generatedCount = 0;
                int bespokeCount = 0;

                for (int roleId = 0; roleId < catalogue.Definitions.Length; roleId++)
                {
                    BuildingPlot plot = FindRole(plan, roleId);
                    StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                    StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);
                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    bool hasFurnitureSignature = HasCommonTableSignature(in catalogue, in definition);

                    if (form.IsGenerated)
                    {
                        generatedCount++;
                        Assert.IsTrue(
                            hasFurnitureSignature,
                            ((KentridgeRole)roleId) +
                            " must compose the interior-prop catalogue into its production program.");
                    }
                    else
                    {
                        bespokeCount++;
                        Assert.IsFalse(
                            hasFurnitureSignature,
                            ((KentridgeRole)roleId) +
                            " is bespoke and must not be changed by the generated-house furniture pass.");
                    }

                    primitives.Clear();
                    anchors.Clear();
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[roleId];
                    ParameterSet parameters = default;
                    EvaluationResult evaluation = ShapeProgram.Evaluate(
                        in catalogue,
                        roleId,
                        in parameters,
                        placement.Position,
                        placement.Orientation,
                        Seed,
                        FeatureGeneration.InstanceSeed(Seed, roleId, placement.Position),
                        primitives,
                        anchors);

                    Assert.AreEqual(
                        EvaluationResult.Ok,
                        evaluation,
                        ((KentridgeRole)roleId) + " production program must evaluate successfully.");
                    Assert.LessOrEqual(
                        primitives.Length,
                        definition.MaxPrimitives,
                        ((KentridgeRole)roleId) +
                        " must remain within the existing per-definition primitive budget.");
                }

                Assert.AreEqual(13, generatedCount,
                    "All houses, shops, inn, and pub should use the generated decorated path.");
                Assert.AreEqual(4, bespokeCount,
                    "Church, warehouse, mansion, and well should remain on their bespoke path.");
            }
            finally
            {
                if (anchors.IsCreated) anchors.Dispose();
                if (primitives.IsCreated) primitives.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void ProductionPubFurnitureIsSupportedDecoratedAndClearOfBar()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                int pubRole = (int)KentridgeRole.Pub;
                BuildingPlot pubPlot = FindRole(plan, pubRole);
                StructureIntent pubIntent = KentridgeDefinition.StructureIntent(pubPlot);
                StructureForm pubForm = ArchitectureCompiler.Resolve(pubIntent, plan.Theme, Seed);
                FeatureDefinition pub = catalogue.Definitions[pubRole];

                Assert.AreEqual(126, pubForm.WidthDm,
                    "Captured pub should keep the production footprint used by the saved pose.");
                Assert.AreEqual(104, pubForm.DepthDm,
                    "Captured pub should keep the production depth used by the saved pose.");
                Assert.AreEqual(WindowTreatment.Warm, pubForm.WindowTreatment,
                    "Captured pub must keep visible warm-window treatment.");

                List<FilledBox> tables = FindFilledBoxes(
                    in catalogue, in pub, 23, 2, 13, TimberMaterial);
                List<FilledBox> chairSeats = FindFilledBoxes(
                    in catalogue, in pub, 6, 2, 6, TimberMaterial);
                List<FilledBox> chairFrontLegs = FindFilledBoxes(
                    in catalogue, in pub, 2, 5, 2, TimberMaterial);
                List<FilledBox> chairBackPosts = FindFilledBoxes(
                    in catalogue, in pub, 2, 13, 2, TimberMaterial);
                List<FilledBox> bars = FindFilledBoxes(
                    in catalogue, in pub, 44, 9, 8, TimberMaterial);
                List<FilledBox> stoolSeats = FindFilledBoxes(
                    in catalogue, in pub, 7, 2, 7, TimberMaterial);
                List<FilledBox> stoolPosts = FindFilledBoxes(
                    in catalogue, in pub, 2, 6, 2, TimberMaterial);
                List<FilledBox> stoolBases = FindFilledBoxes(
                    in catalogue, in pub, 7, 1, 7, TimberMaterial);
                List<FilledBox> paintingFrames = FindFilledBoxes(
                    in catalogue, in pub, 1, 16, 20, -1);
                List<FilledBox> paintingCanvases = FindFilledBoxes(
                    in catalogue, in pub, 1, 12, 16, -1);

                Assert.GreaterOrEqual(tables.Count, 3,
                    "Pub should contain several usable table surfaces.");
                Assert.AreEqual(6, chairSeats.Count,
                    "Each production pub table should retain a pair of readable seats.");
                Assert.GreaterOrEqual(chairFrontLegs.Count, 12,
                    "Pub chairs must have floor-reaching front legs rather than floating seat slabs.");
                Assert.GreaterOrEqual(chairBackPosts.Count, 12,
                    "Pub chairs must have floor-reaching back posts rather than solid placeholder backs.");
                Assert.AreEqual(1, bars.Count,
                    "Pub should contain one role-defining bar counter.");
                Assert.AreEqual(3, stoolSeats.Count,
                    "Pub bar should provide three customer stools.");
                Assert.AreEqual(3, stoolPosts.Count,
                    "Each bar stool should have a supporting post.");
                Assert.AreEqual(3, stoolBases.Count,
                    "Each bar stool should have a stable floor base.");
                Assert.AreEqual(2, paintingFrames.Count,
                    "Pub should decorate both side walls with framed art.");
                Assert.AreEqual(2, paintingCanvases.Count,
                    "Each pub wall frame should contain a visible inset canvas.");
                Assert.Greater(
                    Math.Abs(paintingFrames[0].X - paintingFrames[1].X),
                    50,
                    "Pub paintings should occupy opposing wall zones rather than stack together.");

                Assert.AreEqual(
                    2, CountFilledBoxes(in catalogue, in pub, 36, 2, 3, TimberMaterial),
                    "Pub should retain two shelves behind the bar.");
                Assert.AreEqual(
                    1, CountFilledBoxes(in catalogue, in pub, 10, 7, 5, ClothMaterial),
                    "Pub should retain the female bartender torso signature behind the bar.");
                Assert.AreEqual(
                    1, CountFilledBoxes(in catalogue, in pub, 6, 5, 5, WarmMaterial),
                    "Pub bartender should retain a visible head signature.");

                FilledBox bar = bars[0];
                for (int i = 0; i < tables.Count; i++)
                {
                    Assert.IsTrue(
                        AreSeparatedXZ(tables[i], bar, 2),
                        "Dining table " + i + " must keep at least two decimetres of clearance from the bar.");
                }

                for (int i = 0; i < stoolSeats.Count; i++)
                {
                    FilledBox stool = stoolSeats[i];
                    Assert.GreaterOrEqual(stool.X, bar.X,
                        "Bar stool " + i + " should align within the counter width.");
                    Assert.LessOrEqual(stool.X + stool.SizeX, bar.X + bar.SizeX,
                        "Bar stool " + i + " should align within the counter width.");
                    Assert.LessOrEqual(stool.Z + stool.SizeZ + 2, bar.Z,
                        "Bar stool " + i + " must remain on the customer side with a service gap.");
                }

                int innRole = (int)KentridgeRole.Inn;
                FeatureDefinition inn = catalogue.Definitions[innRole];
                Assert.AreEqual(
                    0, CountFilledBoxes(in catalogue, in inn, 44, 9, 8, TimberMaterial),
                    "Pub-only bar geometry must not leak into the Inn role.");
                Assert.AreEqual(
                    0, CountFilledBoxes(in catalogue, in inn, 7, 2, 7, TimberMaterial),
                    "Pub-only bar stools must not leak into the Inn role.");
                Assert.AreEqual(
                    0, CountFilledBoxes(in catalogue, in inn, 1, 16, 20, -1),
                    "Pub-only paintings must not leak into the Inn role.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static bool HasCommonTableSignature(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition)
        {
            return CountFilledBoxes(in catalogue, in definition, 23, 2, 13, -1) > 0;
        }

        private static int CountFilledBoxes(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition,
            int sizeX,
            int sizeY,
            int sizeZ,
            int material)
        {
            return FindFilledBoxes(
                in catalogue, in definition, sizeX, sizeY, sizeZ, material).Count;
        }

        private static List<FilledBox> FindFilledBoxes(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition,
            int sizeX,
            int sizeY,
            int sizeZ,
            int material)
        {
            var boxes = new List<FilledBox>();
            int pc = definition.ProgramOffset;
            int end = definition.ProgramOffset + definition.ProgramLength;

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                Assert.Greater(length, 0,
                    "Production structure program contains an invalid instruction.");

                if (op == ShapeOp.EmitBox
                    && catalogue.Program[pc + 5] == sizeX
                    && catalogue.Program[pc + 6] == sizeY
                    && catalogue.Program[pc + 7] == sizeZ
                    && (material < 0 || catalogue.Program[pc + 8] == material)
                    && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Fill)
                {
                    boxes.Add(new FilledBox(
                        catalogue.Program[pc + 2],
                        catalogue.Program[pc + 4],
                        catalogue.Program[pc + 5],
                        catalogue.Program[pc + 7]));
                }

                pc += length;
                if (op == ShapeOp.End) break;
            }

            return boxes;
        }

        private static bool AreSeparatedXZ(FilledBox a, FilledBox b, int minimumGap)
        {
            return a.X + a.SizeX + minimumGap <= b.X
                || b.X + b.SizeX + minimumGap <= a.X
                || a.Z + a.SizeZ + minimumGap <= b.Z
                || b.Z + b.SizeZ + minimumGap <= a.Z;
        }

        private readonly struct FilledBox
        {
            public FilledBox(int x, int z, int sizeX, int sizeZ)
            {
                X = x;
                Z = z;
                SizeX = sizeX;
                SizeZ = sizeZ;
            }

            public int X { get; }
            public int Z { get; }
            public int SizeX { get; }
            public int SizeZ { get; }
        }

        private static BuildingPlot FindRole(SettlementPlan plan, int roleId)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == roleId)
                    return plan.Plots[i];

            Assert.Fail("Kentridge settlement is missing stable role id " + roleId + ".");
            return default;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: TimberMaterial, glass: 4, warmWindow: WarmMaterial,
                roofTile: 8, slate: 7, cloth: ClothMaterial,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
