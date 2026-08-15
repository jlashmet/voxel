using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Final Cutover 13 closure guards for production dependency wiring.
    /// Composition is the only production assembly allowed to reference concrete
    /// VoxelEngine Runtime assemblies, and the deleted Core facade may not return.
    /// </summary>
    public sealed class ProductionArchitectureClosureTests
    {
        private const string CompositionAssemblyName = "VoxelEngine.Composition";
        private const string DeletedCoreNamespace = "VoxelEngine.Core";

        private static readonly Regex NameRegex = new Regex(
            "\"name\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled);

        private static readonly Regex ReferencesRegex = new Regex(
            "\"references\"\\s*:\\s*\\[(?<value>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex QuotedStringRegex = new Regex(
            "\"(?<value>[^\"]+)\"",
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
        public void CompositionIsTheOnlyProductionAssemblyThatReferencesRuntimeImplementations()
        {
            var violations = new List<string>();

            foreach (string asmdefPath in EnumerateProductionFiles("*.asmdef"))
            {
                string json = File.ReadAllText(asmdefPath);
                Match nameMatch = NameRegex.Match(json);
                Assert.IsTrue(nameMatch.Success, "Could not parse assembly name from " + asmdefPath);

                string assemblyName = nameMatch.Groups["value"].Value;
                if (string.Equals(assemblyName, CompositionAssemblyName, StringComparison.Ordinal))
                    continue;

                Match referencesMatch = ReferencesRegex.Match(json);
                if (!referencesMatch.Success)
                    continue;

                foreach (Match quoted in QuotedStringRegex.Matches(referencesMatch.Groups["value"].Value))
                {
                    string reference = quoted.Groups["value"].Value;
                    if (reference.StartsWith("VoxelEngine.", StringComparison.Ordinal)
                        && reference.EndsWith(".Runtime", StringComparison.Ordinal))
                    {
                        violations.Add(
                            RelativePath(asmdefPath) + " (" + assemblyName + ") -> " + reference);
                    }
                }
            }

            Assert.IsEmpty(violations,
                "Composition must be the only production assembly that wires concrete Runtime implementations.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void DeletedCoreNamespaceDoesNotReappearInProductionSourceOrAsmdefs()
        {
            var violations = new List<string>();

            foreach (string pattern in new[] { "*.cs", "*.asmdef" })
            {
                foreach (string path in EnumerateProductionFiles(pattern))
                {
                    string text = File.ReadAllText(path);
                    if (text.IndexOf(DeletedCoreNamespace, StringComparison.Ordinal) >= 0)
                        violations.Add(RelativePath(path));
                }
            }

            Assert.IsEmpty(violations,
                "VoxelEngine.Core was deleted during the clean cutover and must not reappear in production code.\n\n" +
                string.Join("\n", violations));
        }

        private static IEnumerable<string> EnumerateProductionFiles(string pattern)
        {
            foreach (string relativeRoot in ProductionRoots())
            {
                string root = Path.Combine(RepoRoot, relativeRoot);
                if (!Directory.Exists(root))
                    continue;

                foreach (string path in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                    yield return path;
            }
        }

        private static IEnumerable<string> ProductionRoots()
        {
            yield return Path.Combine("Assets", "VoxelEngine");
            yield return Path.Combine("Assets", "Scenes", "Showcase");
            yield return Path.Combine("Packages", "com.mountingforce.worldgen", "Runtime");
        }

        private static string RelativePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            return normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;
        }
    }
}
