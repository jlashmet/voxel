using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVerticalFrontageTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void DenseUrbanBlocksPromoteCourtEdgesIntoOccupiedVerticalFrontages()
        {
            KentridgeVerticalFrontagePlan plan = KentridgeVerticalFrontagePlanner.Build(Seed);
            Assert.AreEqual(6, plan.Zones.Count,
                "Every dense block above the lower ward should own one inhabited downhill face.");

            int civic = 0;
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                KentridgeVerticalFrontageZone zone = plan.Zones[i];
                Assert.AreNotEqual(KentridgeUrbanBand.LowerWard, zone.Band);
                Assert.Greater(zone.LengthDm, zone.GapWidthDm);
                Assert.GreaterOrEqual(zone.HeightDm, 38);
                Assert.Greater(zone.DepthDm, 0);
                Assert.Greater(zone.BayPitchDm, 0);
                if (zone.Band == KentridgeUrbanBand.CivicCrown) civic++;
            }

            Assert.AreEqual(2, civic,
                "Both sides of the civic climb should be built into the summit terrace.");
        }

        [Test]
        public void VerticalFrontagesEmbedIntoTerraceAndEndOnAuthoredDownhillEdge()
        {
            KentridgeVerticalFrontagePlan plan = KentridgeVerticalFrontagePlanner.Build(Seed);
            FeatureCatalogue catalogue = KentridgeVerticalFrontageCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(6, catalogue.Definitions.Length);
                Assert.AreEqual(6, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    KentridgeVerticalFrontageZone zone = plan.Zones[i];
                    FeatureDefinition definition = catalogue.Definitions[i];
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[i];

                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(85, definition.Precedence);
                    Assert.Greater(definition.Footprint.x, 0);
                    Assert.Greater(definition.Footprint.y, 0);
                    Assert.AreEqual(zone.DepthDm, definition.Footprint.z,
                        "Test settings use one voxel per decimetre.");

                    Assert.AreEqual(
                        zone.StartDm.Y,
                        placement.Position.z + definition.Footprint.z,
                        "The open facade must end exactly at the downhill block edge, with its depth embedded uphill into the terrace.");
                    Assert.Less(placement.Position.z, zone.StartDm.Y,
                        "Vertical frontage depth must no longer project beyond the downhill block edge.");
                    Assert.GreaterOrEqual(CountOps(catalogue, definition, ShapeOp.EmitBox), 4);
                    Assert.GreaterOrEqual(CountOps(catalogue, definition, ShapeOp.EmitBox, PrimitiveMode.Carve), 1,
                        "Embedded undercrofts must excavate the terrace before rebuilding hard architecture.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static int CountOps(
            FeatureCatalogue catalogue,
            FeatureDefinition definition,
            ShapeOp target,
            PrimitiveMode? mode = null)
        {
            int count = 0;
            int pc = definition.ProgramOffset;
            int end = pc + definition.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                Assert.Greater(length, 0);
                if (op == ShapeOp.End) break;
                if (op == target)
                {
                    if (!mode.HasValue || catalogue.Program[pc + length - 1] == (int)mode.Value)
                        count++;
                }
                pc += length;
            }
            return count;
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
