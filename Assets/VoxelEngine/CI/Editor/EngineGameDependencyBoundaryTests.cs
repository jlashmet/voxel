using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Protects the dependency direction: application/game assemblies may consume VoxelEngine APIs,
    /// but an assembly rooted under Assets/VoxelEngine may never take a direct Game.* dependency.
    /// </summary>
    public sealed class EngineGameDependencyBoundaryTests
    {
        [Test]
        public void VoxelEngineAssemblies_DoNotReferenceGameAssemblies()
        {
            string voxelEngineRoot = Path.Combine(Application.dataPath, "VoxelEngine");
            string[] asmdefs = Directory.GetFiles(
                voxelEngineRoot, "*.asmdef", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string contents = File.ReadAllText(asmdefs[i]);
                if (!contents.Contains("\"Game.")) continue;

                string relative = asmdefs[i].Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add("Assets/" + relative);
            }

            Assert.That(violations, Is.Empty,
                "VoxelEngine assemblies must not depend on Game assemblies. Move semantic/content " +
                "ownership to Assets/Game and depend on VoxelEngine APIs instead.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void LegacyStructureMaterialFacade_HasNoCompileTimeMaterialIds()
        {
            FieldInfo[] fields = typeof(Mat).GetFields(BindingFlags.Public | BindingFlags.Static);
            var constants = new List<string>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsLiteral && fields[i].FieldType == typeof(byte))
                    constants.Add(fields[i].Name);
            }

            Assert.That(constants, Is.Empty,
                "The transitional Mat facade may resolve application-configured roles, but it must " +
                "never define numeric material IDs in VoxelEngine: " + string.Join(", ", constants));
        }
    }
}
