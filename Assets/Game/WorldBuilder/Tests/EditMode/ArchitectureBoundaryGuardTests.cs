using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Enforces the subsystem assembly rules while the architecture migration is in progress.
    /// New Api/Runtime assemblies become protected as soon as they are added.
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
        public void ProductionSourcesDoNotReferenceVoxelEngineCore()
        {
            string[] roots =
            {
                Path.Combine(RepoRoot, "Assets", "VoxelEngine"),
                Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase"),
                Path.Combine(RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime"),
            };
            string[] extensions = { ".cs", ".asmdef", ".asmref", ".json" };
            var violations = new List<string>();

            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        continue;

                    string source = File.ReadAllText(path);
                    if (source.IndexOf("VoxelEngine.Core", StringComparison.Ordinal) < 0)
                        continue;

                    string normalized = path.Replace('\\', '/');
                    string repoRoot = RepoRoot.Replace('\\', '/');
                    violations.Add(normalized.StartsWith(repoRoot + "/", StringComparison.Ordinal)
                        ? normalized.Substring(repoRoot.Length + 1)
                        : normalized);
                }
            }

            Assert.IsEmpty(violations,
                "Production source and assembly metadata may not reference the deleted " +
                "VoxelEngine.Core namespace or assembly.\n\n" + string.Join("\n", violations));
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
                "read views, not physical Core storage representation.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ArchLookdevDoesNotReachIntoStructuresRuntime()
        {
            string path = Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase", "ArchLookdev.cs");
            Assert.IsTrue(File.Exists(path), "Missing ArchLookdev source: " + path);

            string source = File.ReadAllText(path);
            string[] forbiddenTokens =
            {
                "VoxelEngine.Structures.Runtime",
                "ProfileBlockStore",
                "ArchFeatureDefinition",
                "ArchBayFeatureDefinition",
                "ArchRuinDamage",
                "PrimitiveRasteriser",
                "VoxelBrush",
                "MasonryWeathering",
                "NativeList<Primitive>",
            };
            var violations = forbiddenTokens
                .Where(token => source.IndexOf(token, StringComparison.Ordinal) >= 0)
                .ToList();

            Assert.IsEmpty(violations,
                "ArchLookdev must consume stable Api/Composition contracts and must not reach into " +
                "Structures.Runtime implementation details.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void ShowcaseMultiplayerDoesNotReachIntoNetworkingRuntime()
        {
            string sourcePath = Path.Combine(
                RepoRoot, "Assets", "Scenes", "Showcase", "ShowcaseMultiplayerSession.cs");
            Assert.IsTrue(File.Exists(sourcePath),
                "Missing Showcase multiplayer source: " + sourcePath);

            string source = File.ReadAllText(sourcePath);
            string[] forbiddenSourceTokens =
            {
                "VoxelEngine.Net.Runtime",
                "VoxelEngine.Edits.Runtime",
                "Unity.Networking.Transport",
                "AuthoritativeServerSession",
                "ClientNetworkRuntime",
                "C_PlayerInput",
                "S_PlayerState",
            };
            var violations = forbiddenSourceTokens
                .Where(token => source.IndexOf(token, StringComparison.Ordinal) >= 0)
                .Select(token => "ShowcaseMultiplayerSession.cs -> " + token)
                .ToList();

            string showcaseAsmdefPath = Path.Combine(
                RepoRoot, "Assets", "Scenes", "Showcase", "VoxelEngine.Showcase.asmdef");
            string showcaseAsmdef = File.ReadAllText(showcaseAsmdefPath);
            if (showcaseAsmdef.IndexOf(
                    "\"VoxelEngine.Net.Runtime\"", StringComparison.Ordinal) >= 0)
                violations.Add("VoxelEngine.Showcase.asmdef -> VoxelEngine.Net.Runtime");
            if (showcaseAsmdef.IndexOf(
                    "\"Unity.Networking.Transport\"", StringComparison.Ordinal) >= 0)
                violations.Add("VoxelEngine.Showcase.asmdef -> Unity.Networking.Transport");

            string compositionAsmdefPath = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "VoxelEngine.Composition.asmdef");
            string compositionAsmdef = File.ReadAllText(compositionAsmdefPath);
            if (compositionAsmdef.IndexOf(
                    "\"VoxelEngine.Net.Runtime\"", StringComparison.Ordinal) < 0)
                violations.Add("VoxelEngine.Composition.asmdef must own VoxelEngine.Net.Runtime wiring");

            Assert.IsEmpty(violations,
                "Showcase multiplayer must consume stable Composition contracts; concrete Net/UTP " +
                "wiring belongs in Composition.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void ShowcaseSceneSourcesDoNotReferenceRuntimeImplementations()
        {
            string showcaseRoot = Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase");
            Assert.IsTrue(Directory.Exists(showcaseRoot), "Missing Showcase source root: " + showcaseRoot);

            string compositionWorld = Path.Combine(
                RepoRoot, "Assets", "Game", "Composition", "Showcase", "ShowcaseWorld.cs");
            Assert.IsTrue(File.Exists(compositionWorld),
                "Concrete Showcase world ownership must live under VoxelEngine.Composition.");
            Assert.IsFalse(Directory.Exists(Path.Combine(showcaseRoot, "CompositionOwned")),
                "Do not hide Composition-owned runtime source beneath the Showcase scene tree.");

            var runtimeReference = new Regex(
                @"VoxelEngine\.[A-Za-z0-9_.]+\.Runtime",
                RegexOptions.Compiled);
            string[] extensions = { ".cs", ".asmdef", ".asmref" };
            var violations = new List<string>();

            foreach (string path in Directory.EnumerateFiles(showcaseRoot, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(path);
                foreach (Match match in runtimeReference.Matches(source))
                {
                    string normalized = path.Replace('\\', '/');
                    string repoRoot = RepoRoot.Replace('\\', '/');
                    string relative = normalized.StartsWith(repoRoot + "/", StringComparison.Ordinal)
                        ? normalized.Substring(repoRoot.Length + 1)
                        : normalized;
                    violations.Add(relative + " -> " + match.Value);
                }
            }

            Assert.IsEmpty(violations,
                "Scene/application code must consume subsystem APIs or Composition-owned facades; " +
                "concrete Runtime ownership belongs in VoxelEngine.Composition.\n\n" +
                string.Join("\n", violations.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)));
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

            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            string relative = normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;

            return new Asmdef(nameMatch.Groups["value"].Value, relative, references);
        }

        private static string ResolveVoxelEngineReference(string reference)
        {
            if (!reference.StartsWith("GUID:", StringComparison.Ordinal)) return reference;

            string guid = reference.Substring("GUID:".Length);
            string root = Path.Combine(RepoRoot, "Assets", "VoxelEngine");
            foreach (string metaPath in Directory.EnumerateFiles(
                         root, "*.asmdef.meta", SearchOption.AllDirectories))
            {
                Match guidMatch = GuidRegex.Match(File.ReadAllText(metaPath));
                if (!guidMatch.Success
                    || !string.Equals(guidMatch.Groups["value"].Value, guid,
                                      StringComparison.OrdinalIgnoreCase))
                    continue;

                string asmdefPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                if (!File.Exists(asmdefPath)) return reference;
                Match nameMatch = NameRegex.Match(File.ReadAllText(asmdefPath));
                return nameMatch.Success ? nameMatch.Groups["value"].Value : reference;
            }

            // GUID references to external Unity/package assemblies are outside the engine graph.
            return reference;
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
