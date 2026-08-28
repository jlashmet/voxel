using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOrganicRouteSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const int MarkedMinX = 910;
        private const int MarkedMaxX = 938;
        private const int MarkedMinZ = 286;
        private const int MarkedMaxZ = 304;

        [Test]
        public void SceneIssue20260826132234356OrganicRouteEdgesUseRoundSurfaceStamps()
        {
            FeatureCatalogue routes = KentridgeDirectedTownSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);

            try
            {
                int organicDefinitions = 0;
                int markedEnvelopePlacements = 0;

                for (int i = 0; i < routes.Definitions.Length; i++)
                {
                    FeatureDefinition definition = routes.Definitions[i];
                    string name = definition.Name.ToString();
                    Assert.IsTrue(name.StartsWith("kentridge-organic-route-"),
                        "The exact VoxelShowcase seed must exercise the organic circulation backend, not a district-only surface stage.");
                    organicDefinitions++;

                    Assert.AreEqual(20, definition.Precedence);
                    Assert.AreEqual(2, definition.MaxPrimitives,
                        name + " must retain the bounded two-primitive route stamp budget.");
                    Assert.AreEqual(definition.Footprint.x, definition.Footprint.z,
                        name + " route stamps must keep their existing square bounding footprint.");

                    int width = definition.Footprint.x;
                    int radius = width / 2;
                    int fill = 4;
                    int clear = 24;
                    int pc = definition.ProgramOffset;

                    AssertCylinder(routes, pc, radius, fill, radius, radius, clear,
                        1, 0, PrimitiveMode.Carve, name + " clearance");
                    pc += ShapeOps.InstructionLength((ShapeOp)routes.Program[pc]);
                    AssertCylinder(routes, pc, radius, 0, radius, radius, fill,
                        1, 13, PrimitiveMode.Fill, name + " Dirt surface");
                    pc += ShapeOps.InstructionLength((ShapeOp)routes.Program[pc]);
                    Assert.AreEqual(ShapeOp.End, (ShapeOp)routes.Program[pc],
                        name + " must end after its bounded round carve/fill pair.");

                    PlacementRule rule = routes.Rules[i];
                    Assert.AreEqual(i, rule.DefinitionId);
                    for (int p = 0; p < rule.ExplicitCount; p++)
                    {
                        ExplicitPlacement placement =
                            routes.ExplicitPlacements[rule.ExplicitOffset + p];
                        int minX = placement.Position.x;
                        int maxX = minX + width;
                        int minZ = placement.Position.z;
                        int maxZ = minZ + width;
                        if (maxX >= MarkedMinX && minX <= MarkedMaxX
                            && maxZ >= MarkedMinZ && minZ <= MarkedMaxZ)
                            markedEnvelopePlacements++;
                    }
                }

                Assert.Greater(organicDefinitions, 0,
                    "The VoxelShowcase seed emitted no organic route definitions.");
                Assert.Greater(markedEnvelopePlacements, 0,
                    "No live organic route stamp overlaps the corrected upper marked world envelope; the regression would not cover the captured defect.");
            }
            finally
            {
                routes.Dispose();
            }
        }

        private static void AssertCylinder(
            FeatureCatalogue catalogue,
            int pc,
            int cx,
            int y,
            int cz,
            int radius,
            int height,
            int axis,
            byte material,
            PrimitiveMode mode,
            string label)
        {
            Assert.AreEqual(ShapeOp.EmitCylinder, (ShapeOp)catalogue.Program[pc],
                label + " must use a round stamp so diagonal route edges cannot expose square corners.");
            Assert.AreEqual(cx, catalogue.Program[pc + 2], label + " center X");
            Assert.AreEqual(y, catalogue.Program[pc + 3], label + " Y");
            Assert.AreEqual(cz, catalogue.Program[pc + 4], label + " center Z");
            Assert.AreEqual(radius, catalogue.Program[pc + 5], label + " radius");
            Assert.AreEqual(height, catalogue.Program[pc + 6], label + " height");
            Assert.AreEqual(axis, catalogue.Program[pc + 7], label + " axis");
            Assert.AreEqual(material, (byte)catalogue.Program[pc + 8], label + " material");
            Assert.AreEqual(mode, (PrimitiveMode)catalogue.Program[pc + 11], label + " mode");
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
