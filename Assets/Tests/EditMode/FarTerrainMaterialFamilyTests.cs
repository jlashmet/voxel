using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
            int splitHeight = ShowcaseBaseHeight();
            int height = splitHeight + offsetFromSplit;

            byte nearFamily = nearMaterials.SurfaceAt(height, splitHeight);
            byte farFamily = ResolveFarSurface(lowSurface, highSurface, height);

            Assert.That(farFamily, Is.EqualTo(nearFamily));
        }

        [Test]
        public void AnalyticTerrain_SurfaceFamilyDoesNotDependOnCameraOrRingState()
        {
            const byte lowSurface = 5;
            const byte highSurface = 9;
            int fixedWorldHeight = ShowcaseBaseHeight() + 37;

            // ResolveFarSurfaceMaterial receives deterministic world/material facts only. Camera
            // position and clipmap ring are deliberately absent from its contract, so the same
            // world sample cannot change family when clipmap ownership moves around it.
            byte first = ResolveFarSurface(lowSurface, highSurface, fixedWorldHeight);
            byte second = ResolveFarSurface(lowSurface, highSurface, fixedWorldHeight);

            Assert.That(first, Is.EqualTo(highSurface));
            Assert.That(second, Is.EqualTo(first));
        }

        private static byte ResolveFarSurface(byte lowSurface, byte highSurface, int height)
        {
            Type farTerrainType = FindType("VoxelEngine.Showcase.VoxelFarTerrain");
            MethodInfo resolver = farTerrainType.GetMethod(
                "ResolveFarSurfaceMaterial",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolver, Is.Not.Null, "far terrain must expose its semantic material resolver");

            Type materialSetType = resolver.GetParameters()[0].ParameterType;
            object materialSet = Activator.CreateInstance(
                materialSetType,
                new object[]
                {
                    (byte)2, (byte)3, lowSurface, highSurface,
                    (byte)4, (byte)6, (byte)8,
                    (byte)8, (byte)8, (byte)10, (byte)4, (byte)12, (byte)13,
                    (byte)14, (byte)15, (byte)16, (byte)17, (byte)18, (byte)3, 0u,
                });

            return (byte)resolver.Invoke(
                null,
                new[] { materialSet, false, false, (object)(byte)0, height });
        }

        private static int ShowcaseBaseHeight()
        {
            Type showcaseWorldType = FindType("VoxelEngine.Showcase.ShowcaseWorld");
            FieldInfo field = showcaseWorldType.GetField(
                "BaseHeightVoxels",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "showcase terrain split must remain discoverable");
            return (int)field.GetRawConstantValue();
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"required runtime type '{fullName}' was not loaded");
            return type;
        }
    }
}
