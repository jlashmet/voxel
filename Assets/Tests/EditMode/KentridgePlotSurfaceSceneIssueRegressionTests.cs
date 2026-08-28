using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgePlotSurfaceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const int MarkedWorldX = 910;
        private const int MarkedWorldZ = 295;

        [Test]
        public void SceneIssue20260826132234356MayorHousePlotEdgePreservesNaturalTerrain()
        {
            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);

            try
            {
                int definitionId = (int)StructureArchetype.WideHouse;
                FeatureDefinition definition = plots.Definitions[definitionId];
                PlacementRule rule = plots.Rules[definitionId];

                Assert.AreEqual(definitionId, rule.DefinitionId);
                Assert.AreEqual(40, definition.Precedence,
                    "Plot grading must remain in its established generation stage.");

                ExplicitPlacement mayor = default;
                bool foundMayor = false;
                for (int i = 0; i < rule.ExplicitCount; i++)
                {
                    ExplicitPlacement placement = plots.ExplicitPlacements[rule.ExplicitOffset + i];
                    if (placement.Position.x == 910 && placement.Position.z == 250)
                    {
                        mayor = placement;
                        foundMayor = true;
                        break;
                    }
                }

                Assert.IsTrue(foundMayor,
                    "The exact VoxelShowcase seed must retain MayorHouse at the captured organic plot.");

                int localX = MarkedWorldX - mayor.Position.x;
                int localZ = MarkedWorldZ - mayor.Position.z;
                Assert.AreEqual(0, localX,
                    "The captured upper mark should remain on the MayorHouse parcel west edge.");
                Assert.AreEqual(45, localZ);

                int mossLayers = 0;
                bool gradingTouchesMarkedEdge = false;
                int pc = definition.ProgramOffset;
                int end = pc + definition.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)plots.Program[pc];
                    if (op == ShapeOp.EmitBox)
                    {
                        int x = plots.Program[pc + 2];
                        int z = plots.Program[pc + 4];
                        int sx = plots.Program[pc + 5];
                        int sz = plots.Program[pc + 7];
                        byte material = (byte)plots.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)plots.Program[pc + 11];

                        if (material == 14 && mode == PrimitiveMode.Fill)
                        {
                            mossLayers++;
                            Assert.AreEqual(6, x,
                                "WideHouse grading must begin at the building envelope, not the parcel boundary.");
                            Assert.AreEqual(4, z);
                            Assert.AreEqual(116, sx);
                            Assert.AreEqual(100, sz);
                        }

                        if (localX >= x && localX < x + sx
                            && localZ >= z && localZ < z + sz)
                            gradingTouchesMarkedEdge = true;
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End) break;
                }

                Assert.AreEqual(13, mossLayers,
                    "The bounded program budget remains stable while all passes stay inside the building core.");
                Assert.IsFalse(gradingTouchesMarkedEdge,
                    "Plot grading must leave the captured parcel edge to deterministic natural terrain.");

                int targetSurface = mayor.Position.y + 12;
                int naturalSurface = TerrainQuery.HeightAt(
                    MarkedWorldX, MarkedWorldZ, VoxelShowcaseSeed);
                Assert.AreEqual(221, targetSurface,
                    "The exact scene seed should retain the MayorHouse frontage target used by the capture.");
                Assert.AreEqual(223, naturalSurface,
                    "The captured parcel edge should retain its deterministic natural terrain height.");
                Assert.Greater(naturalSurface, targetSurface,
                    "A rising artificial feather is unnecessary where natural ground already meets the graded core.");
            }
            finally
            {
                plots.Dispose();
            }
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
