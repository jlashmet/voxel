using System.Text;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonEvidenceRouteTests
    {
        private const uint Seed = 0x5EED1234;
        private const string RouteLogPrefix = "MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=";

        [Test]
        public void ResolvedProductionRouteCanBeSerializedForEvidence()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);

            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(20));

            var serialized = new StringBuilder(route.Road.Points.Count * 20);
            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                if (i > 0) serialized.Append(';');
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                serialized.Append(point.Xdm)
                    .Append(',')
                    .Append(point.Ydm)
                    .Append(',')
                    .Append(point.Zdm);
            }

            Debug.Log(RouteLogPrefix + serialized);
        }
    }
}
