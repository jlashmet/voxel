using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        private static readonly Regex LegacyStructureMaterialDeclaration = new(
            @"\b(?:static\s+)?class\s+Mat\b|\bstruct\s+StructureMaterialSet\b",
            RegexOptions.Compiled);

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
        public void StructuresModule_DoesNotReintroduceLegacySemanticMaterialTypes()
        {
            string structuresRoot = Path.Combine(Application.dataPath, "VoxelEngine", "Structures");
            string[] sources = Directory.GetFiles(structuresRoot, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < sources.Length; i++)
            {
                string contents = File.ReadAllText(sources[i]);
                Match match = LegacyStructureMaterialDeclaration.Match(contents);
                if (!match.Success) continue;

                string relative = sources[i].Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add($"Assets/{relative}: {match.Value}");
            }

            Assert.That(violations, Is.Empty,
                "VoxelEngine.Structures must not own semantic material facade/types. Game content " +
                "owns material identity; engine structure code consumes opaque indices and generic " +
                "material properties instead.\n" + string.Join("\n", violations));
        }
    }
}
