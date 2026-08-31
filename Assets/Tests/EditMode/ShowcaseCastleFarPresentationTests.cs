using Game.Structures.Api;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleFarPresentationTests
    {
        [Test]
        public void FromPlan_IsDeterministicAndHorizonVisibleBeforeVoxelRealization()
        {
            CastlePlan plan = Plan();

            StructureFarPresentation first = ShowcaseCastleFarPresentation.FromPlan(in plan);
            StructureFarPresentation second = ShowcaseCastleFarPresentation.FromPlan(in plan);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.VisibilityClass, Is.EqualTo(StructureVisibilityClass.HorizonLandmark));
            Assert.That(ShowcaseCastleFarPresentation.ProxyKey, Is.EqualTo("castle"));
            Assert.That(first.FootprintMinDm.X, Is.LessThan(plan.Centre.x));
            Assert.That(first.FootprintMaxDm.X, Is.GreaterThan(plan.Centre.x));
            Assert.That(first.FootprintMinDm.Y, Is.LessThan(plan.Centre.z));
            Assert.That(first.FootprintMaxDm.Y, Is.GreaterThan(plan.Centre.z));
        }

        [Test]
        public void FromPlan_RevisionChangesWhenSemanticCastleGeometryChanges()
        {
            CastlePlan firstPlan = Plan();
            CastlePlan secondPlan = Plan();
            secondPlan.KeepHeight += 12;

            StructureFarPresentation first = ShowcaseCastleFarPresentation.FromPlan(in firstPlan);
            StructureFarPresentation second = ShowcaseCastleFarPresentation.FromPlan(in secondPlan);

            Assert.That(second.StructureKey, Is.EqualTo(first.StructureKey),
                "geometry changes should replace the same semantic landmark rather than duplicate it");
            Assert.That(second.Revision, Is.Not.EqualTo(first.Revision));
            Assert.That(second.HeightDm, Is.GreaterThan(first.HeightDm));
        }

        private static CastlePlan Plan() => new CastlePlan
        {
            Centre = new int3(256, 220, 376),
            PlateauRadius = 180,
            PlateauHeight = 18,
            CliffDrop = 24,
            BaileyHalfX = 150,
            BaileyHalfZ = 120,
            WallHeight = 72,
            WallThickness = 12,
            TowerRadius = 24,
            TowerHeight = 112,
            GateTowerRadius = 28,
            GateTowerHeight = 124,
            KeepHalfX = 62,
            KeepHalfZ = 52,
            KeepHeight = 148,
            FloorHeight = 32,
            Floors = 4,
            Seed = 0x5EED1234u,
        };
    }
}
