using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Final production-wide dependency closure for the subsystem refactor.
    /// Composition is the only production assembly allowed to wire concrete Runtime assemblies.
    /// </summary>
    public sealed class ProductionArchitectureClosureTests
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
            "^guid:\\s*(?<value>[0-9a-fA-F]+)\\s*$",
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
        public void CompositionIsTheOnlyProductionAssemblyThatReferencesConcreteRuntimes()
        {
            IReadOnlyDictionary<string, string> guidOwners = BuildAsmdefGuidOwners();
            var violations = new List<string>();

            foreach (string path in EnumerateProductionAsmdefPaths())
            {
                Asmdef asmdef = ParseAsmdef(path, guidOwners);
                if (string.Equals(asmdef.Name, "VoxelEngine.Composition", StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References)
                {
                    if (reference.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                        && reference.EndsWith(".Runtime", StringComparison.Ordinal))
                    {
                        violations.Add(asmdef.Name + " -> " + reference + " (" + asmdef.RelativePath + ")");
                    }
                }
            }

            Assert.IsEmpty(violations,
                "Composition must be the only production assembly that wires concrete subsystem runtimes.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionSourcesDoNotReferenceVoxelEngineCore()
        {
            string[] extensions = { ".cs", ".asmdef", ".asmref", ".json" };
            var violations = new List<string>();

            foreach (string root in ProductionRoots())
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        continue;

                    string source = File.ReadAllText(path);
                    if (source.IndexOf("VoxelEngine.Core", StringComparison.Ordinal) >= 0)
                        violations.Add(RelativePath(path));
                }
            }

            Assert.IsEmpty(violations,
                "Production source must not retain the deleted VoxelEngine.Core architecture.\n\n" +
                string.Join("\n", violations));
        }

        private static IEnumerable<string> EnumerateProductionAsmdefPaths()
        {
            foreach (string root in ProductionRoots())
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string path in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
                    yield return path;
            }
        }

        private static string[] ProductionRoots()
        {
            return new[]
            {
                Path.Combine(RepoRoot, "Assets", "VoxelEngine"),
                Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase"),
                Path.Combine(RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime"),
            };
        }

        private static IReadOnlyDictionary<string, string> BuildAsmdefGuidOwners()
        {
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] roots =
            {
                Path.Combine(RepoRoot, "Assets"),
                Path.Combine(RepoRoot, "Packages"),
            };

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string asmdefPath in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
                {
                    Match name = NameRegex.Match(File.ReadAllText(asmdefPath));
                    if (!name.Success)
                        continue;

                    string metaPath = asmdefPath + ".meta";
                    if (!File.Exists(metaPath))
                        continue;

                    Match guid = GuidRegex.Match(File.ReadAllText(metaPath));
                    if (guid.Success)
                        owners[guid.Groups["value"].Value] = name.Groups["value"].Value;
                }
            }

            return owners;
        }

        private static Asmdef ParseAsmdef(
            string path,
            IReadOnlyDictionary<string, string> guidOwners)
        {
            string json = File.ReadAllText(path);
            Match nameMatch = NameRegex.Match(json);
            Assert.IsTrue(nameMatch.Success, "Could not parse assembly name from " + path);

            var references = new List<string>();
            Match block = ReferencesRegex.Match(json);
            if (block.Success)
            {
                foreach (Match match in QuotedStringRegex.Matches(block.Groups["value"].Value))
                {
                    string reference = match.Groups["value"].Value;
                    if (reference.StartsWith("GUID:", StringComparison.Ordinal))
                    {
                        string guid = reference.Substring("GUID:".Length);
                        if (guidOwners.TryGetValue(guid, out string owner))
                            references.Add(owner);
                        continue;
                    }

                    references.Add(reference);
                }
            }

            return new Asmdef(nameMatch.Groups["value"].Value, RelativePath(path), references);
        }

        private static string RelativePath(string path)
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

            public string Name { get; }
            public string RelativePath { get; }
            public IReadOnlyList<string> References { get; }
        }
    }
}
