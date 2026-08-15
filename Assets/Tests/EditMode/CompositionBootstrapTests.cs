using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public class CompositionBootstrapTests
    {
        [Test]
        public void StorageBootstrapOwnsRuntimeAndExposesOnlyApiCapabilities()
        {
            using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 4,
                mixedBrickCapacity: 8,
                changeJournalCapacity: 16);

            Assert.That(storage.Generation, Is.InstanceOf<IRegionGenerationStore>());
            Assert.That(storage.Reads, Is.InstanceOf<IRegionReadSource>());
            Assert.That(storage.Mutations, Is.InstanceOf<IRegionMutationStore>());
            Assert.That(storage.Residency, Is.InstanceOf<IRegionResidencyStore>());
            Assert.That(storage.Snapshots, Is.InstanceOf<IRegionSnapshotSource>());
            Assert.That(storage.SnapshotMutations, Is.InstanceOf<IRegionSnapshotMutationStore>());
            Assert.That(storage.SurfaceQuery, Is.InstanceOf<IVoxelSurfaceQuery>());
            Assert.That(storage.Changes, Is.InstanceOf<IVoxelChangeSource>());

            int3 region = int3.zero;
            Assert.That(storage.Residency.IsRegionResident(region), Is.False);
            storage.Residency.EnsureRegionResident(region);
            Assert.That(storage.Residency.IsRegionResident(region), Is.True);
            Assert.That(storage.Reads.TryAcquireRegion(region, out _), Is.True);
        }

        [Test]
        public void PublicStorageLifetimeSurfaceDoesNotExposeRuntimeTypes()
        {
            foreach (var property in typeof(IVoxelStorageRuntime).GetProperties())
            {
                Type type = property.PropertyType;
                Assert.That(type.Namespace, Does.Not.Contain(".Runtime"),
                    $"Composition leaked concrete Runtime type through {property.Name}.");
            }
        }
    }
}
