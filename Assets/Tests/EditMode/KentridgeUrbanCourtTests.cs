using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanCourtTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void EveryUrbanBlockRealizesItsProtectedInteriorAsASeparateHardCourt()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);
            FeatureCatalogue catalogue = KentridgeUrbanCourtCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(plan.Blocks.Count, catalogue.Definitions.Length);
                Assert.AreEqual(8, catalogue.Definitions.Length);
                Assert.AreEqual(8, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    KentridgeUrbanBlock block = plan.Blocks[i];
                    FeatureDefinition definition = catalogue.Definitions[i];
                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(KentridgeUrbanCourtCatalogue.CourtPrecedence, definition.Precedence);
                    Assert.Greater(definition.Footprint.x, 0, block.Id);
                    Assert.Greater(definition.Footprint.z, 0, block.Id);
                    Assert.Less(definition.Footprint.x, block.WidthDm, block.Id);
                    Assert.Less(definition.Footprint.z, block.DepthDm, block.Id);
                    Assert.AreEqual(1, catalogue.Rules[i].ExplicitCount);
                }
            }
            finally { catalogue.Dispose(); }
        }

        [Test]
        public void CourtFloorsStayBelowFabricAndAccessPrecedence()
        {
            Assert.Less(KentridgeUrbanCourtCatalogue.CourtPrecedence, 86);
            Assert.Less(KentridgeUrbanCourtCatalogue.CourtPrecedence, 94);
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(foundationStone: 1, masonry: 1, darkMasonry: 6, timber: 2, glass: 4, warmWindow: 15, roofTile: 8, slate: 7, cloth: 9, moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
