using NUnit.Framework;
using VoxelEngine.Composition.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarTerrainMaterialFamilyTests
    {
        [TestCase(-400)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(400)]
        public void AnalyticTerrain_UsesSameSurfaceFamilyAsNearTerrain(int offsetFromSplit)
        {
            const byte lowSurface = 7;
            const byte highSurface = 11;
            var nearMaterials = new TerrainMaterialSet(
                deep: 2,
                subsurface: 3,
                lowSurface: lowSurface,
                surface: highSurface);
            ShowcaseMaterialSet farMaterials = CreateShowcaseMaterials(lowSurface, highSurface);
            int height = ShowcaseWorld.BaseHeightVoxels + offsetFromSplit;

            byte nearFamily = nearMaterials.SurfaceAt(height, ShowcaseWorld.BaseHeightVoxels);
            byte farFamily = VoxelFarTerrain.ResolveFarSurfaceMaterial(
                farMaterials,
                isStructure: false,
                hasAuthoredTerrain: false,
                authoredTerrainMaterial: 0,
                height: height);

            Assert.That(farFamily, Is.EqualTo(nearFamily));
        }

        [Test]
        public void AnalyticTerrain_SurfaceFamilyDoesNotDependOnCameraOrRingState()
        {
            const byte lowSurface = 5;
            const byte highSurface = 9;
            ShowcaseMaterialSet materials = CreateShowcaseMaterials(lowSurface, highSurface);
            int fixedWorldHeight = ShowcaseWorld.BaseHeightVoxels + 37;

            // The far material resolver receives only deterministic world/material facts. Camera
            // position and clipmap ring are deliberately absent from the contract, so the same
            // world sample cannot change family when clipmap ownership moves around it.
            byte first = VoxelFarTerrain.ResolveFarSurfaceMaterial(
                materials, false, false, 0, fixedWorldHeight);
            byte second = VoxelFarTerrain.ResolveFarSurfaceMaterial(
                materials, false, false, 0, fixedWorldHeight);

            Assert.That(first, Is.EqualTo(highSurface));
            Assert.That(second, Is.EqualTo(first));
        }

        private static ShowcaseMaterialSet CreateShowcaseMaterials(byte lowSurface, byte highSurface)
        {
            return new ShowcaseMaterialSet(
                terrainDeep: 2,
                terrainSubsurface: 3,
                terrainLowSurface: lowSurface,
                terrainHighSurface: highSurface,
                gate: 4,
                referenceArch: 6,
                farStructure: 8,
                worldgenFoundation: 8,
                worldgenMasonry: 8,
                worldgenDarkMasonry: 10,
                worldgenTimber: 4,
                worldgenGlass: 12,
                worldgenWarmWindow: 13,
                worldgenRoofTile: 14,
                worldgenSlate: 15,
                worldgenCloth: 16,
                worldgenMoss: 17,
                worldgenWater: 18,
                worldgenRoadSurface: 3,
                structuralMask: 0u);
        }
    }
}
