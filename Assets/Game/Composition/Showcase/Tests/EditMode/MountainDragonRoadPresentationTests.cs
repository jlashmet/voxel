using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonRoadPresentationTests
    {
        private const uint Seed = 0x5EED1234u;
        private const int MaximumAdjacentWallDm = 30;

        [Test]
        public void AuthoredMountainUsesBroadMassesInsteadOfCliffLikeRoadsideSpikes()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);

            Assert.That(surface.MassCount, Is.GreaterThanOrEqualTo(3),
                "The landmark must remain a composed natural massif rather than a single primitive cone.");
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                int run = mass.BaseRadiusDm - mass.TopRadiusDm;
                Assert.That(run, Is.GreaterThan(0), $"mountain mass {i} has a vertical side");
                long slopePermille = ((long)mass.HeightDm * 1000L + run / 2L) / run;
                Assert.That(slopePermille, Is.LessThanOrEqualTo(900),
                    $"mountain mass {i} is steep enough to recreate the rejected wall-like road views");
            }
        }

        [Test]
        public void ResolvedSpiralDoesNotRunBesideThreeMetreCutWalls()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(network.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);

            int edgeOffsetDm = ShowcaseMountainDragonLayout.PathWidth / 2 + route.ClearanceWidthDm;
            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                ResolvedWorldRoadPoint before = route.Road.Points[Math.Max(0, i - 1)];
                ResolvedWorldRoadPoint after = route.Road.Points[Math.Min(route.Road.Points.Count - 1, i + 1)];
                long tangentX = (long)after.Xdm - before.Xdm;
                long tangentZ = (long)after.Zdm - before.Zdm;
                double length = Math.Sqrt(tangentX * tangentX + tangentZ * tangentZ);
                if (length < 1.0) continue;

                int sideX = (int)Math.Round(-tangentZ * edgeOffsetDm / length);
                int sideZ = (int)Math.Round(tangentX * edgeOffsetDm / length);
                AssertWallBound(surface, point, point.Xdm + sideX, point.Zdm + sideZ, i, "left");
                AssertWallBound(surface, point, point.Xdm - sideX, point.Zdm - sideZ, i, "right");
            }
        }

        private static void AssertWallBound(
            MountainLandformSurface surface,
            ResolvedWorldRoadPoint road,
            int sampleX,
            int sampleZ,
            int pointIndex,
            string side)
        {
            int adjacentSurface = surface.HeightAtDm(sampleX, sampleZ);
            Assert.That(
                adjacentSurface - road.Ydm,
                Is.LessThanOrEqualTo(MaximumAdjacentWallDm),
                $"resolved point {pointIndex} {side} side rises {adjacentSurface - road.Ydm}dm above road at the clearance edge");
        }
    }
}
