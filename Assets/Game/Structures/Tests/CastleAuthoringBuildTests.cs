using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleAuthoringBuildTests
    {
        [Test]
        public void Build_PreservesLegacyOuterAndKeepStageProgression()
        {
            var authoring = new NullAuthoringSession(int.MaxValue);
            CastlePlan plan = SmallPlan();
            var build = new CastleAuthoringBuild(authoring, in plan, 19u);

            Assert.That(build.StageNumber, Is.EqualTo(1));
            AdvancePastStage(build, 1);
            Assert.That(build.StageNumber, Is.EqualTo(2));

            Assert.That(build.Step(), Is.False);
            Assert.That(build.StageNumber, Is.EqualTo(3));
            Assert.That(build.Step(), Is.False);
            Assert.That(build.StageNumber, Is.EqualTo(4));
            Assert.That(build.Step(), Is.False);
            Assert.That(build.StageNumber, Is.EqualTo(5));
            Assert.That(build.Step(), Is.False);
            Assert.That(build.StageNumber, Is.EqualTo(6));

            // The keep remains outer stage 6 while its seven legacy semantic substeps run.
            for (int substep = 0; substep < 6; substep++)
            {
                Assert.That(build.Step(), Is.False);
                Assert.That(build.StageNumber, Is.EqualTo(6));
            }

            Assert.That(build.Step(), Is.False);
            Assert.That(build.StageNumber, Is.EqualTo(7));
            Assert.That(build.IsComplete, Is.False);
        }

        [Test]
        public void Constructor_RejectsPlanWhoseEstimatedWritesExceedSessionBudget()
        {
            CastlePlan plan = SmallPlan();
            var authoring = new NullAuthoringSession(1);

            Assert.Throws<System.InvalidOperationException>(() =>
                new CastleAuthoringBuild(authoring, in plan, 19u));
        }

        private static void AdvancePastStage(CastleAuthoringBuild build, int stage)
        {
            for (int attempt = 0; attempt < 1024 && build.StageNumber == stage; attempt++)
                build.Step();

            Assert.That(build.StageNumber, Is.GreaterThan(stage),
                $"Castle authoring did not advance beyond stage {stage}.");
        }

        private static CastlePlan SmallPlan() => new()
        {
            Centre = new int3(0, 40, 0),
            PlateauRadius = 2,
            PlateauHeight = 4,
            CliffDrop = 1,
            BaileyHalfX = 20,
            BaileyHalfZ = 20,
            WallHeight = 20,
            WallThickness = 4,
            TowerRadius = 8,
            TowerHeight = 24,
            GateTowerRadius = 8,
            GateTowerHeight = 24,
            KeepHalfX = 50,
            KeepHalfZ = 50,
            KeepHeight = 92,
            FloorHeight = 46,
            Floors = 2,
            Seed = 19u,
        };

        private sealed class NullAuthoringSession : IStructureAuthoringSession
        {
            public NullAuthoringSession(int writeBudget)
            {
                WriteBudget = writeBudget;
            }

            public bool BudgetExceeded => false;
            public int WriteBudget { get; }
            public long TotalVoxelsWritten => 0;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(
                int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(
                int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(
                int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(
                int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(
                int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(
                int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(
                int3 min, int width, int height, int depth,
                int depthAxis, byte material) { }
            public void Stairs(
                int3 min, int width, int steps, int rise, int run,
                int axis, byte material) { }
            public void SpiralStair(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(
                int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
