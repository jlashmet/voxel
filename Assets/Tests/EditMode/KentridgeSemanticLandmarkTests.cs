using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeSemanticLandmarkTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void EveryBespokeLandmarkUsesExplicitArchitectureGeometryInActiveCatalogue()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int checkedRoles = 0;
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    StructureForm form = ArchitectureCompiler.Resolve(
                        KentridgeDefinition.StructureIntent(plot),
                        KentridgeDefinition.Theme,
                        Seed);
                    if (form.IsGenerated)
                        continue;

                    FeatureDefinition definition = FindRoleDefinition(
                        in catalogue, (KentridgeRole)plot.RoleId);
                    AssertLandmarkProgram(
                        in catalogue,
                        in definition,
                        plot.Archetype);
                    checkedRoles++;
                }

                Assert.AreEqual(4, checkedRoles,
                    "The active Kentridge plan should exercise the four bespoke landmark programs.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static void AssertLandmarkProgram(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition,
            StructureArchetype archetype)
        {
            bool hasStyledFill = false;
            bool hasRoundedOpening = false;
            bool hasPlanarGlass = false;
            bool hasArchitecturalCylinder = false;
            bool hasSmoothRoof = false;

            int pc = definition.ProgramOffset;
            int end = definition.ProgramOffset + definition.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                if (op == ShapeOp.EmitRoundedBox)
                {
                    ushort surface = (ushort)catalogue.Program[pc + 10];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                    if (mode == PrimitiveMode.Fill
                        && (surface == SurfaceStyles.ArchitecturalRounded
                            || surface == SurfaceStyles.Beveled))
                        hasStyledFill = true;
                    if (mode == PrimitiveMode.Carve
                        && surface == SurfaceStyles.ArchitecturalRounded)
                        hasRoundedOpening = true;
                }
                else if (op == ShapeOp.EmitBox)
                {
                    byte material = (byte)catalogue.Program[pc + 8];
                    ushort surface = (ushort)catalogue.Program[pc + 9];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                    if (mode == PrimitiveMode.Fill
                        && surface == SurfaceStyles.Planar
                        && (material == 4 || material == 15))
                        hasPlanarGlass = true;
                }
                else if (op == ShapeOp.EmitCylinder)
                {
                    ushort surface = (ushort)catalogue.Program[pc + 9];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                    if (mode == PrimitiveMode.Fill
                        && surface == SurfaceStyles.ArchitecturalRounded)
                        hasArchitecturalCylinder = true;
                }
                else if (op == ShapeOp.EmitPrism)
                {
                    ushort surface = (ushort)catalogue.Program[pc + 10];
                    if (surface == SurfaceStyles.Smooth)
                        hasSmoothRoof = true;
                }

                pc += length;
                if (op == ShapeOp.End) break;
            }

            Assert.IsTrue(hasStyledFill || hasArchitecturalCylinder,
                $"{definition.Name} must author an explicit architecture surface instead of relying on inference.");
            Assert.IsTrue(hasSmoothRoof,
                $"{definition.Name} must author the selected roof reconstruction treatment.");

            if (archetype == StructureArchetype.Well)
            {
                Assert.IsTrue(hasArchitecturalCylinder,
                    "The well ring should carry shell reconstruction semantics on its cylinder.");
            }
            else
            {
                Assert.IsTrue(hasRoundedOpening,
                    $"{definition.Name} should author its doors/windows as semantic rounded openings.");
                Assert.IsTrue(hasPlanarGlass,
                    $"{definition.Name} should keep glazing planar instead of inheriting rounded detail geometry.");
            }
        }

        private static FeatureDefinition FindRoleDefinition(
            in FeatureCatalogue catalogue,
            KentridgeRole role)
        {
            string expected = "kentridge-role-" + role.ToString().ToLowerInvariant();
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                FeatureDefinition definition = catalogue.Definitions[i];
                if (definition.Name.ToString() == expected)
                    return definition;
            }

            Assert.Fail("Missing active Kentridge definition: " + expected);
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
