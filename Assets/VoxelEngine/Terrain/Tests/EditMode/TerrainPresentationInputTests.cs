using System;
using NUnit.Framework;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Terrain.Tests.EditMode
{
    public sealed class TerrainPresentationInputTests
    {
        [Test]
        public void SurfaceFamilySplitIsDeterministicAcrossRepresentativeElevations()
        {
            var materials = new TerrainMaterialSet(
                deep: 1,
                subsurface: 2,
                lowSurface: 7,
                surface: 9);
            int split = TerrainQuery.BaseHeight;

            Assert.That(materials.SurfaceAt(split - 1, split), Is.EqualTo(7));
            Assert.That(materials.SurfaceAt(split, split), Is.EqualTo(9));
            Assert.That(materials.SurfaceAt(split + 5000, split), Is.EqualTo(9));
        }

        [TestCase(0, 0, 0x5EED1234u)]
        [TestCase(16000, -9000, 0x5EED1234u)]
        [TestCase(60000, 20000, 0x12345678u)]
        public void SlopeAtMatchesCanonicalHeightDifferences(int x, int z, uint seed)
        {
            int dx = Math.Abs(
                TerrainQuery.HeightAt(x + 4, z, seed)
                - TerrainQuery.HeightAt(x - 4, z, seed));
            int dz = Math.Abs(
                TerrainQuery.HeightAt(x, z + 4, seed)
                - TerrainQuery.HeightAt(x, z - 4, seed));

            Assert.That(TerrainQuery.SlopeAt(x, z, seed), Is.EqualTo(Math.Max(dx, dz)));
        }
    }
}
