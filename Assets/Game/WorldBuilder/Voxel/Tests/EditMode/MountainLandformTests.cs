using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel.Tests.EditMode
{
    public sealed class MountainLandformTests
    {
        [Test]
        public void SameSpecProducesSameMassesAndSurfaceSamples()
        {
            MountainLandformSpec spec = CreateMassifSpec(seed: 1729u);
            var first = new MountainLandformSurface(in spec);
            var second = new MountainLandformSurface(in spec);

            Assert.That(second.MassCount, Is.EqualTo(first.MassCount));
            for (int i = 0; i < first.MassCount; i++)
                AssertMassEqual(first.GetMass(i), second.GetMass(i), i);

            int[,] offsets =
            {
                { 0, 0 },
                { 80, 40 },
                { -130, 90 },
                { 210, -70 },
                { -250, -180 },
                { 330, 120 },
            };

            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                int x = spec.OriginXdm + offsets[i, 0];
                int z = spec.OriginZdm + offsets[i, 1];
                Assert.That(second.HeightAtDm(x, z), Is.EqualTo(first.HeightAtDm(x, z)),
                    $"surface sample {i} changed despite identical semantic input");
            }
        }

        [Test]
        public void SemanticShapeInputsProduceMateriallyDifferentMountainFamilies()
        {
            MountainLandformSpec massifSpec = CreateMassifSpec(seed: 41u);
            MountainLandformSpec ridgeSpec = CreateRidgedSpec(seed: 91u);
            var massif = new MountainLandformSurface(in massifSpec);
            var ridged = new MountainLandformSurface(in ridgeSpec);

            MountainLandformMass massifCore = massif.GetMass(0);
            MountainLandformMass ridgedCore = ridged.GetMass(0);

            Assert.That(massifCore.CentreXdm, Is.EqualTo(massifSpec.OriginXdm));
            Assert.That(massifCore.CentreZdm, Is.EqualTo(massifSpec.OriginZdm));
            Assert.That(ridgedCore.CentreXdm, Is.GreaterThan(ridgeSpec.OriginXdm));
            Assert.That(ridgedCore.CentreZdm, Is.LessThan(ridgeSpec.OriginZdm));
            Assert.That(massifCore.BaseRadiusDm, Is.GreaterThan(ridgedCore.BaseRadiusDm));
            Assert.That(ridged.MassCount, Is.GreaterThan(massif.MassCount));

            int massifTransect = SampleTransectChecksum(massif, massifSpec.OriginXdm, massifSpec.OriginZdm, 360);
            int ridgedTransect = SampleTransectChecksum(ridged, ridgeSpec.OriginXdm, ridgeSpec.OriginZdm, 360);
            Assert.That(ridgedTransect, Is.Not.EqualTo(massifTransect),
                "independent IWorldRoadTerrain consumer should observe a different physical mountain");
        }

        [Test]
        public void VoxelCatalogueCompilesExactSurfaceMassesWithinPrimitiveBudget()
        {
            MountainLandformSpec spec = CreateRidgedSpec(seed: 7331u);
            var surface = new MountainLandformSurface(in spec);

            Assert.That(surface.MassCount, Is.LessThanOrEqualTo(FeatureBudget.MaxPrimitivesPerInstance));

            FeatureCatalogue catalogue = WorldBuilderMountainLandformCatalogue.Build(
                surface,
                mountainMaterial: 1,
                allocator: Allocator.Temp);
            try
            {
                Assert.That(catalogue.Definitions[0].MaxPrimitives, Is.EqualTo(surface.MassCount));
                var placement = catalogue.ExplicitPlacements[0].Position;
                int pc = 0;

                for (int i = 0; i < surface.MassCount; i++)
                {
                    MountainLandformMass mass = surface.GetMass(i);
                    Assert.That((ShapeOp)catalogue.Program[pc], Is.EqualTo(ShapeOp.EmitFrustum));
                    Assert.That(catalogue.Program[pc + 1], Is.EqualTo(0));
                    Assert.That(placement.x + catalogue.Program[pc + 2], Is.EqualTo(mass.CentreXdm));
                    Assert.That(placement.y + catalogue.Program[pc + 3], Is.EqualTo(mass.BaseYdm));
                    Assert.That(placement.z + catalogue.Program[pc + 4], Is.EqualTo(mass.CentreZdm));
                    Assert.That(catalogue.Program[pc + 5], Is.EqualTo(mass.HeightDm));
                    Assert.That(catalogue.Program[pc + 6], Is.EqualTo(mass.BaseRadiusDm));
                    Assert.That(catalogue.Program[pc + 7], Is.EqualTo(mass.TopRadiusDm));
                    Assert.That(catalogue.Program[pc + 8], Is.EqualTo(1), "mountain masses must remain vertical");
                    Assert.That(catalogue.Program[pc + 9], Is.EqualTo(1), "caller material must be preserved");
                    Assert.That((PrimitiveMode)catalogue.Program[pc + 12], Is.EqualTo(PrimitiveMode.FillIfEmpty));
                    pc += ShapeOps.InstructionLength(ShapeOp.EmitFrustum);
                }

                Assert.That((ShapeOp)catalogue.Program[pc], Is.EqualTo(ShapeOp.End));
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static MountainLandformSpec CreateMassifSpec(uint seed) =>
            new MountainLandformSpec(
                originXdm: 120,
                originYdm: 24,
                originZdm: -80,
                radiusXdm: 420,
                radiusZdm: 360,
                heightDm: 240,
                summitRadiusDm: 90,
                macroShape: MountainMacroShape.Massif,
                summitCharacter: MountainSummitCharacter.Broad,
                seed: seed,
                ridgeCount: 3,
                ridgeStrengthPermille: 350,
                asymmetryXPermille: 0,
                asymmetryZPermille: 0,
                roughnessAmplitudeDm: 18,
                roughnessScaleDm: 70,
                erosionStrengthPermille: 700);

        private static MountainLandformSpec CreateRidgedSpec(uint seed) =>
            new MountainLandformSpec(
                originXdm: -200,
                originYdm: 35,
                originZdm: 160,
                radiusXdm: 280,
                radiusZdm: 500,
                heightDm: 330,
                summitRadiusDm: 45,
                macroShape: MountainMacroShape.Ridged,
                summitCharacter: MountainSummitCharacter.Craggy,
                seed: seed,
                ridgeCount: 7,
                ridgeStrengthPermille: 900,
                asymmetryXPermille: 300,
                asymmetryZPermille: -220,
                roughnessAmplitudeDm: 52,
                roughnessScaleDm: 45,
                erosionStrengthPermille: 450);

        private static int SampleTransectChecksum(
            IWorldRoadTerrain terrain,
            int centreXdm,
            int centreZdm,
            int radiusDm)
        {
            unchecked
            {
                int hash = 17;
                for (int x = centreXdm - radiusDm; x <= centreXdm + radiusDm; x += 30)
                {
                    hash = hash * 31 + terrain.HeightAtDm(x, centreZdm);
                    hash = hash * 31 + (int)terrain.FlagsAtDm(x, centreZdm);
                }
                return hash;
            }
        }

        private static void AssertMassEqual(
            MountainLandformMass expected,
            MountainLandformMass actual,
            int index)
        {
            Assert.That(actual.CentreXdm, Is.EqualTo(expected.CentreXdm), $"mass {index} centre x");
            Assert.That(actual.BaseYdm, Is.EqualTo(expected.BaseYdm), $"mass {index} base y");
            Assert.That(actual.CentreZdm, Is.EqualTo(expected.CentreZdm), $"mass {index} centre z");
            Assert.That(actual.HeightDm, Is.EqualTo(expected.HeightDm), $"mass {index} height");
            Assert.That(actual.BaseRadiusDm, Is.EqualTo(expected.BaseRadiusDm), $"mass {index} base radius");
            Assert.That(actual.TopRadiusDm, Is.EqualTo(expected.TopRadiusDm), $"mass {index} top radius");
        }
    }
}
