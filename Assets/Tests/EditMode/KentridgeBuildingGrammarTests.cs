using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeBuildingGrammarTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void GrammarIsDeterministicAndVariesRoleBuildings()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var signatures = new HashSet<string>();
            int generated = 0;
            int bespoke = 0;

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                KentridgeBuildingForm a = KentridgeBuildingGrammar.Resolve(plot, Seed);
                KentridgeBuildingForm b = KentridgeBuildingGrammar.Resolve(plot, Seed);

                Assert.AreEqual(Signature(a), Signature(b),
                    "Same stable role and seed must resolve the same architectural form.");
                Assert.AreEqual(plot.RoleId, a.RoleId);
                Assert.AreEqual(plot.Archetype, a.Archetype);
                Assert.AreEqual(plot.District, a.District);

                if (a.IsGenerated)
                {
                    generated++;
                    signatures.Add(Signature(a));
                    KentridgeBuildingGrammar.ValidateGenerated(a);
                }
                else bespoke++;
            }

            Assert.AreEqual(13, generated,
                "Houses, shops, inn, and pub should now be grammar-generated per stable role.");
            Assert.AreEqual(4, bespoke,
                "Only the remaining landmark and utility forms stay bespoke in this migration slice.");
            Assert.GreaterOrEqual(signatures.Count, 11,
                "The grammar should not collapse role buildings back into archetype clones.");
        }

        [Test]
        public void HospitalityRolesNoLongerShareOneTemplate()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            KentridgeBuildingForm inn = Resolve(plan, KentridgeRole.Inn);
            KentridgeBuildingForm pub = Resolve(plan, KentridgeRole.Pub);

            Assert.IsTrue(inn.IsHospitality);
            Assert.AreEqual(3, inn.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.RearWing, inn.Footprint);
            Assert.AreEqual(KentridgeRoofForm.TwinGable, inn.Roof);
            Assert.AreEqual(KentridgeWindowStyle.Warm, inn.WindowStyle);

            Assert.IsTrue(pub.IsHospitality);
            Assert.AreEqual(2, pub.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.SideWing, pub.Footprint);
            Assert.AreEqual(KentridgeRoofForm.GableWithLeanTo, pub.Roof);
            Assert.AreEqual(KentridgeFrontageRhythm.Asymmetric, pub.FrontageRhythm);
            Assert.AreNotEqual(Signature(inn), Signature(pub),
                "Inn and pub must no longer share one prefab-like geometry.");
        }

        [Test]
        public void GrammarCatalogueUsesStableRoleIdentityAndExactlySeventeenStructures()
        {
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(17, catalogue.Definitions.Length);
                Assert.AreEqual(17, catalogue.Rules.Length);
                Assert.AreEqual(17, catalogue.Anchors.Length);
                Assert.AreEqual(17, catalogue.ExplicitPlacements.Length);

                for (int roleId = 0; roleId < 17; roleId++)
                {
                    KentridgeRole role = (KentridgeRole)roleId;
                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    PlacementRule rule = catalogue.Rules[roleId];

                    Assert.AreEqual(FeatureKind.Structure, definition.Kind);
                    StringAssert.AreEqualIgnoringCase(
                        "kentridge-role-" + role.ToString(),
                        definition.Name.ToString());
                    Assert.AreEqual(roleId, rule.DefinitionId,
                        "Definition identity should follow stable Kentridge role identity.");
                    Assert.AreEqual(roleId, rule.ExplicitOffset);
                    Assert.AreEqual(1, rule.ExplicitCount);
                    Assert.Greater(definition.ProgramLength, 0);
                    Assert.AreEqual(1, definition.AnchorCount);
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void PubDoorwayRemainsCarvedAfterFacadeFraming()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                int roleId = (int)KentridgeRole.Pub;
                FeatureDefinition definition = catalogue.Definitions[roleId];
                ExplicitPlacement placement = catalogue.ExplicitPlacements[roleId];
                ParameterSet parameters = default;
                ulong instanceSeed = FeatureGeneration.InstanceSeed(
                    Seed, roleId, placement.Position);

                EvaluationResult evaluation = ShapeProgram.Evaluate(
                    in catalogue,
                    roleId,
                    in parameters,
                    placement.Position,
                    placement.Orientation,
                    Seed,
                    instanceSeed,
                    primitives,
                    anchors);
                Assert.AreEqual(EvaluationResult.Ok, evaluation);

                Assert.IsTrue(KentridgeGameplaySiteAccessResolver.TryResolve(
                    plan, roleId, 1, out KentridgeGameplaySiteAccess access));

                // CharacterMotor has a 0.3 m radius. Validate a full 0.6 m-wide traversal corridor
                // from the exterior air landing through the decorated facade and into the interior.
                // The two historical blockers were a 6 dm-deep timber rail left at the inner edge and
                // the first composed-world voxel immediately outside an inward-only doorway carve.
                int[] lateralOffsets = { -3, 0, 3 };
                int[] depthOffsets = { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6 };
                int[] heightOffsets = { 0, 1, 4, 8, 12, 16, 18, 21 };
                Int2 inward = access.Inward;
                var lateral = new Int2(-inward.Y, inward.X);

                for (int d = 0; d < depthOffsets.Length; d++)
                for (int l = 0; l < lateralOffsets.Length; l++)
                for (int h = 0; h < heightOffsets.Length; h++)
                {
                    int depth = depthOffsets[d];
                    int side = lateralOffsets[l];
                    int height = heightOffsets[h];
                    var point = new int3(
                        access.Entrance.Position.X + inward.X * depth + lateral.X * side,
                        access.Entrance.Position.Y + height,
                        access.Entrance.Position.Z + inward.Y * depth + lateral.Y * side);
                    AssertFinalBoxMode(
                        primitives,
                        point,
                        PrimitiveMode.Carve,
                        "Pub public doorway corridor was refilled at lateral=" + side +
                        "dm, depth=" + depth + "dm, height=" + height + "dm.");
                }
            }
            finally
            {
                if (anchors.IsCreated) anchors.Dispose();
                if (primitives.IsCreated) primitives.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void EveryAccessibleRoleUsesAFramedArchAtItsPublicEntrance()
        {
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                for (int roleId = 0; roleId < 17; roleId++)
                {
                    if (roleId == (int)KentridgeRole.Well) continue;

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
                    Assert.AreEqual(EvaluationResult.Ok, evaluation);

                    bool hasArchSurround = false;
                    bool hasArchOpening = false;
                    for (int i = 0; i < primitives.Length; i++)
                    {
                        Primitive primitive = primitives[i];
                        if (primitive.Shape != PrimitiveShape.Prism
                            || primitive.Profile != PrismProfile.Arch)
                            continue;
                        if (primitive.Mode == PrimitiveMode.Carve) hasArchOpening = true;
                        if (primitive.Mode == PrimitiveMode.Fill) hasArchSurround = true;
                    }

                    Assert.IsTrue(hasArchSurround,
                        ((KentridgeRole)roleId) + " is missing its structural arch surround.");
                    Assert.IsTrue(hasArchOpening,
                        ((KentridgeRole)roleId) + " is missing its curved entrance head.");
                }
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void EveryGeneratedRoleAuthorsAProjectingFacadeSignature()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                for (int plotIndex = 0; plotIndex < plan.Plots.Count; plotIndex++)
                {
                    BuildingPlot plot = plan.Plots[plotIndex];
                    if (!KentridgeBuildingGrammar.Resolve(plot, Seed).IsGenerated) continue;

                    int roleId = plot.RoleId;
                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    int anchorZ = int.MinValue;
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        if (op == ShapeOp.SetAnchor)
                            anchorZ = catalogue.Program[pc + 5];
                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End) break;
                    }

                    Assert.AreNotEqual(int.MinValue, anchorZ, "Missing public entrance anchor.");
                    bool projectsFromFacade = false;
                    pc = definition.ProgramOffset;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        if ((op == ShapeOp.EmitBox || op == ShapeOp.EmitRoundedBox)
                            && catalogue.Program[pc + 4] <= anchorZ - 5)
                        {
                            if (op == ShapeOp.EmitBox
                                && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Fill)
                                projectsFromFacade = true;
                            if (op == ShapeOp.EmitRoundedBox
                                && (PrimitiveMode)catalogue.Program[pc + 12] == PrimitiveMode.Fill)
                                projectsFromFacade = true;
                        }
                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End) break;
                    }

                    Assert.IsTrue(projectsFromFacade,
                        ((KentridgeRole)roleId) +
                        " should project a porch, canopy, sign, pier, or planter from its facade.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static void AssertFinalBoxMode(
            NativeList<Primitive> primitives,
            int3 point,
            PrimitiveMode expected,
            string message)
        {
            bool found = false;
            PrimitiveMode finalMode = default;
            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Shape != PrimitiveShape.Box) continue;
                if (point.x < primitive.A.x || point.x > primitive.B.x
                    || point.y < primitive.A.y || point.y > primitive.B.y
                    || point.z < primitive.A.z || point.z > primitive.B.z)
                    continue;
                if (primitive.Mode != PrimitiveMode.Fill
                    && primitive.Mode != PrimitiveMode.FillIfEmpty
                    && primitive.Mode != PrimitiveMode.Carve)
                    continue;

                found = true;
                finalMode = primitive.Mode;
            }

            Assert.IsTrue(found, "No occupancy primitive covered doorway probe " + point + ".");
            Assert.AreEqual(expected, finalMode, message);
        }

        private static KentridgeBuildingForm Resolve(SettlementPlan plan, KentridgeRole role)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.RoleId == (int)role)
                    return KentridgeBuildingGrammar.Resolve(plot, Seed);
            }

            Assert.Fail("Missing Kentridge role " + role);
            return default;
        }

        private static string Signature(KentridgeBuildingForm form)
        {
            return form.Mode + ":" + form.Footprint + ":" + form.Roof + ":"
                 + form.FrontageRhythm + ":" + form.WindowStyle + ":"
                 + form.WidthDm + "x" + form.DepthDm + ":" + form.Storeys + ":"
                 + form.DoorOffsetDm + ":" + form.UpperOverhangDm + ":"
                 + form.RoofHeightDm + ":" + form.WingWidthDm + "x" + form.WingDepthDm
                 + ":" + form.WingOnRight + ":" + form.ChimneyOnRight;
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
