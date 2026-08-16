using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Keeps the temporary semantic material facade confined to the one legacy content generator
    /// that has not yet been moved out of VoxelEngine. Reusable structure algorithms must consume
    /// opaque material indices and generic properties instead of asking whether a material is
    /// stone, wood, moss, water, and so on.
    /// </summary>
    public sealed class StructureMaterialSemanticBoundaryTests
    {
        private static readonly Regex SemanticMaterialReference =
            new(@"\bMat\.[A-Za-z_]\w*", RegexOptions.Compiled);

        [Test]
        public void ReusableStructureRuntime_DoesNotUseSemanticMaterialFacade()
        {
            string runtimeRoot = Path.Combine(
                Application.dataPath, "VoxelEngine", "Structures", "Runtime");
            string legacyCastle = Path.Combine(runtimeRoot, "CastleBuilder.cs");
            string[] sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < sources.Length; i++)
            {
                string source = sources[i];
                if (Path.GetFullPath(source) == Path.GetFullPath(legacyCastle))
                    continue;

                string contents = File.ReadAllText(source);
                Match match = SemanticMaterialReference.Match(contents);
                if (!match.Success) continue;

                string relative = source.Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add($"Assets/{relative}: {match.Value}");
            }

            Assert.That(violations, Is.Empty,
                "Only the explicitly isolated legacy CastleBuilder may use the transitional Mat " +
                "facade. Reusable Structures.Runtime code must use opaque material indices and " +
                "generic material properties instead.\n" + string.Join("\n", violations));
        }
    }
}
