using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class UndergroundCavernRuinProductionAcceptanceTests
    {
        private const uint Seed = 0x5EED1234u;

        [Test]
        public void ProductionShowcaseWorldAuthorsTraversalCavernWithinBudgets()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            world.GenerateUndergroundCavernRuinsBlocking();

            Assert.That(world.HasUndergroundCavernRuins, Is.True);
            Assert.That(world.UndergroundCavernTraversalDistance, Is.GreaterThanOrEqualTo(2400),
                "The destination must remain a prolonged traversal from the surface mouth.");
            Assert.That(world.UndergroundCavernMouthOpeningCount, Is.GreaterThanOrEqualTo(4),
                "The production path must author a multi-lobed natural mouth, not only the rectangular core entrance.");
            Assert.That(world.UndergroundCavernDirectionChangeCount, Is.GreaterThanOrEqualTo(4),
                "The production descent must force multiple lateral direction changes.");
            Assert.That(world.UndergroundCavernStatueCount, Is.EqualTo(2));
            Assert.That(world.UndergroundCavernStalactiteCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(world.UndergroundCavernGeologicalCategoryCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(world.UndergroundCavernRouteLightCount, Is.EqualTo(3),
                "Sparse real local lights must guide the descent rather than relying on emissive voxels alone.");
            Assert.That(world.UndergroundCavernLocalLightCount, Is.InRange(4, 8),
                "Route and destination lights must remain bounded so most of the cave stays dark.");
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.InRange(1L, 55_000_000L),
                "The feature must stay inside the existing production authoring budget.");

            float3 delta = world.UndergroundCavernCentreMetres - world.UndergroundCavernEntranceMetres;
            Assert.That(math.length(new float2(delta.x, delta.z)), Is.GreaterThan(250f));
            Assert.That(delta.y, Is.LessThan(-70f),
                "The cavern must remain substantially below the natural surface entrance.");

            TestContext.WriteLine(
                $"cavern writes={world.UndergroundCavernVoxelsWritten}; traversal={world.UndergroundCavernTraversalDistance}; " +
                $"routeLights={world.UndergroundCavernRouteLightCount}; totalLights={world.UndergroundCavernLocalLightCount}; " +
                $"mouthLobes={world.UndergroundCavernMouthOpeningCount}; directionChanges={world.UndergroundCavernDirectionChangeCount}; " +
                $"statues={world.UndergroundCavernStatueCount}; stalactites={world.UndergroundCavernStalactiteCount}; " +
                $"geologyCategories={world.UndergroundCavernGeologicalCategoryCount}; depthDeltaMetres={delta.y:F1}");

            int lights = world.UndergroundCavernLocalLightCount;
            long writes = world.UndergroundCavernVoxelsWritten;
            float3 centre = world.UndergroundCavernCentreMetres;
            world.GenerateUndergroundCavernRuinsBlocking();
            Assert.That(world.UndergroundCavernLocalLightCount, Is.EqualTo(lights));
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.EqualTo(writes));
            Assert.That(math.all(world.UndergroundCavernCentreMetres == centre), Is.True,
                "The production entry point must remain idempotent for runtime/offline restoration.");
        }
    }
}
