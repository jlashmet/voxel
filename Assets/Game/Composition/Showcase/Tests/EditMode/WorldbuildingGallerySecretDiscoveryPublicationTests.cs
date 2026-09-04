using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Regression for the production Gallery's post-bake SecretDiscovery authoring boundary.
    /// The baked world is already visible to derived consumers before this feature is composed,
    /// so finishing its bulk mutation must publish the new resident state to the world change feed.
    /// </summary>
    public sealed class WorldbuildingGallerySecretDiscoveryPublicationTests
    {
        private const uint GallerySeed = 0x5EED1234u;

        [Test]
        public void PostBakeSecretAuthoringPublishesFinishedResidentState()
        {
            using var world = new ShowcaseWorld(
                GallerySeed,
                brickPoolCapacity: 196608,
                loadRadiusRegions: 4,
                unloadRadiusRegions: 6);

            world.StartWorldbuildingGalleryBlocking(null);

            // Preload every region that EnsureWorldbuildingGallerySecretDiscoveryBlocking normally
            // prepares before it authors the secret. Sampling the cursor only after this step keeps
            // region-generation publication from satisfying the regression accidentally.
            MethodInfo preloadGallery = typeof(ShowcaseWorld).GetMethod(
                "PreloadGalleryRegions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo entranceMethod = typeof(ShowcaseWorld).GetMethod(
                "WorldbuildingGallerySecretCaveEntrance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo preloadSecret = typeof(ShowcaseWorld).GetMethod(
                "PreloadWorldbuildingGallerySecretCaveRegions",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Multiple(() =>
            {
                Assert.That(preloadGallery, Is.Not.Null);
                Assert.That(entranceMethod, Is.Not.Null);
                Assert.That(preloadSecret, Is.Not.Null);
            });

            preloadGallery.Invoke(world, null);
            int3 entrance = (int3)entranceMethod.Invoke(world, null);
            preloadSecret.Invoke(world, new object[] { entrance });

            ulong before = world.Changes.CurrentVersion;
            world.EnsureWorldbuildingGallerySecretDiscoveryBlocking();

            ulong cursor = before;
            var changes = new List<VoxelChangeRecord>();
            bool retained = world.Changes.ReadSince(ref cursor, changes);

            Assert.Multiple(() =>
            {
                Assert.That(retained, Is.True,
                    "The SecretDiscovery publication must remain readable by existing derived consumers.");
                Assert.That(world.Changes.CurrentVersion, Is.GreaterThan(before),
                    "Post-bake secret authoring changed authoritative voxels but did not advance the world change feed.");
                Assert.That(changes, Is.Not.Empty,
                    "Derived consumers must receive a post-authoring resident-state publication.");
                Assert.That(cursor, Is.EqualTo(world.Changes.CurrentVersion));
            });
        }
    }
}
