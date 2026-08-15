using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Permanently enforces the engine subsystem assembly boundaries.
    /// </summary>
    public sealed class ArchitectureBoundaryGuardTests
    {
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
            "^guid:\\s*(?<value>[0-9a-fA-F]{32})\\s*$",
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
                    if (reference.EndsWith(".Runtime", StringComparison.Ordinal))
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
                    if (!reference.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                        || !reference.EndsWith(".Runtime", StringComparison.Ordinal))
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
        public void CompositionIsOnlyProductionRuntimeWiringAssembly()
        {
            Asmdef[] production = EnumerateProjectAsmdefs()
                .Where(a => IsProductionPath(a.RelativePath))
                .ToArray();

            Asmdef composition = production.SingleOrDefault(
                a => string.Equals(a.Name, "VoxelEngine.Composition", StringComparison.Ordinal));
            Assert.NotNull(composition,
                "VoxelEngine.Composition must exist as the single concrete runtime wiring root.");

            string[] compositionRuntimeReferences = composition.References
                .Where(IsVoxelEngineRuntimeReference)
                .ToArray();
            Assert.IsNotEmpty(compositionRuntimeReferences,
                "VoxelEngine.Composition must actually wire concrete Runtime assemblies; the exception may not pass vacuously.");

            var violations = new List<string>();
            foreach (Asmdef asmdef in production)
            {
                if (string.Equals(asmdef.Name, "VoxelEngine.Composition", StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References.Where(IsVoxelEngineRuntimeReference))
                    violations.Add(asmdef.Name + " -> " + reference + " (" + asmdef.RelativePath + ")");
            }

            Assert.IsEmpty(violations,
                "Composition is the sole production assembly allowed to wire concrete VoxelEngine Runtime assemblies. " +
                "Tests, CI and Editor tooling may reference implementations explicitly.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionSourcesDoNotReferenceVoxelEngineCore()
        {
            string[] roots =
            {
                Path.Combine(RepoRoot, "Assets"),
                Path.Combine(RepoRoot, "Packages"),
            };
            string[] extensions = { ".cs", ".asmdef", ".asmref", ".json" };
            var violations = new List<string>();

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        continue;

                    string relativePath = ToRepoRelativePath(path);
                    if (!IsProductionPath(relativePath))
                        continue;

                    string source = File.ReadAllText(path);
                    if (source.IndexOf("VoxelEngine.Core", StringComparison.Ordinal) < 0)
                        continue;

                    violations.Add(relativePath);
                }
            }

            Assert.IsEmpty(violations,
                "Production source and assembly metadata may not reference the deleted VoxelEngine.Core namespace or assembly.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void CompositionShowcaseWorldExposesProfileBlocksThroughStorageApi()
        {
            string path = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase", "ShowcaseWorld.cs");
            Assert.IsTrue(File.Exists(path), "Missing Composition-owned ShowcaseWorld: " + path);

            string source = File.ReadAllText(path);
            StringAssert.Contains("public IProfileBlockReadSource ProfileBlocks => _profileBlocks;", source,
                "ShowcaseWorld must expose retained profile blocks through the Storage.Api read contract.");
            StringAssert.DoesNotContain("public ProfileBlockStore ProfileBlocks", source,
                "A public Composition surface may not force scene assemblies to reference Structures.Runtime.");
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

        private static bool IsVoxelEngineRuntimeReference(string reference)
        {
            return reference.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                   && reference.EndsWith(".Runtime", StringComparison.Ordinal);
        }

        private static IReadOnlyList<Asmdef> EnumerateVoxelEngineAsmdefs()
        {
            string root = Path.Combine(RepoRoot, "Assets", "VoxelEngine");
            Assert.IsTrue(Directory.Exists(root), "Missing VoxelEngine source root: " + root);

            return Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories)
                .Select(ParseAsmdef)
                .OrderBy(a => a.RelativePath, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<Asmdef> EnumerateProjectAsmdefs()
        {
            var paths = new List<string>();
            string assets = Path.Combine(RepoRoot, "Assets");
            string packages = Path.Combine(RepoRoot, "Packages");

            if (Directory.Exists(assets))
                paths.AddRange(Directory.EnumerateFiles(assets, "*.asmdef", SearchOption.AllDirectories));
            if (Directory.Exists(packages))
                paths.AddRange(Directory.EnumerateFiles(packages, "*.asmdef", SearchOption.AllDirectories));

            return paths
                .Select(ParseAsmdef)
                .OrderBy(a => a.RelativePath, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsProductionPath(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("/Test/", StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0
                   && !normalized.StartsWith("Assets/Tests/", StringComparison.OrdinalIgnoreCase)
                   && !normalized.StartsWith("Assets/VoxelEngine/CI/", StringComparison.OrdinalIgnoreCase);
        }

        private static Asmdef ParseAsmdef(string path)
        {
            string json = File.ReadAllText(path);
            Match nameMatch = NameRegex.Match(json);
            Assert.IsTrue(nameMatch.Success, "Could not parse assembly name from " + path);

            var references = new List<string>();
            Match block = ReferencesRegex.Match(json);
            if (block.Success)
            {
                foreach (Match match in QuotedStringRegex.Matches(block.Groups["value"].Value))
                    references.Add(ResolveVoxelEngineReference(match.Groups["value"].Value));
            }

            return new Asmdef(nameMatch.Groups["value"].Value, ToRepoRelativePath(path), references);
        }

        private static string ResolveVoxelEngineReference(string reference)
        {
            if (!reference.StartsWith("GUID:", StringComparison.Ordinal))
                return reference;

            string guid = reference.Substring("GUID:".Length);
            string root = Path.Combine(RepoRoot, "Assets", "VoxelEngine");
            if (!Directory.Exists(root))
                return reference;

            foreach (string metaPath in Directory.EnumerateFiles(root, "*.asmdef.meta", SearchOption.AllDirectories))
            {
                Match guidMatch = GuidRegex.Match(File.ReadAllText(metaPath));
                if (!guidMatch.Success
                    || !string.Equals(guidMatch.Groups["value"].Value, guid, StringComparison.OrdinalIgnoreCase))
                    continue;

                string asmdefPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                if (!File.Exists(asmdefPath))
                    return reference;

                Match nameMatch = NameRegex.Match(File.ReadAllText(asmdefPath));
                return nameMatch.Success ? nameMatch.Groups["value"].Value : reference;
            }

            // GUID references to external Unity/package assemblies are outside the engine graph.
            return reference;
        }

        private static string ToRepoRelativePath(string path)
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
