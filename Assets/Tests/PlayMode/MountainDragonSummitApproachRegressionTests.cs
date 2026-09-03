using Game.WorldBuilder.Api;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonSummitApproachRegressionTests
    {
        private const uint Seed = 0x5EED1234;

        [Test]
        public void SummitApproachKeepsInwardSpiralControlBeforeCentre()
        {
            var surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(network.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);

            var controls = route.Road.Intent.ControlPoints;
            Assert.That(controls.Count, Is.EqualTo(27),
                "The summit approach must retain one semantic inward-turn control before the centre leg.");

            WorldRoadPlanPoint previous = controls[controls.Count - 3];
            WorldRoadPlanPoint transition = controls[controls.Count - 2];
            WorldRoadPlanPoint centre = controls[controls.Count - 1];

            Assert.That(centre.Xdm, Is.EqualTo(ShowcaseMountainDragonLayout.CentreXdm));
            Assert.That(centre.Zdm, Is.EqualTo(ShowcaseMountainDragonLayout.CentreZdm));

            long previousDx = previous.Xdm - centre.Xdm;
            long previousDz = previous.Zdm - centre.Zdm;
            long transitionDx = transition.Xdm - centre.Xdm;
            long transitionDz = transition.Zdm - centre.Zdm;
            long previousRadiusSquared = previousDx * previousDx + previousDz * previousDz;
            long transitionRadiusSquared = transitionDx * transitionDx + transitionDz * transitionDz;

            Assert.That(transitionRadiusSquared, Is.LessThan(previousRadiusSquared),
                "The added semantic control must continue inward toward the summit.");
            Assert.That(transitionDx * previousDz - transitionDz * previousDx, Is.Not.EqualTo(0),
                "The summit transition must continue the spiral heading instead of jumping radially to centre.");
        }
    }
}
