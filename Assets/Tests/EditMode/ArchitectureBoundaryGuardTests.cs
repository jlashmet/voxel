using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Enforces the permanent subsystem assembly boundaries.
    /// </summary>
    public sealed class ArchitectureBoundaryGuardTests
    {
        private const string CompositionAssembly = "VoxelEngine.Composition";

        private static readonly Regex NameRegex = new Regex(
            "\"name\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled);

        private static readonly Regex ReferencesRegex = new Regex(
            "\"references\"\\s*:\\s*\\[(?<value>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex QuotedStringRegex = new Regex(
            "\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled);

        private static readonly Regex GuidRegex = new Regex(
            @"^\s*guid:\s*(?<value>[0-9a-fA-F]+)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void ApiAssembliesDoNotReferenceRuntimeAssemblies()
        {
            var violations = new List<string>();

            foreach (Asmdef asmdef in EnumerateVoxelEngineAsmdefs())
            {
                if (!asmdef.Name.EndsWith(".Api", StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References)
                {
                    if (IsConcreteRuntimeReference(reference))
                        violations.Add(asmdef.Name + " -> " + reference + " (" + asmdef.RelativePath + ")");
                }
            }

            Assert.IsEmpty(violations,
                "API assemblies are contracts. They may not depend on runtime implementations.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void RuntimeAssembliesDoNotReferenceForeignRuntimeAssemblies()
        {
            var violations = new List<string>();

            foreach (Asmdef asmdef in EnumerateVoxelEngineAsmdefs())
            {
                if (!asmdef.Name.EndsWith(".Runtime", StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References)
                {
                    if (!IsConcreteRuntimeReference(reference))
                        continue;

                    if (!string.Equals(reference, asmdef.Name, StringComparison.Ordinal))
                        violations.Add(asmdef.Name + " -> " + reference + " (" + asmdef.RelativePath + ")");
                }
            }

            Assert.IsEmpty(violations,
                "Subsystem runtimes may consume another subsystem only through its Api assembly.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionAssemblies_ConcreteRuntimeReferencesAreOwnedByComposition()
        {
            var violations = new List<string>();

            foreach (Asmdef asmdef in EnumerateProjectAsmdefs())
            {
                if (!IsProductionAssembly(asmdef)
                    || string.Equals(asmdef.Name, CompositionAssembly, StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References)
                {
                    if (IsConcreteRuntimeReference(reference))
                        violations.Add(asmdef.Name + " -> " + reference + " (" + asmdef.RelativePath + ")");
                }
            }

            Assert.IsEmpty(violations,
                "Production code may depend on subsystem implementations only through the Composition root.\n" +
                "Concrete VoxelEngine.*.Runtime references outside VoxelEngine.Composition are forbidden.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionAssembliesDoNotReferenceRemovedCoreAssembly()
        {
            var violations = new List<string>();

            foreach (Asmdef asmdef in EnumerateProjectAsmdefs())
            {
                if (!IsProductionAssembly(asmdef))
                    continue;

                if (string.Equals(asmdef.Name, "VoxelEngine.Core", StringComparison.Ordinal))
                    violations.Add("removed assembly still exists: " + asmdef.RelativePath);

                foreach (string reference in asmdef.References)
                {
                    if (string.Equals(reference, "VoxelEngine.Core", StringComparison.Ordinal))
                        violations.Add(asmdef.Name + " -> VoxelEngine.Core (" + asmdef.RelativePath + ")");
                }
            }

            Assert.IsEmpty(violations,
                "VoxelEngine.Core was deleted by the final cutover and may not reappear in production dependencies.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ApiAndRuntimeAssemblyNamesHaveSingleOwners()
        {
            var duplicates = EnumerateVoxelEngineAsmdefs()
                .Where(a => a.Name.EndsWith(".Api", StringComparison.Ordinal)
                            || a.Name.EndsWith(".Runtime", StringComparison.Ordinal))
                .GroupBy(a => a.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key + ": " + string.Join(", ", g.Select(a => a.RelativePath)))
                .ToList();

            Assert.IsEmpty(duplicates,
                "Each Api/Runtime assembly name must have exactly one owning subsystem.\n\n" +
                string.Join("\n", duplicates));
        }

        [Test]
        public void RenderingReadPathUsesStorageApiForPhysicalReads()
        {
            string renderingRoot = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime");
            string[] relativePaths =
            {
                Path.Combine("RenderFeature", "VoxelRenderBridge.cs"),
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"),
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"),
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"),
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"),
                Path.Combine("SurfaceExtraction", "SurfaceBrickDiscoveryJob.cs"),
            };
            string[] physicalStorageTokens = { "RegionTable", "BrickPool", "BrickRef", "VoxelAccess" };
            var violations = new List<string>();

            foreach (string relativePath in relativePaths)
            {
                string path = Path.Combine(renderingRoot, relativePath);
                Assert.IsTrue(File.Exists(path), "Missing Rendering read-path source: " + path);
                string source = File.ReadAllText(path);
                foreach (string token in physicalStorageTokens)
                {
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        violations.Add(relativePath + " -> " + token);
                }
            }

            string transvoxel = File.ReadAllText(Path.Combine(
                renderingRoot, "SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            if (transvoxel.IndexOf("VoxelMipSampler", StringComparison.Ordinal) >= 0)
                violations.Add("SurfaceExtraction/CpuTransvoxelChunkCache.cs -> VoxelMipSampler");
            if (transvoxel.IndexOf("VoxelDimensions.", StringComparison.Ordinal) >= 0)
                violations.Add("SurfaceExtraction/CpuTransvoxelChunkCache.cs -> VoxelDimensions.");

            Assert.IsEmpty(violations,
                "Rendering's authoritative read path must consume Storage through Storage.Api " +
                "read views, not physical storage representation.\n\n" +
                string.Join("\n", violations));
        }

        private static IReadOnlyList<Asmdef> EnumerateVoxelEngineAsmdefs()
        {
            return EnumerateProjectAsmdefs()
                .Where(a => a.RelativePath.StartsWith("Assets/VoxelEngine/", StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<Asmdef> EnumerateProjectAsmdefs()
        {
            string root = Path.Combine(RepoRoot, "Assets");
            Assert.IsTrue(Directory.Exists(root), "Missing Assets source root: " + root);

            string[] paths = Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            IReadOnlyDictionary<string, string> assemblyNamesByGuid = BuildAssemblyNamesByGuid(paths);

            return paths
                .Select(path => ParseAsmdef(path, assemblyNamesByGuid))
                .OrderBy(a => a.RelativePath, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyDictionary<string, string> BuildAssemblyNamesByGuid(IEnumerable<string> asmdefPaths)
        {
            var namesByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in asmdefPaths)
            {
                string metaPath = path + ".meta";
                Assert.IsTrue(File.Exists(metaPath), "Missing asmdef metadata: " + metaPath);

                Match guidMatch = GuidRegex.Match(File.ReadAllText(metaPath));
                Assert.IsTrue(guidMatch.Success, "Could not parse asmdef GUID from " + metaPath);

                string guid = guidMatch.Groups["value"].Value;
                string name = ParseAssemblyName(path);
                string existing;
                if (namesByGuid.TryGetValue(guid, out existing))
                {
                    Assert.AreEqual(existing, name,
                        "Duplicate asmdef GUID " + guid + " is owned by both " + existing + " and " + name + ".");
                    continue;
                }

                namesByGuid.Add(guid, name);
            }

            return namesByGuid;
        }

        private static Asmdef ParseAsmdef(
            string path,
            IReadOnlyDictionary<string, string> assemblyNamesByGuid)
        {
            string json = File.ReadAllText(path);
            string name = ParseAssemblyName(path);
            var references = new List<string>();
            Match block = ReferencesRegex.Match(json);
            if (block.Success)
            {
                foreach (Match match in QuotedStringRegex.Matches(block.Groups["value"].Value))
                {
                    string reference = match.Groups["value"].Value;
                    if (!reference.StartsWith("GUID:", StringComparison.Ordinal))
                    {
                        references.Add(reference);
                        continue;
                    }

                    string guid = reference.Substring("GUID:".Length);
                    string resolvedName;
                    if (assemblyNamesByGuid.TryGetValue(guid, out resolvedName))
                        references.Add(resolvedName);
                    // Unresolved GUIDs belong to package/external assemblies outside Assets.
                }
            }

            return new Asmdef(name, Relative(path), references);
        }

        private static string ParseAssemblyName(string path)
        {
            Match nameMatch = NameRegex.Match(File.ReadAllText(path));
            Assert.IsTrue(nameMatch.Success, "Could not parse assembly name from " + path);
            return nameMatch.Groups["value"].Value;
        }

        private static bool IsConcreteRuntimeReference(string reference)
        {
            return reference.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                   && reference.EndsWith(".Runtime", StringComparison.Ordinal);
        }

        private static bool IsProductionAssembly(Asmdef asmdef)
        {
            string path = "/" + asmdef.RelativePath.Replace('\\', '/') + "/";
            return path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0
                   && path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0
                   && asmdef.Name.IndexOf(".Tests", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string Relative(string path)
        {
            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            return normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;
        }

        private sealed class Asmdef
        {
            public Asmdef(string name, string relativePath, IReadOnlyList<string> references)
            {
                Name = name;
                RelativePath = relativePath;
                References = references;
            }

            public string Name { get; private set; }
            public string RelativePath { get; private set; }
            public IReadOnlyList<string> References { get; private set; }
        }
    }
}
