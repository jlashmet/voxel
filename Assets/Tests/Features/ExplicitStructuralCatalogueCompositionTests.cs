using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class ExplicitStructuralCatalogueCompositionTests
    {
        private const uint Seed = 0x51A7C0DEu;

        [Test]
        public void ExplicitCatalogueVisitsRegionOccupiedOnlyByAcceptedStructuralChild()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));
                using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                    expectedResidentRegions: 4,
                    mixedBrickCapacity: 4096,
                    changeJournalCapacity: 64);

                FeatureCatalogueBuildResult result = StructuresComposition.BuildExplicitFeatureCatalogue(
                    storage, in catalogue, Seed);

                Assert.AreEqual(2, result.RegionsVisited,
                    "The explicit lookdev helper must request both the root region and the child-only region.");
                Assert.IsTrue(storage.Residency.IsRegionResident(StructuralCompositionFixture.ChildRegion),
                    "The accepted structural child must become authoritative storage in its own region.");
                Assert.GreaterOrEqual(result.InstancesRasterised, 2,
                    "Root and accepted child must both be rasterised through production generation.");
                Assert.Greater(result.VoxelsWritten, 0);
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
