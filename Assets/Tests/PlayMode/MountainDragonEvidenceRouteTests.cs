using System;
using System.IO;
using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonEvidenceRouteTests
    {
        private const uint Seed = 0x5EED1234;
        private const string RouteLogPrefix = "MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=";
        private const string EvidenceRouteRelativePath =
            "SceneIssues/open/20260828-180417-000-VoxelShowcaseMountainDragonCutscene/mountain-dragon-evidence-route.json";

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

        [Test]
        public void EvidenceCaptureWaypointsStayOnResolvedProductionRoad()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);

            string routePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                EvidenceRouteRelativePath));
            Assert.That(File.Exists(routePath), Is.True, routePath);
            EvidenceRoute evidence = JsonUtility.FromJson<EvidenceRoute>(File.ReadAllText(routePath));
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.waypoints, Is.Not.Null);

            AssertCaptureMatchesResolvedPoint(evidence, route, "lower-turn", 31);
            AssertCaptureMatchesResolvedPoint(evidence, route, "mid-turn", 50);
            AssertCaptureMatchesResolvedPoint(evidence, route, "upper-turn", 74);
            AssertCaptureMatchesResolvedPoint(evidence, route, "summit-supported", 90);
            AssertCaptureMatchesResolvedPoint(evidence, route, "summit-proximity", 93);
        }

        private static void AssertCaptureMatchesResolvedPoint(
            EvidenceRoute evidence,
            WorldRoadNetworkRoute route,
            string waypointName,
            int resolvedIndex)
        {
            EvidenceWaypoint waypoint = Array.Find(
                evidence.waypoints,
                candidate => candidate != null && candidate.name == waypointName);
            Assert.That(waypoint, Is.Not.Null, $"Missing evidence waypoint '{waypointName}'.");
            Assert.That(route.Road.Points.Count, Is.GreaterThan(resolvedIndex));

            ResolvedWorldRoadPoint resolved = route.Road.Points[resolvedIndex];
            Assert.That(waypoint.x, Is.EqualTo(resolved.Xdm / 10f).Within(0.001f),
                $"Evidence waypoint '{waypointName}' drifted off resolved road point {resolvedIndex} X.");
            Assert.That(waypoint.z, Is.EqualTo(resolved.Zdm / 10f).Within(0.001f),
                $"Evidence waypoint '{waypointName}' drifted off resolved road point {resolvedIndex} Z.");
        }

        [Serializable]
        private sealed class EvidenceRoute
        {
            public EvidenceWaypoint[] waypoints;
        }

        [Serializable]
        private sealed class EvidenceWaypoint
        {
            public string name;
            public float x;
            public float z;
        }
    }
}
