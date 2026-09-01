using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

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
    }
}
