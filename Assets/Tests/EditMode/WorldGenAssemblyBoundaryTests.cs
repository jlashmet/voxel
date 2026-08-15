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
        private static readonly Regex EngineRuntimeNamespaceRegex = new Regex(
            @"VoxelEngine\.[A-Za-z0-9_.]+\.Runtime(?:\.|\b)",
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
        public void SemanticWorldGenSourceDoesNotImportVoxelEngine()
        {
            string packageRoot = Path.Combine(
                RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime");
            string[] semanticRoots =
            {
                Path.Combine(packageRoot, "Core"),
                Path.Combine(packageRoot, "Architecture"),
            };
            var violations = new List<string>();

            foreach (string root in semanticRoots)
            {
                Assert.IsTrue(Directory.Exists(root), "Missing semantic worldgen source root: " + root);
                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(path);
                    if (source.IndexOf("VoxelEngine.", StringComparison.Ordinal) >= 0)
                        violations.Add(Path.GetRelativePath(packageRoot, path));
                }
            }

            Assert.IsEmpty(violations,
                "Semantic WorldGen source must stay engine-independent, not merely rely on an asmdef " +
                "that happens not to reference VoxelEngine.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void VoxelAdapterReferencesOnlyEngineApiAssemblies()
        {
            const string asmdef = "Runtime/Voxel/MountingForce.WorldGen.Voxel.asmdef";
            var violations = ReadReferences(asmdef)
                .Where(r => r.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                            && !r.EndsWith(".Api", StringComparison.Ordinal))
                .ToArray();

            Assert.IsEmpty(violations,
                "The worldgen Voxel adapter may consume engine contracts only. Every VoxelEngine " +
                "assembly reference must be an Api assembly.\n" + string.Join("\n", violations));
        }

        [Test]
        public void VoxelAdapterSourceDoesNotImportEngineRuntimeNamespaces()
        {
            string voxelRoot = Path.Combine(
                RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel");
            Assert.IsTrue(Directory.Exists(voxelRoot), "Missing WorldGen Voxel adapter root: " + voxelRoot);
            var violations = new List<string>();

            foreach (string path in Directory.EnumerateFiles(voxelRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                if (EngineRuntimeNamespaceRegex.IsMatch(source))
                    violations.Add(Path.GetRelativePath(voxelRoot, path));
            }

            Assert.IsEmpty(violations,
                "WorldGen Voxel source must not bypass Api assembly references with direct Runtime " +
                "namespace coupling.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void CastleVegetationPlannerReadsVoxelStateThroughStorageApi()
        {
            string path = Path.Combine(
                RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel",
                "CastleVegetationPlanner.cs");
            Assert.IsTrue(File.Exists(path), "Missing Castle vegetation planner: " + path);

            string source = File.ReadAllText(path);
            string[] forbidden = { "RegionTable", "BrickPool", "BrickRef", "VoxelAccess" };
            var violations = forbidden
                .Where(token => source.IndexOf(token, StringComparison.Ordinal) >= 0)
                .ToArray();

            Assert.IsEmpty(violations,
                "Worldgen voxel realization may query authoritative voxel state through Storage.Api, " +
                "but must not depend on Core's physical storage representation.\n" +
                string.Join("\n", violations));
            StringAssert.Contains("VoxelEngine.Storage.Api", source);
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