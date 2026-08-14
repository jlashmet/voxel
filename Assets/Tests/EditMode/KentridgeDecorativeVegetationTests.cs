using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeDecorativeVegetationTests
    {
        [Test]
        public void SurfaceSamples_IncludeGroundAndWallAttachments()
        {
            const uint seed = 1337u;
            SettlementPlan plan = KentridgeDefinition.Build(seed);

            List<VegetationSurfaceSample> samples =
                KentridgeDecorativeVegetationPlanner.BuildSurfaceSamples(plan, seed);

            Assert.That(samples.Count, Is.GreaterThan(plan.Plots.Count));
            Assert.That(samples.Exists(s => s.Surface == VegetationSurface.Ground), Is.True);
            Assert.That(samples.Exists(s => s.Surface == VegetationSurface.Masonry
                                         || s.Surface == VegetationSurface.Wood), Is.True);
            Assert.That(samples.Exists(s => s.Surface != VegetationSurface.Ground
                                         && System.Math.Abs(s.Normal.y) < 0.01f), Is.True);
        }

        [Test]
        public void BuildAnalytic_IsDeterministicAndProducesMultipleVegetationClasses()
        {
            const uint seed = 99173u;

            List<VegetationInstance> a =
                KentridgeDecorativeVegetationPlanner.BuildAnalytic(seed, density: 1f);
            List<VegetationInstance> b =
                KentridgeDecorativeVegetationPlanner.BuildAnalytic(seed, density: 1f);

            Assert.That(a.Count, Is.GreaterThan(8));
            Assert.That(b.Count, Is.EqualTo(a.Count));

            var kinds = new HashSet<VegetationKind>();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Seed, Is.EqualTo(a[i].Seed));
                Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                Assert.That(b[i].PositionMetres.x, Is.EqualTo(a[i].PositionMetres.x).Within(0.0001f));
                Assert.That(b[i].PositionMetres.y, Is.EqualTo(a[i].PositionMetres.y).Within(0.0001f));
                Assert.That(b[i].PositionMetres.z, Is.EqualTo(a[i].PositionMetres.z).Within(0.0001f));
                kinds.Add(a[i].Kind);
            }

            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(kinds.Contains(VegetationKind.Moss) || kinds.Contains(VegetationKind.Vine), Is.True);
        }
    }
}
