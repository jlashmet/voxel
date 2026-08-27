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
        public void ProductionPubCarriesCapturedBarInteriorRequirementsWithoutLeakingToInn()
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
                    "Captured pub should keep the modestly enlarged production footprint.");
                Assert.AreEqual(104, pubForm.DepthDm,
                    "Captured pub should keep the modestly enlarged production depth.");
                Assert.AreEqual(WindowTreatment.Warm, pubForm.WindowTreatment,
                    "Captured pub must keep visible warm-window treatment.");

                Assert.GreaterOrEqual(
                    CountFilledBoxes(in catalogue, in pub, 23, 2, 13, TimberMaterial), 3,
                    "Pub should contain several usable table surfaces.");
                Assert.GreaterOrEqual(
                    CountFilledBoxes(in catalogue, in pub, 6, 3, 6, TimberMaterial), 6,
                    "Pub should contain chairs around its tables.");
                Assert.AreEqual(
                    1, CountFilledBoxes(in catalogue, in pub, 44, 9, 8, TimberMaterial),
                    "Pub should contain one role-defining bar counter.");
                Assert.AreEqual(
                    2, CountFilledBoxes(in catalogue, in pub, 36, 2, 3, TimberMaterial),
                    "Pub should contain two shelves behind the bar.");
                Assert.AreEqual(
                    1, CountFilledBoxes(in catalogue, in pub, 10, 7, 5, ClothMaterial),
                    "Pub should contain the female bartender torso signature behind the bar.");
                Assert.AreEqual(
                    1, CountFilledBoxes(in catalogue, in pub, 6, 5, 5, WarmMaterial),
                    "Pub bartender should include a visible head signature.");

                int innRole = (int)KentridgeRole.Inn;
                FeatureDefinition inn = catalogue.Definitions[innRole];
                Assert.AreEqual(
                    0, CountFilledBoxes(in catalogue, in inn, 44, 9, 8, TimberMaterial),
                    "Pub-only bar geometry must not leak into the Inn role.");
                Assert.AreEqual(
                    0, CountFilledBoxes(in catalogue, in inn, 10, 7, 5, ClothMaterial),
                    "Pub-only bartender geometry must not leak into the Inn role.");
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
            int count = 0;
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
                    count++;
                }

                pc += length;
                if (op == ShapeOp.End) break;
            }

            return count;
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
