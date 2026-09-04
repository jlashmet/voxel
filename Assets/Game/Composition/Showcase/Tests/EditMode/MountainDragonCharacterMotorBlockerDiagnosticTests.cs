using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonCharacterMotorBlockerDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;

        [Test]
        public void SummitArrivalStaysOutsideDragonPlaceholderWhileRemainingSupportedOnCrest()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);

            MountainLandformMass summit = surface.GetMass(0);
            ResolvedWorldRoadPoint arrival = ShowcaseMountainDragonLayout.SummitApproach(ascent);
            int dx = Math.Abs(arrival.Xdm - summit.CentreXdm);
            int dz = Math.Abs(arrival.Zdm - summit.CentreZdm);
            int placeholderHalf = ShowcaseMountainDragonLayout.PlaceholderSize / 2;
            int placeholderClearance = Math.Max(dx - placeholderHalf, dz - placeholderHalf);

            Assert.That(
                placeholderClearance,
                Is.GreaterThanOrEqualTo(ShowcaseMountainDragonLayout.PathWidth / 2),
                "The production route must finish beside the solid dragon placeholder instead of driving the player capsule into it.");

            int supportedRadius = ShowcaseMountainDragonLayout.SummitRadius
                - ShowcaseMountainDragonLayout.PathWidth / 2;
            long radialDistanceSquared = (long)dx * dx + (long)dz * dz;
            Assert.That(
                radialDistanceSquared,
                Is.LessThanOrEqualTo((long)supportedRadius * supportedRadius),
                "The terminal road centreline plus half its width must remain on the broad summit crest.");
        }
    }
}
