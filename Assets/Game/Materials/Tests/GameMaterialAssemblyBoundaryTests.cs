using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialAssemblyBoundaryTests
    {
        private static readonly Regex EngineRuntimeReference = new(
            "\\\"VoxelEngine\\.[^\\\"]*\\.Runtime\\\"",
            RegexOptions.Compiled);

        [Test]
        public void MaterialGameAssemblies_ReferenceEngineApisOrComposition_NotRuntimeImplementations()
        {
            string[] roots =
            {
                Path.Combine(Application.dataPath, "Game", "Materials"),
                Path.Combine(Application.dataPath, "Game", "Composition", "Materials"),
            };
            var violations = new List<string>();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string[] asmdefs = Directory.GetFiles(
                    roots[rootIndex], "*.asmdef", SearchOption.AllDirectories);
                for (int i = 0; i < asmdefs.Length; i++)
                {
                    string contents = File.ReadAllText(asmdefs[i]);
                    if (!EngineRuntimeReference.IsMatch(contents)) continue;

                    string relative = asmdefs[i].Substring(Application.dataPath.Length + 1)
                        .Replace('\\', '/');
                    violations.Add("Assets/" + relative);
                }
            }

            Assert.That(violations, Is.Empty,
                "Game material assemblies must consume VoxelEngine APIs or Composition, never " +
                "engine Runtime implementations:\n" + string.Join("\n", violations));
        }
    }
}
