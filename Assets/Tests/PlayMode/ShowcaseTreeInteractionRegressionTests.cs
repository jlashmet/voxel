using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseTreeInteractionRegressionTests
    {
        [TearDown]
        public void TearDown()
        {
            VegetationComposition.ReplaceTreeWorld(Array.Empty<TreeInstance>());
        }

        [Test]
        public void RepresentativeTreeBlocksPlayerSizedWoodAabb()
        {
            PublishRepresentativeOak();

            ITreeWorldReadSource read = TreeWorldReadRegistry.Current;
            TreeSkeletonSnapshot skeleton = read.SkeletonFor(0);
            Assert.That(skeleton, Is.Not.Null);

            TreeBranchSegment trunk = skeleton.Branches
                .Where(branch => branch.Level == 0)
                .OrderBy(branch => (branch.Start.y + branch.End.y) * 0.5f)
                .First();
            float3 root = read.Instances[0].PositionMetres;
            float3 midpoint = root + (trunk.Start + trunk.End) * 0.5f;
            float3 halfExtents = new(0.30f, 0.90f, 0.30f);

            bool blocked = VegetationComposition.TreeDamage.OverlapsWoodAabb(
                midpoint - halfExtents, midpoint + halfExtents);

            Assert.That(blocked, Is.True,
                "A player-sized box through authored tree wood must be blocked.");
        }

        [Test]
        public void RepresentativeTreeShotCutsAndMarksDamage()
        {
            PublishRepresentativeOak();

            ITreeWorldReadSource read = TreeWorldReadRegistry.Current;
            TreeSkeletonSnapshot skeleton = read.SkeletonFor(0);
            Assert.That(skeleton, Is.Not.Null);

            TreeBranchSegment lowerTrunk = skeleton.Branches
                .Where(branch => branch.Level == 0)
                .OrderBy(branch => (branch.Start.y + branch.End.y) * 0.5f)
                .First();
            float3 root = read.Instances[0].PositionMetres;
            float3 impact = root + (lowerTrunk.Start + lowerTrunk.End) * 0.5f;

            VegetationComposition.TreeDamage.ApplyBlast(
                impact, 0.25f, new float3(1f, 0.15f, 0f));

            Assert.That(read.RemovedBranches(0).Count, Is.GreaterThan(0),
                "A direct lower-trunk shot must remove semantic branch geometry.");
            Assert.That(read.Damage[0].Severed, Is.True,
                "A direct lower-trunk shot must mark the tree severed so presentation reacts.");
        }

        [Test]
        public void ShowcaseMotorAndProjectileUseSemanticTreeInteractionPath()
        {
            string motor = File.ReadAllText("Assets/Scenes/Showcase/CharacterMotor.cs");
            string showcase = File.ReadAllText("Assets/Scenes/Showcase/VoxelShowcase.cs");

            StringAssert.Contains("VegetationComposition.TreeDamage.OverlapsWoodAabb(", motor,
                "Character movement must query surviving semantic tree wood after voxel collision.");
            StringAssert.DoesNotContain("VegetationComposition.TreeDamage.TrySweepImpact(", motor,
                "Character movement must not reuse the leaf-sensitive projectile sweep.");
            StringAssert.Contains("VegetationComposition.TreeDamage.TrySweepImpact(", showcase,
                "Showcase projectiles must test semantic trees instead of voxel storage only.");
            StringAssert.Contains("VegetationComposition.TreeDamage.ApplyBlast(", showcase,
                "A semantic tree projectile hit must reach branch damage/destruction.");
        }

        private static void PublishRepresentativeOak()
        {
            VegetationComposition.ReplaceTreeWorld(new[]
            {
                new TreeInstance
                {
                    PositionMetres = new float3(10f, 20f, 30f),
                    Species = TreeSpecies.Oak,
                    Seed = 0xA61E5EEDu,
                    Scale = 1f,
                },
            });
        }
    }
}
