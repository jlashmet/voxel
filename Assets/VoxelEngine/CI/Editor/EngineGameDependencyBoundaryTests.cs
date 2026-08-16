using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

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
    }
}
