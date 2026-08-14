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
            "\\\"name\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"",
            RegexOptions.Compiled);

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

                // Composition is the deliberate wiring root and is the only runtime-to-runtime exception.
                if (string.Equals(asmdef.Name, "VoxelEngine.Composition.Runtime", StringComparison.Ordinal))
                    continue;

                foreach (string reference in asmdef.References)
                {
                    if (!reference.EndsWith(".Runtime", StringComparison.Ordinal))
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
                {
                    string reference = match.Groups["value"].Value;
                    // Target VoxelEngine subsystem asmdefs use named references. Existing GUID
                    // references are intentionally ignored until their owner is migrated.
                    if (!reference.StartsWith("GUID:", StringComparison.Ordinal))
                        references.Add(reference);
                }
            }

            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            string relative = normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;

            return new Asmdef(nameMatch.Groups["value"].Value, relative, references);
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
