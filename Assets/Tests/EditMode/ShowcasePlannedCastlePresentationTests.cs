using System.Linq;
using Game.WorldBuilder.Voxel;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcasePlannedCastlePresentationTests
    {
        [Test]
        public void PlannedCastleIsQueryableBeforeAnyDetailedRegionGeneration()
        {
            using var world = new ShowcaseWorld(
                seed: 0xA11CEu,
                brickPoolCapacity: 512,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            Assert.That(world.RegionsGenerated, Is.Zero);

            IFeaturePresentationSource source = world.FeaturePresentation;

            Assert.That(world.RegionsGenerated, Is.Zero,
                "Far presentation must not generate any detailed voxel region.");
            Assert.That(world.TryGetPlannedCastle(out var plan), Is.True);
            Assert.That(world.RequiredCastleRegions, Is.GreaterThan(0),
                "Planning may enumerate dependencies, but must not make them resident.");
            Assert.That(world.ReadyCastleRegions, Is.Zero);

            // The public composition plan intentionally exposes only semantic identity/centre;
            // query a conservative window around that centre rather than reaching into the
            // game-owned castle implementation for private derived bounds.
            var query = new FeaturePresentationBounds(
                plan.Centre - new int3(1024, 512, 1024),
                plan.Centre + new int3(1024, 1024, 1024));
            var matches = source.Query(query);
            FeaturePresentationBake castle = matches.FirstOrDefault(bake =>
                bake.Kind == FeatureKind.Structure
                && bake.BoundsMin.x <= plan.Centre.x && bake.BoundsMax.x >= plan.Centre.x
                && bake.BoundsMin.z <= plan.Centre.z && bake.BoundsMax.z >= plan.Centre.z);

            Assert.That(castle, Is.Not.Null,
                "The never-visited planned castle must be present in the generic sparse source.");
            Assert.That(castle.PrimitiveCount, Is.InRange(1, 64));
            Assert.That(world.RegionsGenerated, Is.Zero);
            Assert.That(world.ReadyCastleRegions, Is.Zero);

            var repeated = world.FeaturePresentation.Query(query)
                .First(bake => bake.SourceId == castle.SourceId);
            Assert.That(repeated.Revision, Is.EqualTo(castle.Revision));
            Assert.That(world.RegionsGenerated, Is.Zero);
        }

        [Test]
        public void PlannedCastleAndIndependentMountainCoexistWithoutDetailedResidency()
        {
            const uint worldSeed = 0xA11CEu;
            using var world = new ShowcaseWorld(
                seed: worldSeed,
                brickPoolCapacity: 512,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            Assert.That(world.RegionsGenerated, Is.Zero);
            Assert.That(world.TryGetPlannedCastle(out var plan), Is.True);

            var castleQuery = new FeaturePresentationBounds(
                plan.Centre - new int3(1024, 512, 1024),
                plan.Centre + new int3(1024, 1024, 1024));
            FeaturePresentationBake castle = world.FeaturePresentation.Query(castleQuery)
                .First(bake =>
                    bake.Kind == FeatureKind.Structure
                    && bake.BoundsMin.x <= plan.Centre.x && bake.BoundsMax.x >= plan.Centre.x
                    && bake.BoundsMin.z <= plan.Centre.z && bake.BoundsMax.z >= plan.Centre.z);

            FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                new MountainLandmarkSpec(
                    origin: new int3(2048, 180, 4096),
                    footprintEdge: 256,
                    mountainRadius: 96,
                    mountainHeight: 80,
                    summitRadius: 32,
                    pathWidth: 12,
                    pathRun: 80,
                    pathRise: 12,
                    switchbackCount: 5,
                    placeholderSize: 16),
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 2,
                allocator: Allocator.Temp);

            try
            {
                var sharedManifest = new FeaturePresentationManifest(sectorSizeVoxels: 512);
                sharedManifest.Upsert(castle);
                int mountainBakeCount = FeaturePresentationCatalogueBaker.Populate(
                    in mountain, worldSeed, sharedManifest);

                Assert.That(mountainBakeCount, Is.GreaterThan(0));
                var all = sharedManifest.Query(new FeaturePresentationBounds(
                    new int3(-32768, -32768, -32768),
                    new int3(32768, 32768, 32768)));
                FeaturePresentationBake mountainBake = all.FirstOrDefault(bake =>
                    bake.Kind == FeatureKind.Landform);

                Assert.That(mountainBake, Is.Not.Null,
                    "A normal non-building producer must enter the same sparse presentation index without a producer-specific visibility adapter.");
                Assert.That(all.Any(bake => bake.SourceId == castle.SourceId), Is.True);
                Assert.That(mountainBake.SourceId, Is.Not.EqualTo(castle.SourceId));

                FeaturePresentationManifest repeatedMountain =
                    FeaturePresentationCatalogueBaker.Build(in mountain, worldSeed, sectorSizeVoxels: 512);
                FeaturePresentationBake repeatedBake = repeatedMountain.Query(new FeaturePresentationBounds(
                        new int3(-32768, -32768, -32768),
                        new int3(32768, 32768, 32768)))
                    .First(bake => bake.SourceId == mountainBake.SourceId);
                Assert.That(repeatedBake.Revision, Is.EqualTo(mountainBake.Revision));
                Assert.That(repeatedBake.BoundsMin, Is.EqualTo(mountainBake.BoundsMin));
                Assert.That(repeatedBake.BoundsMax, Is.EqualTo(mountainBake.BoundsMax));

                Assert.That(world.RegionsGenerated, Is.Zero,
                    "Neither querying the planned castle nor baking an independent mountain may make detailed voxel regions resident.");
                Assert.That(world.ReadyCastleRegions, Is.Zero);
            }
            finally
            {
                mountain.Dispose();
            }
        }
    }
}
