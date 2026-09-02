using NUnit.Framework;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Covers the mountain relief added to the terrain sampler: that spawn stays walkable, that
    /// the range actually reaches mountain scale, and that the sampler remains deterministic and
    /// integer-only under amplitudes three orders of magnitude larger than before.
    ///
    /// The amplitude increase is the risk here. <c>Octave</c> multiplies a 10-bit noise value by
    /// the amplitude before shifting, so a large enough amplitude silently overflows a signed
    /// 32-bit multiply and wraps — producing terrain that looks plausible in places and inverts
    /// in others. These tests pin the range rather than trusting it.
    /// </summary>
    public sealed class MountainTerrainTests
    {
        private const uint Seed = 12345u;

        [Test]
        public void SpawnSitsInAWalkableBasin()
        {
            // The whole point of the radial mask: whatever the noise does elsewhere, the origin
            // must stay low enough to walk on and build from.
            for (int z = -2000; z <= 2000; z += 500)
            for (int x = -2000; x <= 2000; x += 500)
            {
                int h = TerrainSampler.HeightAt(x, z, Seed);
                Assert.Less(h, 1500,
                    $"Spawn basin column ({x},{z}) rose to {h} voxels ({h * 0.1f} m); the "
                  + "mask should hold mountain relief out of the starting area.");
            }
        }

        [Test]
        public void MountainMaskIsZeroInsideTheValleyAndFullBeyondTheRange()
        {
            Assert.AreEqual(0, TerrainSampler.MountainMask(0, 0));
            Assert.AreEqual(0, TerrainSampler.MountainMask(TerrainSampler.ValleyRadius - 100, 0));
            Assert.AreEqual(1024,
                TerrainSampler.MountainMask(TerrainSampler.MountainFullRadius + 100, 0));
        }

        [Test]
        public void MountainMaskRisesMonotonicallyWithDistance()
        {
            int previous = -1;
            for (int d = 0; d <= TerrainSampler.MountainFullRadius + 5000; d += 500)
            {
                int mask = TerrainSampler.MountainMask(d, 0);
                Assert.GreaterOrEqual(mask, previous,
                    $"Mask fell back at {d} voxels; the ramp must not reverse or foothills "
                  + "would form terraces.");
                Assert.That(mask, Is.InRange(0, 1024));
                previous = mask;
            }
        }

        [Test]
        public void MountainMaskIsRadiallySymmetric()
        {
            int reference = TerrainSampler.MountainMask(20_000, 0);
            Assert.AreEqual(reference, TerrainSampler.MountainMask(-20_000, 0));
            Assert.AreEqual(reference, TerrainSampler.MountainMask(0, 20_000));
            Assert.AreEqual(reference, TerrainSampler.MountainMask(0, -20_000));
        }

        [Test]
        public void DistantTerrainReachesMountainScale()
        {
            // Sweep the far field and confirm the range genuinely produces peaks, not just a
            // raised plateau. Without this, an amplitude that silently clamps would pass every
            // other test here.
            // Sample an area, not a line. The massif octave has a 13 km wavelength, so a thin
            // diagonal transect can cross less than one noise cell and miss every summit.
            int highest = 0;
            for (int z = -150_000; z <= 150_000; z += 4_099)
            for (int x = -150_000; x <= 150_000; x += 4_099)
            {
                if (TerrainSampler.MountainMask(x, z) < 1024) continue;
                int h = TerrainSampler.HeightAt(x, z, Seed);
                if (h > highest) highest = h;
            }

            Assert.Greater(highest, 15_000,
                $"Tallest distant peak found was {highest} voxels ({highest * 0.1f} m). "
              + "The range should reach kilometre scale.");

            // Clearance, not just inequality. Terrain that merely grazes the clamp still
            // shears whole summits flat; an earlier tuning saturated it on every bearing and
            // produced a 6 km wall 1.5 km from spawn rather than a distant range.
            Assert.Less(highest, (int)(TerrainSampler.MaxHeight * 0.9f),
                $"Tallest peak {highest} is within 10% of the {TerrainSampler.MaxHeight} clamp; "
              + "summits are being sheared into mesas at one altitude.");
        }

        [Test]
        public void TheRangeReadsAsDistantNotAsAWallAroundSpawn()
        {
            // The failure this replaces: every bearing hit the height clamp 1.5 km out,
            // subtending 76 degrees. That is an enclosure, not scenery. Mountains must sit far
            // enough away that they occupy a believable slice of the sky.
            const float eyeMetres = 24f;
            for (int degrees = 0; degrees < 360; degrees += 30)
            {
                double radians = degrees * System.Math.PI / 180.0;
                int tallest = 0, atDistance = 0;
                for (int d = 9_000; d < 120_000; d += 600)
                {
                    int x = (int)(System.Math.Cos(radians) * d);
                    int z = (int)(System.Math.Sin(radians) * d);
                    int h = TerrainSampler.HeightAt(x, z, Seed);
                    if (h <= tallest) continue;
                    tallest = h;
                    atDistance = d;
                }

                double elevation = System.Math.Atan2(tallest * 0.1f - eyeMetres,
                                                     atDistance * 0.1f) * 180.0 / System.Math.PI;
                Assert.Less(elevation, 45.0,
                    $"On bearing {degrees} the tallest terrain rises {elevation:0.0} degrees "
                  + $"above the eye ({tallest * 0.1f:0} m at {atDistance * 0.1f:0} m). That "
                  + "walls the player in rather than reading as a distant range.");
            }
        }

        [Test]
        public void SpawnHasOpenGroundBeforeTheFoothills()
        {
            // Mountains should be approached, not stepped into. The basin must stay open for
            // roughly its declared radius.
            for (int d = 0; d < TerrainSampler.ValleyRadius; d += 1_000)
            {
                int h = TerrainSampler.HeightAt(d, 0, Seed);
                Assert.Less(h, 2_000,
                    $"Ground {d * 0.1f:0} m from spawn is already {h * 0.1f:0} m up; the open "
                  + "basin should extend to about {TerrainSampler.ValleyRadius * 0.1f:0} m.");
            }
        }

        [Test]
        public void HeightStaysWithinDeclaredBounds()
        {
            // Overflow in the amplitude multiply would show up here as a height outside the
            // clamp, because the wrap happens before HeightAt clamps.
            for (int d = 0; d < 200_000; d += 1031)
            {
                int h = TerrainSampler.HeightAt(d, -d / 2, Seed);
                Assert.That(h, Is.InRange(TerrainSampler.MinHeight, TerrainSampler.MaxHeight),
                    $"Column at {d} produced {h}, outside [{TerrainSampler.MinHeight}, "
                  + $"{TerrainSampler.MaxHeight}].");
            }
        }

        [Test]
        public void SamplerRemainsDeterministic()
        {
            // Cross-client agreement depends on this exactly (Constitution I).
            for (int i = 0; i < 200; i++)
            {
                int x = i * 613 - 40_000;
                int z = i * -917 + 25_000;
                Assert.AreEqual(TerrainSampler.HeightAt(x, z, Seed),
                                TerrainSampler.HeightAt(x, z, Seed));
            }
        }

        [Test]
        public void TerrainIsContinuousAcrossAdjacentColumns()
        {
            // A discontinuity would be a cliff the mesher renders as a wall and the player
            // cannot climb. Large relief makes big steps legitimate, but not unbounded ones.
            int worst = 0;
            for (int d = 10_000; d < 80_000; d += 313)
            {
                int a = TerrainSampler.HeightAt(d, 1234, Seed);
                int b = TerrainSampler.HeightAt(d + 1, 1234, Seed);
                worst = System.Math.Max(worst, System.Math.Abs(a - b));
            }

            Assert.Less(worst, 200,
                $"Adjacent columns differed by {worst} voxels ({worst * 0.1f} m). A step that "
              + "large between neighbouring voxels indicates noise aliasing, not a cliff.");
        }

        [Test]
        public void SlopeAtAgreesWithHeightDifferences()
        {
            // SlopeAt drives placement rules; if it disagrees with HeightAt after the relief
            // change, structures get sited on cliff faces.
            for (int d = 15_000; d < 60_000; d += 4001)
            {
                int slope = TerrainSampler.SlopeAt(d, d / 4, Seed);
                int manual = System.Math.Max(
                    System.Math.Abs(TerrainSampler.HeightAt(d + 4, d / 4, Seed)
                                  - TerrainSampler.HeightAt(d - 4, d / 4, Seed)),
                    System.Math.Abs(TerrainSampler.HeightAt(d, d / 4 + 4, Seed)
                                  - TerrainSampler.HeightAt(d, d / 4 - 4, Seed)));
                Assert.AreEqual(manual, slope);
            }
        }
    }
}
