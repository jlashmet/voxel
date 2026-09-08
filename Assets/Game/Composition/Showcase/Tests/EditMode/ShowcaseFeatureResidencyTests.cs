using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseFeatureResidencyTests
    {
        [TestCase(512, false)]
        [TestCase(513, true)]
        public void RuntimeQueueIncludesAuthoredUpperLayerOnlyWhenFootprintCrossesCeiling(
            int upperExclusive, bool needsUpperLayer)
        {
            using var world = new ShowcaseWorld(0x5EED1234u, 64, 1, 2);
            var catalogue = new FeatureCatalogue
            {
                Definitions = new NativeArray<FeatureDefinition>(1, Allocator.Persistent),
                Rules = new NativeArray<PlacementRule>(1, Allocator.Persistent),
                ExplicitPlacements = new NativeArray<ExplicitPlacement>(1, Allocator.Persistent)
            };
            catalogue.Definitions[0] = new FeatureDefinition
            {
                Footprint = new int3(16, upperExclusive - 500, 16)
            };
            catalogue.Rules[0] = new PlacementRule { DefinitionId = 0, ExplicitCount = 1 };
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(-16, 500, 0)
            };
            world.ConfigureGeneratedContentForGameplay(catalogue);
            typeof(ShowcaseWorld).GetMethod("RefreshPending", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(world, new object[] { new int3(0) });
            var pending = (List<int3>)typeof(ShowcaseWorld)
                .GetField("_pendingLoads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
            Assert.That(pending.Contains(new int3(-1, 1, 0)), Is.EqualTo(needsUpperLayer));
            Assert.That(pending.Contains(new int3(0, 1, 0)), Is.False,
                "An exclusive X boundary must not request a neighboring feature layer.");
            Assert.That(pending.Contains(new int3(-1, 0, 0)), Is.True,
                "Feature height must preserve the underlying terrain layer.");
        }

        [Test]
        public void PresentationColumnReadinessWaitsForPendingAuthoredFeaturePublication()
        {
            using var world = new ShowcaseWorld(0x5EED1234u, 64, 1, 2);
            const int regionX = 5;
            const int regionZ = 5;

            object[] spanArguments = { regionX, regionZ, 0, 0 };
            typeof(ShowcaseWorld)
                .GetMethod("SurfaceLayerSpan", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(world, spanArguments);
            int minLayer = (int)spanArguments[2];
            int maxLayer = (int)spanArguments[3];

            var generated = (HashSet<int3>)typeof(ShowcaseWorld)
                .GetField("_generated", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(world);
            for (int y = minLayer; y <= maxLayer; y++)
                generated.Add(new int3(regionX, y, regionZ));

            var checkedRegion = new int3(regionX, minLayer, regionZ);
            var point = new Vector3(
                regionX * ShowcaseWorld.RegionMetres + 1f,
                minLayer * ShowcaseWorld.RegionMetres + 1f,
                regionZ * ShowcaseWorld.RegionMetres + 1f);
            Assert.That(world.IsPresentationColumnContentSettled(point), Is.True,
                "A generated column with no pending authored work should be settled.");

            var pendingFeatures = (List<int3>)typeof(ShowcaseWorld)
                .GetField("_pendingFeatureRegions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(world);
            pendingFeatures.Add(checkedRegion);
            Assert.That(world.IsPresentationColumnContentSettled(point), Is.False,
                "Terrain residency must not race pending authored feature publication.");

            pendingFeatures.Remove(checkedRegion);
            Assert.That(world.IsPresentationColumnContentSettled(point), Is.True,
                "The same generated column should settle once authored feature publication is complete.");
        }
    }
}
