using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonSummitApproachRegressionTests
    {
        private const uint Seed = 0x5EED1234;

        [Test]
        public void SummitApproachEndsBesidePlaceholderWithSupportedCrestClearance()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(network.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);

            var controls = route.Road.Intent.ControlPoints;
            Assert.That(controls.Count, Is.EqualTo(27),
                "The summit approach must retain the inward transition plus a distinct arrival control.");

            MountainLandformMass summit = surface.GetMass(0);
            WorldRoadPlanPoint previous = controls[controls.Count - 3];
            WorldRoadPlanPoint transition = controls[controls.Count - 2];
            WorldRoadPlanPoint arrival = controls[controls.Count - 1];

            long previousRadiusSquared = RadiusSquared(previous, summit);
            long transitionRadiusSquared = RadiusSquared(transition, summit);
            long arrivalRadiusSquared = RadiusSquared(arrival, summit);
            Assert.That(transitionRadiusSquared, Is.LessThan(previousRadiusSquared),
                "The summit transition must continue inward from the spiral exit.");
            Assert.That(arrivalRadiusSquared, Is.LessThan(transitionRadiusSquared),
                "The arrival must continue onto the summit rather than stopping at the outer crest edge.");
            Assert.That(arrivalRadiusSquared, Is.GreaterThan(0),
                "The authored road must not terminate at the centre occupied by the dragon placeholder.");

            int dx = Math.Abs(arrival.Xdm - summit.CentreXdm);
            int dz = Math.Abs(arrival.Zdm - summit.CentreZdm);
            int placeholderHalf = ShowcaseMountainDragonLayout.PlaceholderSize / 2;
            int placeholderClearance = Math.Max(dx - placeholderHalf, dz - placeholderHalf);
            Assert.That(
                placeholderClearance,
                Is.GreaterThanOrEqualTo(ShowcaseMountainDragonLayout.PathWidth / 2),
                "The terminal authored control must leave player-scale clearance from the solid placeholder footprint.");

            int supportedRadius = ShowcaseMountainDragonLayout.SummitRadius
                - ShowcaseMountainDragonLayout.PathWidth / 2;
            Assert.That(
                arrivalRadiusSquared,
                Is.LessThanOrEqualTo((long)supportedRadius * supportedRadius),
                "The terminal road centreline plus half its width must remain on the broad summit crest.");
        }

        private static long RadiusSquared(WorldRoadPlanPoint point, MountainLandformMass summit)
        {
            long dx = point.Xdm - summit.CentreXdm;
            long dz = point.Zdm - summit.CentreZdm;
            return dx * dx + dz * dz;
        }
    }
}
