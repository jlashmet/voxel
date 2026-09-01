using System.Collections.Generic;
using Game.Structures.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleVisibilityManifestTests
    {
        [Test]
        public void Register_MakesPlannedCastleQueryableWithoutVoxelRealization()
        {
            var visibility = new ShowcaseCastleVisibilityManifest();
            VoxelEngine.Showcase.CastlePlan plan = Plan();

            StructureFarPresentation registered = visibility.Register(in plan);
            IReadOnlyList<StructureFarPresentation> queried = visibility.Source.Query(
                new WorldVisibilityBoundsDm(
                    registered.FootprintMinDm.X - 1,
                    registered.FootprintMinDm.Y - 1,
                    registered.FootprintMaxDm.X + 1,
                    registered.FootprintMaxDm.Y + 1));

            Assert.That(visibility.Count, Is.EqualTo(1));
            Assert.That(queried.Count, Is.EqualTo(1));
            Assert.That(queried[0], Is.EqualTo(registered));
            Assert.That(queried[0].VisibilityClass, Is.EqualTo(StructureVisibilityClass.HorizonLandmark));
        }

        [Test]
        public void Register_ChangedPlanReplacesStableCastleInsteadOfDuplicating()
        {
            var visibility = new ShowcaseCastleVisibilityManifest();
            VoxelEngine.Showcase.CastlePlan firstPlan = Plan();
            VoxelEngine.Showcase.CastlePlan changedPlan = Plan();
            changedPlan.KeepHeight += 20;

            StructureFarPresentation first = visibility.Register(in firstPlan);
            StructureFarPresentation changed = visibility.Register(in changedPlan);

            Assert.That(visibility.Count, Is.EqualTo(1));
            Assert.That(changed.StructureKey, Is.EqualTo(first.StructureKey));
            Assert.That(changed.Revision, Is.Not.EqualTo(first.Revision));
            Assert.That(visibility.TryGetCastle(out StructureFarPresentation current), Is.True);
            Assert.That(current, Is.EqualTo(changed));
        }

        private static VoxelEngine.Showcase.CastlePlan Plan() => new VoxelEngine.Showcase.CastlePlan
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
