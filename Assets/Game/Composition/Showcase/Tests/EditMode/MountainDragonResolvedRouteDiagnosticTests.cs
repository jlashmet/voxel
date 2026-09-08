using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonResolvedRouteDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;
        private const string RouteLogPrefix = "MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=";

        [Test]
        public void CurrentProductionRouteSerializesForEvidenceRefresh()
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

        [Test]
        public void CurrentProductionRouteEndpointsFollowSemanticMountainAnchors()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(1));

            ResolvedWorldRoadPoint entry = route.Road.Points[0];
            long entryDx = (long)entry.Xdm - ShowcaseMountainDragonLayout.EntryXdm;
            long entryDz = (long)entry.Zdm - ShowcaseMountainDragonLayout.EntryZdm;
            long entryTolerance = ShowcaseMountainDragonLayout.PathWidth;
            Assert.That(
                entryDx * entryDx + entryDz * entryDz,
                Is.LessThanOrEqualTo(entryTolerance * entryTolerance),
                "Resolved ascent must begin at the semantic mountain entry rather than a captured historical route index.");

            MountainLandformMass summit = surface.GetMass(0);
            ResolvedWorldRoadPoint arrival = ShowcaseMountainDragonLayout.SummitApproach(ascent);
            long summitDx = (long)arrival.Xdm - summit.CentreXdm;
            long summitDz = (long)arrival.Zdm - summit.CentreZdm;
            int supportedRadius = ShowcaseMountainDragonLayout.SummitRadius
                - ShowcaseMountainDragonLayout.PathWidth / 2;
            Assert.That(
                summitDx * summitDx + summitDz * summitDz,
                Is.LessThanOrEqualTo((long)supportedRadius * supportedRadius),
                "Resolved ascent must finish on the supported semantic summit crest.");
            Assert.That(arrival.Ydm, Is.GreaterThan(entry.Ydm),
                "The semantic summit arrival must remain above the semantic mountain entry.");
        }
    }
}
