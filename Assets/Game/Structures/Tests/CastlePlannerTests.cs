using System;
using System.Linq;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class CastlePlannerTests
    {
        [Test]
        public void SameSeedAndCentre_ProduceIdenticalPlan()
        {
            int3 centre = new(256, 220, 376);
            CastlePlan a = CastlePlanner.Plan(centre, 0x5EED1234u);
            CastlePlan b = CastlePlanner.Plan(centre, 0x5EED1234u);

            AssertPlanEqual(in a, in b);
        }

        [Test]
        public void Plan_StaysInsideAuthoredCastleFamilyRanges()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(256, 220, 376), 0x5EED1234u);

            Assert.That(plan.BaileyHalfX, Is.InRange(220, 279));
            Assert.That(plan.BaileyHalfZ, Is.InRange(220, 279));
            Assert.That(plan.PlateauHeight, Is.InRange(26, 43));
            Assert.That(plan.CliffDrop, Is.InRange(26, 43));
            Assert.That(plan.WallHeight, Is.InRange(82, 107));
            Assert.That(plan.WallThickness, Is.InRange(18, 24));
            Assert.That(plan.TowerRadius, Is.InRange(30, 38));
            Assert.That(plan.TowerHeight, Is.InRange(125, 159));
            Assert.That(plan.GateTowerRadius, Is.InRange(28, 35));
            Assert.That(plan.GateTowerHeight, Is.InRange(135, 171));
            Assert.That(plan.KeepHalfX, Is.InRange(92, 120));
            Assert.That(plan.KeepHalfZ, Is.InRange(78, 100));
            Assert.That(plan.FloorHeight, Is.EqualTo(46));
            Assert.That(plan.Floors, Is.InRange(5, 6));
            Assert.That(plan.KeepHeight, Is.EqualTo(plan.Floors * plan.FloorHeight));

            double cornerRadius = math.sqrt(
                plan.BaileyHalfX * (double)plan.BaileyHalfX
                + plan.BaileyHalfZ * (double)plan.BaileyHalfZ);
            Assert.That(plan.PlateauRadius, Is.GreaterThan(cornerRadius),
                "The authored crag must contain the rectangular bailey corners.");
        }

        [Test]
        public void LayoutHelpers_DeriveLandmarkCoordinatesFromPlanOnly()
        {
            var plan = new CastlePlan
            {
                Centre = new int3(300, 200, 500),
                PlateauHeight = 40,
                BaileyHalfX = 250,
                BaileyHalfZ = 240,
                WallThickness = 20,
                TowerRadius = 34,
                KeepHalfX = 100,
                KeepHalfZ = 90,
            };

            int3 gate = CastleLayout.FrontGateMinimum(in plan);
            Assert.That(gate.x, Is.EqualTo(300 - CastleLayout.FrontGateWidth / 2));
            Assert.That(gate.y, Is.EqualTo(241));
            Assert.That(gate.z, Is.EqualTo(500 - 240 - 20 + 2));

            int streamX = CastleLayout.WaterfallStreamX(in plan);
            Assert.That(streamX, Is.EqualTo(300 + 250 + 34 + 36));
            Assert.That(CastleLayout.WaterfallLipZ(in plan),
                Is.EqualTo(CastleLayout.LowerRiverZAt(in plan, streamX) + 68));

            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            Assert.That(trapdoor.x, Is.EqualTo(plan.Centre.x));
            Assert.That(trapdoor.y, Is.EqualTo(plan.Centre.y + plan.PlateauHeight));
        }

        [Test]
        public void EstimateWrites_GrowsWithLargerCastleDimensions()
        {
            CastlePlan small = CastlePlanner.Plan(int3.zero, 7u);
            CastlePlan large = small;
            large.PlateauRadius += 50;
            large.BaileyHalfX += 50;
            large.BaileyHalfZ += 50;
            large.TowerRadius += 10;
            large.KeepHalfX += 20;
            large.KeepHalfZ += 20;
            large.Floors += 1;

            Assert.That(CastlePlanner.EstimateWrites(in large),
                Is.GreaterThan(CastlePlanner.EstimateWrites(in small)));
        }

        [Test]
        public void GameStructuresAssemblies_DoNotReferenceVoxelEngineRuntimeAssemblies()
        {
            AssertNoEngineRuntimeDependencies(typeof(CastlePlan).Assembly);
            AssertNoEngineRuntimeDependencies(typeof(CastlePlanner).Assembly);
            AssertNoEngineRuntimeDependencies(typeof(CastleSiteAuthoring).Assembly);
        }

        private static void AssertNoEngineRuntimeDependencies(System.Reflection.Assembly assembly)
        {
            string[] violations = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name != null
                    && name.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                    && name.EndsWith(".Runtime", StringComparison.Ordinal))
                .ToArray();

            Assert.That(violations, Is.Empty,
                $"{assembly.GetName().Name} must consume VoxelEngine APIs only: "
                + string.Join(", ", violations));
        }

        private static void AssertPlanEqual(in CastlePlan a, in CastlePlan b)
        {
            Assert.That(a.Centre, Is.EqualTo(b.Centre));
            Assert.That(a.PlateauRadius, Is.EqualTo(b.PlateauRadius));
            Assert.That(a.PlateauHeight, Is.EqualTo(b.PlateauHeight));
            Assert.That(a.CliffDrop, Is.EqualTo(b.CliffDrop));
            Assert.That(a.BaileyHalfX, Is.EqualTo(b.BaileyHalfX));
            Assert.That(a.BaileyHalfZ, Is.EqualTo(b.BaileyHalfZ));
            Assert.That(a.WallHeight, Is.EqualTo(b.WallHeight));
            Assert.That(a.WallThickness, Is.EqualTo(b.WallThickness));
            Assert.That(a.TowerRadius, Is.EqualTo(b.TowerRadius));
            Assert.That(a.TowerHeight, Is.EqualTo(b.TowerHeight));
            Assert.That(a.GateTowerRadius, Is.EqualTo(b.GateTowerRadius));
            Assert.That(a.GateTowerHeight, Is.EqualTo(b.GateTowerHeight));
            Assert.That(a.KeepHalfX, Is.EqualTo(b.KeepHalfX));
            Assert.That(a.KeepHalfZ, Is.EqualTo(b.KeepHalfZ));
            Assert.That(a.KeepHeight, Is.EqualTo(b.KeepHeight));
            Assert.That(a.FloorHeight, Is.EqualTo(b.FloorHeight));
            Assert.That(a.Floors, Is.EqualTo(b.Floors));
            Assert.That(a.Seed, Is.EqualTo(b.Seed));
        }
    }
}
