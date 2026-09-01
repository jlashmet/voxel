using Game.WorldBuilder.Voxel;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FeaturePresentationCatalogueBakerTests
    {
        private const uint Seed = 0x46415237u;

        [Test]
        public void Build_ComposedProductionCatalogueAutomaticallyBakesUnrelatedFeatureKinds()
        {
            FeatureCatalogue town = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildKentridgeSettings(), Allocator.Temp);
            FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                BuildMountainSpec(),
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 2,
                allocator: Allocator.Temp);
            FeatureCatalogue combined = default;

            try
            {
                combined = FeatureCatalogueComposer.Combine(in town, in mountain, Allocator.Temp);

                FeaturePresentationManifest manifest =
                    FeaturePresentationCatalogueBaker.Build(in combined, Seed, sectorSizeVoxels: 512);
                var bakes = manifest.Query(new FeaturePresentationBounds(
                    new int3(-32768, -32768, -32768),
                    new int3(32768, 32768, 32768)));

                Assert.That(bakes.Count, Is.GreaterThan(0));
                Assert.That(ContainsKindAndShape(bakes, FeatureKind.Structure, PrimitiveShape.Box), Is.True,
                    "A normal Kentridge structure should enter presentation baking through catalogue composition alone.");
                Assert.That(ContainsKindAndShape(bakes, FeatureKind.Landform, PrimitiveShape.Frustum), Is.True,
                    "The unrelated mountain landform should use the identical catalogue bake path without registration.");

                FeaturePresentationManifest repeat =
                    FeaturePresentationCatalogueBaker.Build(in combined, Seed, sectorSizeVoxels: 512);
                var repeatedBakes = repeat.Query(new FeaturePresentationBounds(
                    new int3(-32768, -32768, -32768),
                    new int3(32768, 32768, 32768)));

                Assert.That(repeatedBakes.Count, Is.EqualTo(bakes.Count));
                for (int i = 0; i < bakes.Count; i++)
                {
                    Assert.That(repeatedBakes[i].SourceId, Is.EqualTo(bakes[i].SourceId));
                    Assert.That(repeatedBakes[i].Revision, Is.EqualTo(bakes[i].Revision));
                    Assert.That(repeatedBakes[i].BoundsMin, Is.EqualTo(bakes[i].BoundsMin));
                    Assert.That(repeatedBakes[i].BoundsMax, Is.EqualTo(bakes[i].BoundsMax));
                }
            }
            finally
            {
                if (combined.IsCreated) combined.Dispose();
                mountain.Dispose();
                town.Dispose();
            }
        }

        private static bool ContainsKindAndShape(
            System.Collections.Generic.IReadOnlyList<FeaturePresentationBake> bakes,
            FeatureKind kind,
            PrimitiveShape shape)
        {
            for (int bakeIndex = 0; bakeIndex < bakes.Count; bakeIndex++)
            {
                FeaturePresentationBake bake = bakes[bakeIndex];
                if (bake.Kind != kind) continue;
                for (int primitiveIndex = 0; primitiveIndex < bake.PrimitiveCount; primitiveIndex++)
                {
                    if (bake.GetPrimitive(primitiveIndex).Shape == shape)
                        return true;
                }
            }

            return false;
        }

        private static MountainLandmarkSpec BuildMountainSpec() => new MountainLandmarkSpec(
            origin: new int3(2048, 180, 4096),
            footprintEdge: 256,
            mountainRadius: 96,
            mountainHeight: 80,
            summitRadius: 32,
            pathWidth: 12,
            pathRun: 80,
            pathRise: 12,
            switchbackCount: 5,
            placeholderSize: 16);

        private static VoxelWorldGenSettings BuildKentridgeSettings()
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
