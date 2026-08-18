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

        /// <summary>
        /// The composition layer is allowed to wire concrete Runtime assemblies; everything else
        /// must go through Api. It is no longer a single assembly: application-side composition
        /// moved to Assets/Game/Composition and is split per area (Showcase, Materials, Campaign,
        /// CombatEnvironment...), so membership is decided by location as well as by name.
        /// </summary>
        private static bool IsCompositionLayer(string assemblyName, string asmdefPath) =>
            string.Equals(assemblyName, CompositionAssemblyName, StringComparison.Ordinal)
            || asmdefPath.Replace('\\', '/').Contains("/Assets/Game/Composition/", StringComparison.Ordinal);
        private const string DeletedCoreNamespace = "VoxelEngine.Core";

        private static readonly Regex NameRegex = new Regex(
            "\"name\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled);

        private static readonly Regex ReferencesRegex = new Regex(
            "\"references\"\\s*:\\s*\\[(?<value>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex OptionalUnityReferencesRegex = new Regex(
            "\"optionalUnityReferences\"\\s*:\\s*\\[(?<value>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex QuotedStringRegex = new Regex(
            "\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled);

        private static readonly Regex GuidRegex = new Regex(
            "^guid:\\s*(?<value>[0-9a-fA-F]{32})\\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex RuntimeNamespaceRegex = new Regex(
            @"\bVoxelEngine\.[A-Za-z0-9_.]+\.Runtime\b",
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
            IReadOnlyDictionary<string, string> asmdefNamesByGuid = BuildAsmdefNamesByGuid();

            foreach (string asmdefPath in EnumerateProductionFiles("*.asmdef"))
            {
                string json = File.ReadAllText(asmdefPath);
                if (IsTestAssembly(json))
                    continue;

                Match nameMatch = NameRegex.Match(json);
                Assert.IsTrue(nameMatch.Success, "Could not parse assembly name from " + asmdefPath);

                string assemblyName = nameMatch.Groups["value"].Value;
                if (IsCompositionLayer(assemblyName, asmdefPath))
                    continue;

                Match referencesMatch = ReferencesRegex.Match(json);
                if (!referencesMatch.Success)
                    continue;

                foreach (Match quoted in QuotedStringRegex.Matches(referencesMatch.Groups["value"].Value))
                {
                    string reference = ResolveAssemblyReference(
                        quoted.Groups["value"].Value,
                        asmdefNamesByGuid);
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
        public void SceneSourceDoesNotReferenceRuntimeImplementationNamespaces()
        {
            string sceneRoot = Path.Combine(RepoRoot, "Assets", "Scenes");
            var violations = new List<string>();

            if (Directory.Exists(sceneRoot))
            {
                foreach (string path in Directory.EnumerateFiles(sceneRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(path);
                    Match match = RuntimeNamespaceRegex.Match(source);
                    if (match.Success)
                        violations.Add(RelativePath(path) + " -> " + match.Value);
                }
            }

            Assert.IsEmpty(violations,
                "Scene/application source must consume subsystem APIs or Composition entry points, never " +
                "concrete Runtime namespaces.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void ShowcaseConcreteWorldOwnershipLivesInComposition()
        {
            string sceneRoot = Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase");
            string compositionRoot = Path.Combine(
                RepoRoot, "Assets", "Game", "Composition", "Showcase");
            string[] ownedFiles =
            {
                "ShowcaseWorld.cs",
                "ShowcaseWorld.StorageBridge.cs",
                "FarFieldStructureStore.cs",
                "ShowcaseCatalogue.cs",
                "ShowcaseHeightJob.cs",
            };
            var violations = new List<string>();

            foreach (string fileName in ownedFiles)
            {
                string compositionPath = Path.Combine(compositionRoot, fileName);
                if (!File.Exists(compositionPath))
                    violations.Add("missing Composition-owned file: " + RelativePath(compositionPath));

                string scenePath = Path.Combine(sceneRoot, fileName);
                if (File.Exists(scenePath))
                    violations.Add("concrete world owner leaked back into scene assembly: " + RelativePath(scenePath));

                string transitionalPath = Path.Combine(sceneRoot, "CompositionOwned", fileName);
                if (File.Exists(transitionalPath))
                    violations.Add("transitional CompositionOwned shim returned: " + RelativePath(transitionalPath));
            }

            string asmrefPath = Path.Combine(sceneRoot, "CompositionOwned", "VoxelEngine.Composition.asmref");
            if (File.Exists(asmrefPath))
                violations.Add("transitional Composition asmref returned: " + RelativePath(asmrefPath));

            Assert.IsEmpty(violations,
                "Concrete Showcase world wiring must be physically owned by Composition; the scene assembly " +
                "must remain an API-only application shell.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void ShowcaseWorldDoesNotOwnStructuresRuntimeDetails()
        {
            string path = Path.Combine(
                RepoRoot, "Assets", "Game", "Composition", "Showcase", "ShowcaseWorld.cs");
            Assert.IsTrue(File.Exists(path), "Missing Composition-owned ShowcaseWorld.cs");

            string source = File.ReadAllText(path);
            string[] forbidden =
            {
                "using VoxelEngine.Structures.Runtime;",
                "CastleBuilder.",
                "ArchFeatureDefinition",
                "PrimitiveRasteriser",
                "ProfileBlockStore",
                "RasterResult",
            };
            var violations = forbidden.Where(token =>
                source.IndexOf(token, StringComparison.Ordinal) >= 0).ToArray();

            Assert.IsEmpty(violations,
                "ShowcaseWorld may orchestrate Structures through Structures.Api/Composition contracts, " +
                "but concrete Structures.Runtime authoring state and algorithms must stay behind the " +
                "Composition Structures boundary.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void ShowcaseWorldDoesNotAllocateOrDisposePhysicalStorage()
        {
            string path = Path.Combine(
                RepoRoot, "Assets", "Game", "Composition", "Showcase", "ShowcaseWorld.cs");
            Assert.IsTrue(File.Exists(path), "Missing Composition-owned ShowcaseWorld: " + path);

            string source = File.ReadAllText(path);
            string[] forbidden =
            {
                "new RegionTable(",
                "new BrickPool(",
                "_table.Dispose()",
                "_pool.Dispose()",
            };
            var violations = forbidden
                .Where(token => source.IndexOf(token, StringComparison.Ordinal) >= 0)
                .ToArray();

            Assert.IsEmpty(violations,
                "ShowcaseWorld may borrow physical storage for Composition-owned hot paths, but allocation " +
                "and disposal must stay centralized in the shared Composition storage lifetime.\n\n" +
                string.Join("\n", violations));
            StringAssert.Contains("VoxelEngineBootstrap.StorageRuntimeLifetime", source);
        }

        [Test]
        public void DeletedCoreNamespaceDoesNotReappearInProductionSourceOrAsmdefs()
        {
            var violations = new List<string>();

            foreach (string pattern in new[] { "*.cs", "*.asmdef" })
            {
                foreach (string path in EnumerateProductionFiles(pattern))
                {
                    if (string.Equals(Path.GetExtension(path), ".asmdef", StringComparison.OrdinalIgnoreCase)
                        && IsTestAssembly(File.ReadAllText(path)))
                        continue;

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
                {
                    if (!IsNonProductionPath(path))
                        yield return path;
                }
            }
        }

        private static IEnumerable<string> ProductionRoots()
        {
            // Scan the whole application Assets tree, not just VoxelEngine/Showcase. Tests,
            // Editor-only code and CI harnesses are filtered by IsNonProductionPath/IsTestAssembly.
            yield return "Assets";
            yield return Path.Combine("Packages", "com.mountingforce.worldgen", "Runtime");
        }

        private static bool IsNonProductionPath(string path)
        {
            string relative = "/" + RelativePath(path).Replace('\\', '/') + "/";
            return relative.IndexOf("/Assets/Tests/", StringComparison.Ordinal) >= 0
                   || relative.IndexOf("/Assets/Editor/", StringComparison.Ordinal) >= 0
                   || relative.IndexOf("/Assets/VoxelEngine/CI/", StringComparison.Ordinal) >= 0
                   || relative.IndexOf("/Editor/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsTestAssembly(string json)
        {
            Match optionalReferences = OptionalUnityReferencesRegex.Match(json);
            if (!optionalReferences.Success)
                return false;

            return QuotedStringRegex.Matches(optionalReferences.Groups["value"].Value)
                .Cast<Match>()
                .Any(match => string.Equals(
                    match.Groups["value"].Value,
                    "TestAssemblies",
                    StringComparison.Ordinal));
        }

        private static IReadOnlyDictionary<string, string> BuildAsmdefNamesByGuid()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativeRoot in new[]
                     {
                         "Assets",
                         Path.Combine("Packages", "com.mountingforce.worldgen", "Runtime"),
                     })
            {
                string root = Path.Combine(RepoRoot, relativeRoot);
                if (!Directory.Exists(root))
                    continue;

                foreach (string metaPath in Directory.EnumerateFiles(
                             root,
                             "*.asmdef.meta",
                             SearchOption.AllDirectories))
                {
                    Match guidMatch = GuidRegex.Match(File.ReadAllText(metaPath));
                    if (!guidMatch.Success)
                        continue;

                    string asmdefPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                    if (!File.Exists(asmdefPath))
                        continue;

                    Match nameMatch = NameRegex.Match(File.ReadAllText(asmdefPath));
                    if (nameMatch.Success)
                        names[guidMatch.Groups["value"].Value] = nameMatch.Groups["value"].Value;
                }
            }

            return names;
        }

        private static string ResolveAssemblyReference(
            string reference,
            IReadOnlyDictionary<string, string> asmdefNamesByGuid)
        {
            const string prefix = "GUID:";
            if (!reference.StartsWith(prefix, StringComparison.Ordinal))
                return reference;

            string guid = reference.Substring(prefix.Length);
            return asmdefNamesByGuid.TryGetValue(guid, out string assemblyName)
                ? assemblyName
                : reference;
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
