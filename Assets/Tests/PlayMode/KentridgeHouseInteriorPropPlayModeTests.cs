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

        private static bool HasCommonTableSignature(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition)
        {
            int pc = definition.ProgramOffset;
            int end = definition.ProgramOffset + definition.ProgramLength;

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                Assert.Greater(length, 0,
                    "Production structure program contains an invalid instruction.");

                if (op == ShapeOp.EmitBox
                    && catalogue.Program[pc + 5] == 23
                    && catalogue.Program[pc + 6] == 2
                    && catalogue.Program[pc + 7] == 13
                    && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Fill)
                {
                    return true;
                }

                pc += length;
                if (op == ShapeOp.End) break;
            }

            return false;
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
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
