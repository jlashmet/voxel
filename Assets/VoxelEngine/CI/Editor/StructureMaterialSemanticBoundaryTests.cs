using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Prevents semantic game-material names from leaking back into reusable structure runtime code.
    /// Structures.Runtime must consume opaque material indices and generic material properties only.
    /// </summary>
    public sealed class StructureMaterialSemanticBoundaryTests
    {
        private static readonly Regex SemanticMaterialReference =
            new(@"\bMat\.[A-Za-z_]\w*", RegexOptions.Compiled);

        [Test]
        public void StructureRuntime_DoesNotUseSemanticMaterialFacade()
        {
            string runtimeRoot = Path.Combine(
                Application.dataPath, "VoxelEngine", "Structures", "Runtime");
            string[] sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < sources.Length; i++)
            {
                string source = sources[i];
                string contents = File.ReadAllText(source);
                Match match = SemanticMaterialReference.Match(contents);
                if (!match.Success) continue;

                string relative = source.Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add($"Assets/{relative}: {match.Value}");
            }

            Assert.That(violations, Is.Empty,
                "VoxelEngine.Structures.Runtime must not contain semantic game-material references. " +
                "Use opaque material indices and generic material properties instead.\n" +
                string.Join("\n", violations));
        }
    }
}
