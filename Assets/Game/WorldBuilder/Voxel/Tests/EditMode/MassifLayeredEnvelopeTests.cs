using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.WorldBuilder.Voxel.Tests.EditMode
{
    public sealed class MassifLayeredEnvelopeTests
    {
        [Test]
        public void BroadMassifCoreUsesContinuousProgressivelySteeperBands()
        {
            var spec = new MountainLandformSpec(
                originXdm: 120,
                originYdm: 24,
                originZdm: -80,
                radiusXdm: 420,
                radiusZdm: 390,
                heightDm: 240,
                summitRadiusDm: 90,
                macroShape: MountainMacroShape.Massif,
                summitCharacter: MountainSummitCharacter.Broad,
                seed: 0xA11CEu,
                ridgeCount: 0,
                ridgeStrengthPermille: 0,
                asymmetryXPermille: 0,
                asymmetryZPermille: 0,
                roughnessAmplitudeDm: 0,
                roughnessScaleDm: 70,
                erosionStrengthPermille: 700);

            var surface = new MountainLandformSurface(in spec);
            Assert.That(surface.MassCount, Is.EqualTo(4),
                "a plain broad massif should realize its core as the reusable four-band envelope without aspect shoulders or relief masses");

            int previousSlopePermille = -1;
            MountainLandformMass previous = default;
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass band = surface.GetMass(i);
                Assert.That(band.CentreXdm, Is.EqualTo(spec.OriginXdm));
                Assert.That(band.CentreZdm, Is.EqualTo(spec.OriginZdm));
                Assert.That(band.BaseRadiusDm, Is.GreaterThan(band.TopRadiusDm));

                int radialRun = band.BaseRadiusDm - band.TopRadiusDm;
                int verticalRise = band.HeightDm - 1;
                int slopePermille = (verticalRise * 1000 + radialRun / 2) / radialRun;
                Assert.That(slopePermille, Is.GreaterThan(previousSlopePermille),
                    $"massif band {i} should steepen inward instead of repeating one giant planar slope");

                if (i == 0)
                {
                    Assert.That(band.BaseYdm, Is.EqualTo(spec.OriginYdm));
                }
                else
                {
                    Assert.That(band.BaseYdm, Is.EqualTo(previous.TopYdm),
                        $"massif band {i} must share the previous vertical seam exactly");
                    Assert.That(band.BaseRadiusDm, Is.EqualTo(previous.TopRadiusDm),
                        $"massif band {i} must share the previous radial seam exactly");
                    Assert.That(
                        surface.HeightAtDm(spec.OriginXdm + band.BaseRadiusDm, spec.OriginZdm),
                        Is.EqualTo(band.BaseYdm),
                        $"terrain consumer must observe the same continuous seam used by voxel realization at band {i}");
                }

                previousSlopePermille = slopePermille;
                previous = band;
            }

            Assert.That(previous.TopYdm, Is.EqualTo(spec.OriginYdm + spec.HeightDm - 1));
            Assert.That(previous.TopRadiusDm, Is.EqualTo(spec.SummitRadiusDm));

            IWorldRoadTerrain terrain = surface;
            int outer = terrain.HeightAtDm(spec.OriginXdm + 360, spec.OriginZdm);
            int middle = terrain.HeightAtDm(spec.OriginXdm + 240, spec.OriginZdm);
            int upper = terrain.HeightAtDm(spec.OriginXdm + 150, spec.OriginZdm);
            Assert.That(middle, Is.GreaterThan(outer));
            Assert.That(upper, Is.GreaterThan(middle));
        }
    }
}
