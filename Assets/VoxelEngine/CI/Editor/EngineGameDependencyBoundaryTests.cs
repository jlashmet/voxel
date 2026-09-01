using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Protects engine/game ownership and repository module API boundaries.
    /// </summary>
    public sealed class EngineGameDependencyBoundaryTests
    {
        private static readonly Regex LegacyStructureMaterialDeclaration = new(
            @"\b(?:static\s+)?class\s+Mat\b|\bstruct\s+StructureMaterialSet\b",
            RegexOptions.Compiled);

        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            public string name;
            public string[] references;
        }

        private sealed class AssemblyInfo
        {
            public string Name;
            public string Path;
            public string ModuleRoot;
            public string Role;
            public string[] References;
        }

        [Test]
        public void VoxelEngineAssemblies_DoNotReferenceGameAssemblies()
        {
            string voxelEngineRoot = Path.Combine(Application.dataPath, "VoxelEngine");
            string[] asmdefs = Directory.GetFiles(
                voxelEngineRoot, "*.asmdef", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string contents = File.ReadAllText(asmdefs[i]);
                if (!contents.Contains("\"Game.")) continue;

                string relative = asmdefs[i].Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add("Assets/" + relative);
            }

            Assert.That(violations, Is.Empty,
                "VoxelEngine assemblies must not depend on Game assemblies. Move semantic/content " +
                "ownership to Assets/Game and depend on VoxelEngine APIs instead.\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void StructuresModule_DoesNotReintroduceLegacySemanticMaterialTypes()
        {
            string structuresRoot = Path.Combine(Application.dataPath, "VoxelEngine", "Structures");
            string[] sources = Directory.GetFiles(structuresRoot, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            for (int i = 0; i < sources.Length; i++)
            {
                string contents = File.ReadAllText(sources[i]);
                Match match = LegacyStructureMaterialDeclaration.Match(contents);
                if (!match.Success) continue;

                string relative = sources[i].Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                violations.Add($"Assets/{relative}: {match.Value}");
            }

            Assert.That(violations, Is.Empty,
                "VoxelEngine.Structures must not own semantic material facade/types. Game content " +
                "owns material identity; engine structure code consumes opaque indices and generic " +
                "material properties instead.\n" + string.Join("\n", violations));
        }

        [Test]
        public void ProductionModules_ReferenceOtherModulesThroughApiOnly()
        {
            Dictionary<string, AssemblyInfo> assemblies = LoadRepositoryAssemblies();
            var violations = new List<string>();

            foreach (AssemblyInfo source in assemblies.Values)
            {
                if (!IsOrdinaryProduction(source)) continue;

                for (int i = 0; i < source.References.Length; i++)
                {
                    string reference = source.References[i];
                    if (!assemblies.TryGetValue(reference, out AssemblyInfo target)) continue;
                    if (IsAllowedProductionReference(source, target)) continue;

                    violations.Add(
                        $"{source.Name} ({source.Path}) -> {target.Name} ({target.Path}): " +
                        "production modules must depend on the target Api, or move concrete Runtime wiring to Composition.");
                }
            }

            Assert.That(violations, Is.Empty,
                "Repository production modules contain cross-module implementation dependencies:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionModuleRule_RejectsCrossModuleRuntimeAndAcceptsApi()
        {
            AssemblyInfo source = Fixture(
                "Game.Structures.Runtime",
                "Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef");
            AssemblyInfo runtime = Fixture(
                "VoxelEngine.Structures.Runtime",
                "Assets/VoxelEngine/Structures/Runtime/VoxelEngine.Structures.Runtime.asmdef");
            AssemblyInfo api = Fixture(
                "VoxelEngine.Structures.Api",
                "Assets/VoxelEngine/Structures/Api/VoxelEngine.Structures.Api.asmdef");

            Assert.That(IsAllowedProductionReference(source, runtime), Is.False);
            Assert.That(IsAllowedProductionReference(source, api), Is.True);
        }

        [Test]
        public void ProductionModuleRule_AllowsSameModuleImplementationAndCompositionRuntimeWiring()
        {
            AssemblyInfo source = Fixture(
                "Game.Structures.Runtime",
                "Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef");
            AssemblyInfo sameModule = Fixture(
                "Game.Structures.Private",
                "Assets/Game/Structures/Runtime/Private/Game.Structures.Private.asmdef");
            AssemblyInfo composition = Fixture(
                "Game.Composition.Showcase",
                "Assets/Game/Composition/Showcase/Game.Composition.Showcase.asmdef");
            AssemblyInfo runtime = Fixture(
                "VoxelEngine.Structures.Runtime",
                "Assets/VoxelEngine/Structures/Runtime/VoxelEngine.Structures.Runtime.asmdef");

            Assert.That(IsAllowedProductionReference(source, sameModule), Is.True);
            Assert.That(IsAllowedProductionReference(composition, runtime), Is.True);
        }

        private static Dictionary<string, AssemblyInfo> LoadRepositoryAssemblies()
        {
            var result = new Dictionary<string, AssemblyInfo>(StringComparer.Ordinal);
            LoadAssembliesUnder(Path.Combine(Application.dataPath, "Game"), result);
            LoadAssembliesUnder(Path.Combine(Application.dataPath, "VoxelEngine"), result);
            return result;
        }

        private static void LoadAssembliesUnder(
            string root,
            Dictionary<string, AssemblyInfo> result)
        {
            if (!Directory.Exists(root)) return;
            string[] asmdefs = Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories);
            for (int i = 0; i < asmdefs.Length; i++)
            {
                AssemblyDefinitionJson json = JsonUtility.FromJson<AssemblyDefinitionJson>(
                    File.ReadAllText(asmdefs[i]));
                if (json == null || string.IsNullOrEmpty(json.name)) continue;

                string relative = "Assets/" + asmdefs[i]
                    .Substring(Application.dataPath.Length + 1)
                    .Replace('\\', '/');
                AssemblyInfo info = Fixture(json.name, relative);
                info.References = json.references ?? Array.Empty<string>();
                result[info.Name] = info;
            }
        }

        private static AssemblyInfo Fixture(string name, string path)
        {
            return new AssemblyInfo
            {
                Name = name,
                Path = path,
                ModuleRoot = ModuleRoot(path),
                Role = AssemblyRole(path),
                References = Array.Empty<string>(),
            };
        }

        private static string ModuleRoot(string path)
        {
            string[] segments = path.Replace('\\', '/').Split('/');
            if (segments.Length < 3 || segments[0] != "Assets") return string.Empty;
            if (segments[1] != "Game" && segments[1] != "VoxelEngine") return string.Empty;
            return $"Assets/{segments[1]}/{segments[2]}";
        }

        private static string AssemblyRole(string path)
        {
            string moduleRoot = ModuleRoot(path);
            if (string.IsNullOrEmpty(moduleRoot)) return string.Empty;
            string remainder = path.Substring(moduleRoot.Length).TrimStart('/');
            int slash = remainder.IndexOf('/');
            return slash < 0 ? string.Empty : remainder.Substring(0, slash);
        }

        private static bool IsOrdinaryProduction(AssemblyInfo assembly)
        {
            if (string.IsNullOrEmpty(assembly.ModuleRoot)) return false;
            if (IsTestOrEditor(assembly)) return false;
            if (IsComposition(assembly)) return false;
            return true;
        }

        private static bool IsAllowedProductionReference(AssemblyInfo source, AssemblyInfo target)
        {
            if (string.IsNullOrEmpty(source.ModuleRoot) || string.IsNullOrEmpty(target.ModuleRoot))
                return true;
            if (source.ModuleRoot == target.ModuleRoot) return true;
            if (IsTestOrEditor(source) || IsComposition(source)) return true;
            if (target.Role == "Api") return true;

            // Foundation is the deliberately tiny shared-primitives module. It is not a generic bypass:
            // only references whose target module is exactly VoxelEngine/Foundation are exempted.
            if (target.ModuleRoot == "Assets/VoxelEngine/Foundation") return true;
            return false;
        }

        private static bool IsComposition(AssemblyInfo assembly)
        {
            return assembly.ModuleRoot == "Assets/Game/Composition" ||
                   assembly.Role == "Composition" ||
                   assembly.Path.Contains("/Composition/");
        }

        private static bool IsTestOrEditor(AssemblyInfo assembly)
        {
            return assembly.Role == "Tests" || assembly.Role == "Editor" ||
                   assembly.Path.Contains("/Tests/") || assembly.Path.Contains("/Editor/") ||
                   assembly.Name.EndsWith(".Tests", StringComparison.Ordinal) ||
                   assembly.Name.Contains(".Tests.") ||
                   assembly.Name.EndsWith(".Editor", StringComparison.Ordinal) ||
                   assembly.Name.Contains(".Editor.");
        }
    }
}
