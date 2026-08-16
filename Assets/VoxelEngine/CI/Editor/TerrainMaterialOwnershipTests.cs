using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.CI
{
    public sealed class TerrainMaterialOwnershipTests
    {
        [Test]
        public void TerrainGenerator_ExposesNoCompileTimeMaterialIds()
        {
            FieldInfo[] fields = typeof(TerrainGenerator)
                .GetFields(BindingFlags.Public | BindingFlags.Static);
            var constants = new List<string>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsLiteral && fields[i].FieldType == typeof(byte))
                    constants.Add(fields[i].Name);
            }

            Assert.That(constants, Is.Empty,
                "TerrainGenerator must consume opaque TerrainMaterialSet roles instead of " +
                "defining game material IDs: " + string.Join(", ", constants));
        }

        [Test]
        public void LegacyGenerate_RequiresApplicationConfiguredMaterialRoles()
        {
            bool hadPrevious = TerrainMaterialCompatibility.IsConfigured;
            TerrainMaterialSet previous = hadPrevious
                ? TerrainMaterialCompatibility.RequireConfigured()
                : default;

            try
            {
                TerrainMaterialCompatibility.Reset();
#pragma warning disable CS0618
                Assert.Throws<InvalidOperationException>(() =>
                    TerrainGenerator.Generate(null, int3.zero, 1u));
#pragma warning restore CS0618
            }
            finally
            {
                if (hadPrevious)
                    TerrainMaterialCompatibility.Configure(in previous);
                else
                    TerrainMaterialCompatibility.Reset();
            }
        }
    }
}
