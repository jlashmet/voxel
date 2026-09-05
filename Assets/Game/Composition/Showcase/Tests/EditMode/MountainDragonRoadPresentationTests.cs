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
        private const int AuthoredClearanceWidthDm = 10;
        private const int CorridorClearAboveDm = 24;

        [Test]
        public void AuthoredMountainUsesContinuousSharedMassifEnvelopeWithoutAspectShoulders()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);

            Assert.That(surface.MassCount, Is.GreaterThanOrEqualTo(3),
                "the Mountain Dragon landmark must use the shared layered massif envelope instead of one giant full-height planar frustum");

            MountainLandformMass previous = default;
            int previousSlopePermille = -1;
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                Assert.That(mass.CentreXdm, Is.EqualTo(surface.Spec.OriginXdm),
                    "the Mountain Dragon policy must stay below the shared aspect-shoulder threshold so the landmark does not regress to offset giant lobes");
                Assert.That(mass.CentreZdm, Is.EqualTo(surface.Spec.OriginZdm));
                Assert.That(mass.BaseRadiusDm, Is.GreaterThan(mass.TopRadiusDm));

                int run = mass.BaseRadiusDm - mass.TopRadiusDm;
                int rise = mass.HeightDm - 1;
                int slopePermille = (rise * 1000 + run / 2) / run;
                Assert.That(slopePermille, Is.GreaterThan(previousSlopePermille),
                    "the shared massif profile must steepen inward instead of exposing one constant-slope faceted wall");

                if (i > 0)
                {
                    Assert.That(mass.BaseYdm, Is.EqualTo(previous.TopYdm),
                        "adjacent massif bands must share their vertical seam without a terrace or unsupported gap");
                    Assert.That(mass.BaseRadiusDm, Is.EqualTo(previous.TopRadiusDm),
                        "adjacent massif bands must share their radial seam without a terrace or unsupported gap");
                }

                previous = mass;
                previousSlopePermille = slopePermille;
            }

            Assert.That(previous.TopYdm,
                Is.EqualTo(surface.Spec.OriginYdm + surface.Spec.HeightDm - 1));
            Assert.That(previous.TopRadiusDm, Is.EqualTo(ShowcaseMountainDragonLayout.SummitRadius));
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

            int edgeOffsetDm = ShowcaseMountainDragonLayout.PathWidth / 2 + AuthoredClearanceWidthDm;
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

        [Test]
        public void ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(network.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);

            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                int authoredSurface = surface.HeightAtDm(point.Xdm, point.Zdm);
                int cutDm = authoredSurface - point.Ydm;
                Assert.That(cutDm, Is.LessThanOrEqualTo(CorridorClearAboveDm),
                    $"resolved point {i} cuts {cutDm}dm below the authored mountain surface, deeper than the {CorridorClearAboveDm}dm clear-above volume; this leaves mountain voxels overhead and turns the open ascent into a tunnel");
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
