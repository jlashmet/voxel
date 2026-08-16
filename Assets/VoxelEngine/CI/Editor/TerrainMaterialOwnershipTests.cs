using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.CI
{
    public sealed class TerrainMaterialOwnershipTests
    {
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
