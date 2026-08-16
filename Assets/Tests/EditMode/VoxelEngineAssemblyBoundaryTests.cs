using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Mechanical enforcement for docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md.
    /// New Api/Runtime assemblies obey the final rules immediately. Existing broad
    /// assemblies are allowed only through the exact, numbered migration exceptions below.
    /// </summary>
    public sealed class VoxelEngineAssemblyBoundaryTests
    {
        private sealed class AsmdefInfo
        {
            public string Name;
            public readonly List<string> References = new List<string>();
        }

        private sealed class LegacyException
        {
            public readonly string Assembly;
            public readonly string Reference;
            public readonly int RemovedByCutover;

            public LegacyException(string assembly, string reference, int removedByCutover)
            {
                Assembly = assembly;
                Reference = reference;
                RemovedByCutover = removedByCutover;
            }
        }

        // Exact dependencies present at the refactor baseline. Every entry must disappear
        // in the named cutover. The cutover is complete, so no legacy production leak remains
        // allowed. Never add an entry just to make CI green.
        private static readonly LegacyException[] LegacyExceptions = Array.Empty<LegacyException>();

        private static readonly HashSet<string> LegacyEngineAssemblies = new HashSet<string>(
            new[]
            {
                "VoxelEngine.Core",
                "VoxelEngine.Collision",
                "VoxelEngine.Net",
                "VoxelEngine.Rendering",
                "VoxelEngine.Streaming",
                "VoxelEngine.Structures",
                "VoxelEngine.Tiering",
                "VoxelEngine.Vegetation"
            },
            StringComparer.Ordinal);

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
        public void ApiAssembliesNeverReferenceRuntimeAssemblies()
        {
            var violations = LoadAsmdefs()
                .Where(a => a.Name.EndsWith(".Api", StringComparison.Ordinal))
                .SelectMany(a => a.References
                    .Where(IsRuntimeAssembly)
                    .Select(reference => $"{a.Name} -> {reference}"))
                .ToList();

            Assert.IsEmpty(violations,
                "Api assemblies must never reference Runtime assemblies.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void RuntimeAssembliesNeverReferenceForeignRuntimeAssemblies()
        {
            var violations = new List<string>();
            foreach (var asmdef in LoadAsmdefs().Where(a => IsRuntimeAssembly(a.Name)))
            {
                if (IsCompositionAssembly(asmdef.Name))
                    continue;

                string owner = SubsystemPrefix(asmdef.Name);
                violations.AddRange(asmdef.References
                    .Where(IsRuntimeAssembly)
                    // A composition assembly is the sanctioned wiring point, so depending on one
                    // is not a foreign-runtime reference even though its name ends in .Runtime.
                    // Game.Composition is split per area now (Campaign, Kentridge, ...), and a
                    // scene shell reaching a composition runtime is exactly the intended edge.
                    .Where(reference => !IsCompositionAssembly(reference))
                    .Where(reference => SubsystemPrefix(reference) != owner)
                    .Select(reference => $"{asmdef.Name} -> {reference}"));
            }

            Assert.IsEmpty(violations,
                "A Runtime assembly may not reference another subsystem's Runtime. " +
                "Composition is the sole production wiring exception.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void LegacyProductionLeaksAreExactAndCannotGrow()
        {
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asmdef in LoadAsmdefs().Where(IsProductionAssembly))
            {
                foreach (var reference in asmdef.References)
                {
                    if (LegacyEngineAssemblies.Contains(reference) && reference != asmdef.Name)
                        actual.Add(Key(asmdef.Name, reference));
                }
            }

            var allowed = new HashSet<string>(
                LegacyExceptions.Select(e => Key(e.Assembly, e.Reference)),
                StringComparer.Ordinal);

            var unexpected = actual.Except(allowed).OrderBy(key => key).ToList();
            Assert.IsEmpty(unexpected,
                "A new legacy architecture dependency appeared. Do not add an exception merely " +
                "to make this test green; route the dependency through the owning Api.\n\n" +
                string.Join("\n", unexpected));

            var stale = LegacyExceptions
                .Where(exception => !actual.Contains(Key(exception.Assembly, exception.Reference)))
                .Select(exception =>
                    $"{exception.Assembly} -> {exception.Reference} " +
                    $"(remove this stale exception; scheduled Cutover {exception.RemovedByCutover})")
                .ToList();

            Assert.IsEmpty(stale,
                "A legacy dependency has already disappeared. Tighten the migration allowlist now.\n\n" +
                string.Join("\n", stale));
        }

        [Test]
        public void StreamingRuntimeNeverReferencesNet()
        {
            var streaming = LoadAsmdefs()
                .FirstOrDefault(a => a.Name == "VoxelEngine.Streaming.Runtime");
            if (streaming == null)
                Assert.Ignore("Streaming.Runtime is created in Cutover 8; legacy Streaming is tracked explicitly.");

            var violations = streaming.References
                .Where(reference => reference == "VoxelEngine.Net" ||
                                    reference.StartsWith("VoxelEngine.Net.", StringComparison.Ordinal))
                .ToList();

            Assert.IsEmpty(violations,
                "Streaming is transport-agnostic. Net may call Streaming.Api; Streaming.Runtime must never call Net.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void SimulationRuntimeNeverReferencesRenderingRuntime()
        {
            var violations = LoadAsmdefs()
                .Where(a => IsRuntimeAssembly(a.Name) && a.Name != "VoxelEngine.Rendering.Runtime")
                .Where(a => !IsCompositionAssembly(a.Name))
                .Where(a => a.References.Contains("VoxelEngine.Rendering.Runtime"))
                .Select(a => $"{a.Name} -> VoxelEngine.Rendering.Runtime")
                .ToList();

            Assert.IsEmpty(violations,
                "Simulation/domain code must not depend on presentation implementation.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void NonCompositionProductionAssembliesNeverReferenceForeignRuntimes()
        {
            var violations = new List<string>();
            foreach (var asmdef in LoadAsmdefs().Where(IsProductionAssembly))
            {
                if (IsCompositionAssembly(asmdef.Name))
                    continue;

                string owner = SubsystemPrefix(asmdef.Name);
                violations.AddRange(asmdef.References
                    .Where(IsRuntimeAssembly)
                    // A composition assembly is the sanctioned wiring point, so depending on one
                    // is not a foreign-runtime reference even though its name ends in .Runtime.
                    // Game.Composition is split per area now (Campaign, Kentridge, ...), and a
                    // scene shell reaching a composition runtime is exactly the intended edge.
                    .Where(reference => !IsCompositionAssembly(reference))
                    .Where(reference => SubsystemPrefix(reference) != owner)
                    .Select(reference => $"{asmdef.Name} -> {reference}"));
            }

            Assert.IsEmpty(violations,
                "Only Composition may wire foreign Runtime assemblies. Production consumers use Api assemblies.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void WorldGenSemanticAssembliesDoNotDependOnVoxelEngine()
        {
            var asmdefs = LoadAsmdefs();
            foreach (var assemblyName in new[]
                     {
                         "MountingForce.WorldGen.Core",
                         "MountingForce.WorldGen.Architecture"
                     })
            {
                var asmdef = asmdefs.FirstOrDefault(a => a.Name == assemblyName);
                Assert.NotNull(asmdef, $"Missing expected worldgen assembly {assemblyName}.");

                var engineReferences = asmdef.References
                    .Where(reference => reference.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                    .ToList();
                Assert.IsEmpty(engineReferences,
                    $"{assemblyName} is semantic world generation and must remain independent of VoxelEngine.\n\n" +
                    string.Join("\n", engineReferences));
            }
        }

        [Test]
        public void WorldGenVoxelAdapterNeverReferencesVoxelEngineRuntime()
        {
            var adapter = LoadAsmdefs()
                .FirstOrDefault(a => a.Name == "MountingForce.WorldGen.Voxel");
            Assert.NotNull(adapter, "Missing MountingForce.WorldGen.Voxel adapter assembly.");

            var runtimeReferences = adapter.References
                .Where(reference => reference.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                .Where(IsRuntimeAssembly)
                .ToList();

            Assert.IsEmpty(runtimeReferences,
                "MountingForce.WorldGen.Voxel is an engine client and may depend only on VoxelEngine Api assemblies, never Runtime.\n\n" +
                string.Join("\n", runtimeReferences));
        }

        private static bool IsProductionAssembly(AsmdefInfo asmdef)
        {
            string name = asmdef.Name;
            return !name.Contains(".Tests") &&
                   !name.EndsWith(".Editor", StringComparison.Ordinal) &&
                   !name.Contains(".Editor.") &&
                   name != "VoxelEngine.CI" &&
                   !name.StartsWith("VoxelEngine.CI.", StringComparison.Ordinal) &&
                   !name.StartsWith("VoxelEngine.Tools", StringComparison.Ordinal);
        }

        private static bool IsRuntimeAssembly(string assemblyName) =>
            (assemblyName.StartsWith("VoxelEngine.", StringComparison.Ordinal)
             || assemblyName.StartsWith("Game.", StringComparison.Ordinal))
            && assemblyName.EndsWith(".Runtime", StringComparison.Ordinal);

        private static bool IsCompositionAssembly(string assemblyName) =>
            string.Equals(assemblyName, "VoxelEngine.Composition", StringComparison.Ordinal)
            || string.Equals(assemblyName, "Game.Composition", StringComparison.Ordinal)
            || assemblyName.StartsWith("Game.Composition.", StringComparison.Ordinal);

        private static string SubsystemPrefix(string assemblyName)
        {
            if (!assemblyName.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                return assemblyName;

            string[] parts = assemblyName.Split('.');
            return parts.Length >= 2
                ? string.Join(".", parts.Take(2))
                : assemblyName;
        }

        private static string Key(string assembly, string reference) =>
            assembly + " -> " + reference;

        private static List<AsmdefInfo> LoadAsmdefs()
        {
            var files = new List<string>();
            string assets = Path.Combine(RepoRoot, "Assets");
            if (Directory.Exists(assets))
                files.AddRange(Directory.EnumerateFiles(assets, "*.asmdef", SearchOption.AllDirectories));

            string worldgen = Path.Combine(RepoRoot, "Packages", "com.mountingforce.worldgen");
            if (Directory.Exists(worldgen))
                files.AddRange(Directory.EnumerateFiles(worldgen, "*.asmdef", SearchOption.AllDirectories));

            var guidToAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                string name = ParseName(File.ReadAllText(file));
                string meta = file + ".meta";
                if (name == null || !File.Exists(meta))
                    continue;

                Match guid = Regex.Match(File.ReadAllText(meta), @"(?m)^guid:\s*([0-9a-fA-F]+)\s*$");
                if (guid.Success)
                    guidToAssembly[guid.Groups[1].Value] = name;
            }

            var result = new List<AsmdefInfo>();
            foreach (var file in files)
            {
                string json = File.ReadAllText(file);
                string name = ParseName(json);
                if (string.IsNullOrEmpty(name))
                    continue;

                var info = new AsmdefInfo { Name = name };
                Match block = Regex.Match(json, @"""references""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                if (block.Success)
                {
                    foreach (Match match in Regex.Matches(block.Groups[1].Value, @"""([^""]+)"""))
                    {
                        string reference = match.Groups[1].Value;
                        if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                        {
                            string guid = reference.Substring("GUID:".Length);
                            string resolved;
                            if (guidToAssembly.TryGetValue(guid, out resolved))
                                reference = resolved;
                        }
                        info.References.Add(reference);
                    }
                }
                result.Add(info);
            }

            return result;
        }

        private static string ParseName(string json)
        {
            Match name = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
            return name.Success ? name.Groups[1].Value : null;
        }
    }
}
