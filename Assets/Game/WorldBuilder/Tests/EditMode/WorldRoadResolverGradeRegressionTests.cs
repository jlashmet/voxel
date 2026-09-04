using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldRoadResolverGradeRegressionTests
    {
        [Test]
        public void ThreeDecimetreSegmentAtTwoHundredEightyPermilleCannotRiseOneDecimetre()
        {
            WorldRoadProfile profile = Profile(maximumGradePermille: 280);
            var intent = new WorldRoadIntent(
                "short-grade-contract",
                "from",
                "to",
                0x47524144u,
                profile,
                "short-segment grade regression",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(3, 0),
                });

            ResolvedWorldRoad road = WorldRoadResolver.Resolve(
                intent,
                new StepTerrain(stepXdm: 3, upperHeightDm: 1),
                sampleSpacingDm: 3,
                searchMarginCells: 0);

            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, road.Status, road.FailureReason);
            Assert.AreEqual(2, road.Points.Count);
            Assert.AreEqual(0, road.Points[0].Ydm);
            Assert.AreEqual(0, road.Points[1].Ydm,
                "A 3dm run at 280 permille allows floor(3*280/1000)=0dm rise; grading must not invent a 1dm allowance that validation then rejects.");
            Assert.LessOrEqual(
                (long)System.Math.Abs(road.Points[1].Ydm - road.Points[0].Ydm) * 1000L,
                (long)profile.MaximumGradePermille * 3L);
        }

        [Test]
        public void FourDecimetreSegmentAtTwoHundredEightyPermilleMayRiseOneDecimetre()
        {
            WorldRoadProfile profile = Profile(maximumGradePermille: 280);
            var intent = new WorldRoadIntent(
                "short-grade-positive-control",
                "from",
                "to",
                0x47524144u,
                profile,
                "short-segment grade positive control",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(4, 0),
                });

            ResolvedWorldRoad road = WorldRoadResolver.Resolve(
                intent,
                new StepTerrain(stepXdm: 4, upperHeightDm: 1),
                sampleSpacingDm: 4,
                searchMarginCells: 0);

            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, road.Status, road.FailureReason);
            Assert.AreEqual(1, road.Points[1].Ydm,
                "A 4dm run at 280 permille permits a 1dm rise (250 permille); exact integer grading should retain valid terrain rather than over-flattening it.");
        }

        private static WorldRoadProfile Profile(int maximumGradePermille)
            => new WorldRoadProfile(
                "short-grade-profile",
                "road-surface",
                carriagewayWidthDm: 18,
                transitionWidthDm: 8,
                maximumGradePermille: maximumGradePermille,
                maximumCutFillDm: 1,
                edgeVariationDm: 0);

        private sealed class StepTerrain : IWorldRoadTerrain
        {
            private readonly int _stepXdm;
            private readonly int _upperHeightDm;

            public StepTerrain(int stepXdm, int upperHeightDm)
            {
                _stepXdm = stepXdm;
                _upperHeightDm = upperHeightDm;
            }

            public int HeightAtDm(int xdm, int zdm)
                => xdm >= _stepXdm ? _upperHeightDm : 0;

            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm)
                => WorldRoadTerrainFlags.None;
        }
    }
}
