using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
        public void TerrainGenerator_RequiresExplicitMaterialRoles()
        {
            MethodInfo[] methods = typeof(TerrainGenerator)
                .GetMethods(BindingFlags.Public | BindingFlags.Static);
            var generateOverloads = new List<MethodInfo>();

            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == nameof(TerrainGenerator.Generate))
                    generateOverloads.Add(methods[i]);
            }

            Assert.That(generateOverloads, Has.Count.EqualTo(1),
                "Terrain generation must have one explicit application-material entry point; " +
                "do not reintroduce a configured/global compatibility overload.");

            ParameterInfo[] parameters = generateOverloads[0].GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(4));
            Assert.That(parameters[3].ParameterType, Is.EqualTo(typeof(TerrainMaterialSet)),
                "The final TerrainGenerator.Generate parameter must be the caller-supplied " +
                "TerrainMaterialSet.");
        }
    }
}
