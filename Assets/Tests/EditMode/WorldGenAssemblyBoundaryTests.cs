using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldGenAssemblyBoundaryTests
    {
        private static readonly Regex ReferencesRegex = new Regex(
            "\\\"references\\\"\\s*:\\s*\\[(?<value>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex QuotedStringRegex = new Regex(
            "\\\"(?<value>[^\\\"]+)\\\"",
            RegexOptions.Compiled);

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Packages")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Packages/.");
                return dir.FullName;
            }
        }

        [TestCase("Runtime/MountingForce.WorldGen.Core.asmdef")]
        [TestCase("Runtime/Architecture/MountingForce.WorldGen.Architecture.asmdef")]
        public void SemanticWorldGenAssembliesDoNotReferenceVoxelEngine(string relativeAsmdef)
        {
            var references = ReadReferences(relativeAsmdef);
            var violations = references
                .Where(r => r.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                .ToArray();

            Assert.IsEmpty(violations,
                relativeAsmdef + " is semantic world generation and must remain independent of VoxelEngine.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void VoxelAdapterDoesNotReferenceEngineRuntimeAssemblies()
        {
            const string asmdef = "Runtime/Voxel/MountingForce.WorldGen.Voxel.asmdef";
            var violations = ReadReferences(asmdef)
                .Where(r => r.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                            && r.EndsWith(".Runtime", StringComparison.Ordinal))
                .ToArray();

            Assert.IsEmpty(violations,
                "The worldgen Voxel adapter may consume engine contracts only, never engine runtime implementations.\n" +
                string.Join("\n", violations));
        }

        private static IReadOnlyList<string> ReadReferences(string relativeAsmdef)
        {
            string path = Path.Combine(
                RepoRoot,
                "Packages",
                "com.mountingforce.worldgen",
                relativeAsmdef.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "Missing worldgen asmdef: " + path);

            string json = File.ReadAllText(path);
            Match block = ReferencesRegex.Match(json);
            if (!block.Success)
                return new string[0];

            return QuotedStringRegex.Matches(block.Groups["value"].Value)
                .Cast<Match>()
                .Select(m => m.Groups["value"].Value)
                .ToArray();
        }
    }
}
