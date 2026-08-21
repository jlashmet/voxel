using Game.Materials.Api;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeStructureComparisonTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void ComparisonCatalogue_IsolatesArchitectureVariantUnderMatchedConditions()
        {
            VoxelWorldGenSettings settings = Settings();
            FeatureCatalogue implicitCurrent = KentridgeGrammarVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            FeatureCatalogue explicitCurrent = KentridgeGrammarVoxelCatalogue.Build(
                Seed, settings, KentridgeArchitectureVariant.Current, Allocator.Temp);
            FeatureCatalogue pair = KentridgeStructureComparisonCatalogue.Build(
                Seed, settings, roleId: 1, Allocator.Temp);
            try
            {
                Assert.That(explicitCurrent.Hash, Is.EqualTo(implicitCurrent.Hash),
                    "The default gameplay build must remain the current architecture.");
                Assert.That(pair.Definitions.Length, Is.EqualTo(2));
                Assert.That(pair.Rules.Length, Is.EqualTo(2));
                Assert.That(pair.Definitions[0].Footprint, Is.EqualTo(pair.Definitions[1].Footprint));
                Assert.That(pair.Definitions[0].BasePlane, Is.EqualTo(BasePlaneRule.FixedAltitude));
                Assert.That(pair.Definitions[1].BasePlane, Is.EqualTo(BasePlaneRule.FixedAltitude));
                Assert.That(pair.Definitions[0].FixedAltitude,
                    Is.EqualTo(pair.Definitions[1].FixedAltitude));
                Assert.That(pair.ExplicitPlacements[0].Orientation,
                    Is.EqualTo(pair.ExplicitPlacements[1].Orientation));
                Assert.That(pair.Definitions[0].ProgramLength,
                    Is.LessThan(pair.Definitions[1].ProgramLength),
                    "The selected generated structure should expose the added arch/signature program.");
                Assert.That(pair.ExplicitPlacements[1].Position.x,
                    Is.GreaterThan(pair.ExplicitPlacements[0].Position.x
                        + pair.Definitions[0].Footprint.x));
            }
            finally
            {
                implicitCurrent.Dispose();
                explicitCurrent.Dispose();
                pair.Dispose();
            }
        }

        private static VoxelWorldGenSettings Settings() =>
            new(1, new VoxelMaterialMap(
                GameMaterialIds.MasonryLarge,
                GameMaterialIds.MasonrySmall,
                GameMaterialIds.DarkStone,
                GameMaterialIds.Wood,
                GameMaterialIds.Glass,
                GameMaterialIds.LitWindow,
                GameMaterialIds.Tile,
                GameMaterialIds.Slate,
                GameMaterialIds.Cloth,
                GameMaterialIds.Moss,
                GameMaterialIds.Water,
                GameMaterialIds.Dirt));
    }
}
