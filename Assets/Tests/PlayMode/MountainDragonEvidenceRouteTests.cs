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
        public void EvidenceCaptureWaypointsFollowResolvedProductionRoadInSemanticOrder()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);

            EvidenceRoute evidence = LoadEvidenceRoute();
            string[] semanticCaptures =
            {
                "lower-turn",
                "mid-turn",
                "upper-turn",
                "summit-supported",
                "summit-proximity",
            };

            int previousIndex = -1;
            int previousYdm = int.MinValue;
            foreach (string waypointName in semanticCaptures)
            {
                EvidenceWaypoint waypoint = RequireWaypoint(evidence, waypointName);
                int resolvedIndex = FindClosestResolvedPoint(route, waypoint);
                ResolvedWorldRoadPoint resolved = route.Road.Points[resolvedIndex];

                Assert.That(resolvedIndex, Is.GreaterThan(previousIndex),
                    $"Evidence waypoint '{waypointName}' must follow the authoritative resolved road in semantic traversal order.");
                Assert.That(resolved.Ydm, Is.GreaterThanOrEqualTo(previousYdm),
                    $"Evidence waypoint '{waypointName}' must not regress below the preceding semantic ascent capture.");
                AssertWaypointMatchesResolvedPoint(waypoint, resolved, waypointName);

                previousIndex = resolvedIndex;
                previousYdm = resolved.Ydm;
            }

            EvidenceWaypoint summitProximity = RequireWaypoint(evidence, "summit-proximity");
            int summitIndex = FindClosestResolvedPoint(route, summitProximity);
            Assert.That(summitIndex, Is.EqualTo(route.Road.Points.Count - 1),
                "The summit-proximity capture must follow the semantic terminal road point, not a historical numeric index.");
        }

        private static EvidenceRoute LoadEvidenceRoute()
        {
            string routePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                EvidenceRouteRelativePath));
            Assert.That(File.Exists(routePath), Is.True, routePath);
            EvidenceRoute evidence = JsonUtility.FromJson<EvidenceRoute>(File.ReadAllText(routePath));
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.waypoints, Is.Not.Null);
            return evidence;
        }

        private static EvidenceWaypoint RequireWaypoint(EvidenceRoute evidence, string waypointName)
        {
            EvidenceWaypoint waypoint = Array.Find(
                evidence.waypoints,
                candidate => candidate != null && candidate.name == waypointName);
            Assert.That(waypoint, Is.Not.Null, $"Missing evidence waypoint '{waypointName}'.");
            return waypoint;
        }

        private static int FindClosestResolvedPoint(WorldRoadNetworkRoute route, EvidenceWaypoint waypoint)
        {
            int targetXdm = Mathf.RoundToInt(waypoint.x * 10f);
            int targetZdm = Mathf.RoundToInt(waypoint.z * 10f);
            int bestIndex = -1;
            long bestDistanceSquared = long.MaxValue;
            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                long dx = (long)point.Xdm - targetXdm;
                long dz = (long)point.Zdm - targetZdm;
                long distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                bestIndex = i;
            }

            Assert.That(bestIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(bestDistanceSquared, Is.LessThanOrEqualTo(1L),
                $"Evidence waypoint '{waypoint.name}' drifted off the authoritative resolved road.");
            return bestIndex;
        }

        private static void AssertWaypointMatchesResolvedPoint(
            EvidenceWaypoint waypoint,
            ResolvedWorldRoadPoint resolved,
            string waypointName)
        {
            Assert.That(waypoint.x, Is.EqualTo(resolved.Xdm / 10f).Within(0.001f),
                $"Evidence waypoint '{waypointName}' drifted off authoritative road X.");
            Assert.That(waypoint.z, Is.EqualTo(resolved.Zdm / 10f).Within(0.001f),
                $"Evidence waypoint '{waypointName}' drifted off authoritative road Z.");
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
