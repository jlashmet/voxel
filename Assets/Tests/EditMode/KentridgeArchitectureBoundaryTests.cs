using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeArchitectureBoundaryTests
    {
        private const uint Seed = 0x4B454E54u;

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
                var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                    directory = directory.Parent;

                Assert.NotNull(directory, "Could not locate project root containing Packages/.");
                return directory.FullName;
            }
        }

        private static string WorldGenRuntimeRoot => Path.Combine(
            RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime");

        [Test]
        public void KentridgeSemanticSourcesDoNotReferenceVoxelEngineNamespaces()
        {
            string[] semanticKentridgeRoots =
            {
                Path.Combine(WorldGenRuntimeRoot, "Content", "Kentridge"),
                Path.Combine(WorldGenRuntimeRoot, "Architecture", "Kentridge"),
            };
            var violations = new List<string>();

            foreach (string root in semanticKentridgeRoots)
            {
                Assert.IsTrue(Directory.Exists(root), "Missing Kentridge semantic source root: " + root);
                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(path);
                    if (source.IndexOf("VoxelEngine.", StringComparison.Ordinal) >= 0)
                        violations.Add(Path.GetRelativePath(WorldGenRuntimeRoot, path));
                }
            }

            Assert.IsEmpty(violations,
                "Kentridge settlement/content and architecture semantics must remain engine-independent; " +
                "only the WorldGen.Voxel adapter may consume VoxelEngine contracts.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void KentridgeAssemblyDefinitionsKeepEngineDependenciesAtVoxelApiBoundary()
        {
            string[] semanticAsmdefs =
            {
                "MountingForce.WorldGen.Core.asmdef",
                Path.Combine("Architecture", "MountingForce.WorldGen.Architecture.asmdef"),
            };
            var violations = new List<string>();

            foreach (string relativeAsmdef in semanticAsmdefs)
            {
                foreach (string reference in ReadReferences(relativeAsmdef))
                {
                    if (reference.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                        violations.Add(relativeAsmdef + " -> " + reference);
                }
            }

            string voxelAsmdef = Path.Combine("Voxel", "MountingForce.WorldGen.Voxel.asmdef");
            string[] engineReferences = ReadReferences(voxelAsmdef)
                .Where(reference => reference.StartsWith("VoxelEngine.", StringComparison.Ordinal))
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();
            string[] allowedEngineReferences =
            {
                "VoxelEngine.Storage.Api",
                "VoxelEngine.Structures.Api",
                "VoxelEngine.Terrain.Api",
                "VoxelEngine.Vegetation.Api",
            };

            CollectionAssert.AreEquivalent(
                allowedEngineReferences,
                engineReferences,
                "The Kentridge/WorldGen voxel realization boundary must consume only the explicitly " +
                "approved engine API assemblies. Adding a new engine dependency requires an intentional " +
                "architecture decision and guard update.");

            foreach (string reference in engineReferences)
            {
                if (!reference.EndsWith(".Api", StringComparison.Ordinal))
                    violations.Add(voxelAsmdef + " -> non-Api engine reference " + reference);
            }

            Assert.IsEmpty(violations,
                "Kentridge semantic assemblies must not reference VoxelEngine, and the Voxel adapter " +
                "must never depend on engine Runtime/Core implementation assemblies.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void KentridgeContentSuppliesIntentAndArchitectureOwnsDetail()
        {
            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(KentridgeDefinition).Assembly.GetName().Name,
                "Kentridge settlement planning must remain in the high-level Core/content assembly.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(StructureIntent).Assembly.GetName().Name,
                "The settlement-to-architecture handoff contract belongs to Core.");
            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(UrbanFabricIntent).Assembly.GetName().Name,
                "Anonymous frontage intent belongs to the high-level Core assembly.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(ArchitectureCompiler).Assembly.GetName().Name,
                "Named-building roof/window/facade generation must live in Architecture.");
            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(StructureForm).Assembly.GetName().Name,
                "Detailed named-building forms must live below settlement planning.");
            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(UrbanFabricCompiler).Assembly.GetName().Name,
                "Anonymous frontage detail generation must live in Architecture.");
            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(UrbanFabricForm).Assembly.GetName().Name,
                "Anonymous detailed forms must live below settlement planning.");

            string[] coreReferences = typeof(KentridgeDefinition).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(
                coreReferences,
                "MountingForce.WorldGen.Architecture",
                "The high-level Kentridge/Core assembly must never depend downward on architectural detail.");
        }

        [Test]
        public void StructureIntentContainsHighLevelConstraintsAndNoArchitecturalDetail()
        {
            string[] fields = typeof(StructureIntent)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "RoleId", "StyleId", "Archetype", "District",
                    "PositionDm", "Frontage", "EnvelopeDm"
                },
                fields,
                "StructureIntent should describe what/where/how-large, not roofs, windows or facade modules.");

            CollectionAssert.DoesNotContain(fields, "Roof");
            CollectionAssert.DoesNotContain(fields, "WindowStyle");
            CollectionAssert.DoesNotContain(fields, "Storeys");
            CollectionAssert.DoesNotContain(fields, "ChimneyOnRight");
        }

        [Test]
        public void UrbanFabricIntentContainsMassingConstraintsAndNoLocalDetail()
        {
            string[] fields = typeof(UrbanFabricIntent)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "StyleId", "District", "MinStoreys", "MaxStoreys",
                    "EnvelopeDm", "VariationContext"
                },
                fields,
                "UrbanFabricIntent should carry urban hierarchy/massing, not architectural realization.");

            CollectionAssert.DoesNotContain(fields, "Roof");
            CollectionAssert.DoesNotContain(fields, "WindowTreatment");
            CollectionAssert.DoesNotContain(fields, "HasAwning");
            CollectionAssert.DoesNotContain(fields, "ChimneyOnRight");
            CollectionAssert.DoesNotContain(fields, "AnnexOnRight");
            CollectionAssert.DoesNotContain(fields, "WidthDm");
            CollectionAssert.DoesNotContain(fields, "DepthDm");
        }

        [Test]
        public void ArchitectureCompilerPreservesIntentIdentityAndOwnsLocalForm()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            int generated = 0;
            int bespoke = 0;

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);

                Assert.AreEqual(plot.RoleId, intent.RoleId);
                Assert.AreEqual(KentridgeDefinition.Id, intent.StyleId);
                Assert.AreEqual(plot.Archetype, intent.Archetype);
                Assert.AreEqual(plot.District, intent.District);
                Assert.AreEqual(plot.PositionDm.X, intent.PositionDm.X);
                Assert.AreEqual(plot.PositionDm.Y, intent.PositionDm.Y);
                Assert.AreEqual(plot.Frontage, intent.Frontage);
                Assert.AreEqual(KentridgeDefinition.FootprintDm(plot.Archetype).X, intent.EnvelopeDm.X);
                Assert.AreEqual(KentridgeDefinition.FootprintDm(plot.Archetype).Z, intent.EnvelopeDm.Z);

                Assert.AreEqual(intent.RoleId, form.RoleId);
                Assert.AreEqual(intent.Archetype, form.Archetype);
                Assert.AreEqual(intent.District, form.District);

                if (form.IsGenerated)
                {
                    generated++;
                    ArchitectureCompiler.ValidateGenerated(intent, plan.Theme, form);
                    Assert.Greater(form.WidthDm, 0);
                    Assert.Greater(form.DepthDm, 0);
                    Assert.Greater(form.Storeys, 0);
                }
                else bespoke++;
            }

            Assert.AreEqual(13, generated);
            Assert.AreEqual(4, bespoke);
        }

        [Test]
        public void UrbanFabricCompilerOwnsAnonymousLocalFormInsideRunMassing()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);
            int samples = 0;

            for (int runIndex = 0; runIndex < plan.FrontageRuns.Count; runIndex++)
            {
                KentridgeFrontageRun run = plan.FrontageRuns[runIndex];
                UrbanFabricIntent intent = KentridgeDefinition.UrbanFabricIntent(run);

                Assert.AreEqual(KentridgeDefinition.Id, intent.StyleId);
                Assert.AreEqual(run.District, intent.District);
                Assert.AreEqual(run.MinStoreys, intent.MinStoreys);
                Assert.AreEqual(run.MaxStoreys, intent.MaxStoreys);
                Assert.AreEqual(KentridgeDefinition.AnonymousFabricEnvelopeDm, intent.EnvelopeDm);
                Assert.AreEqual((int)run.Band, intent.VariationContext);

                for (int siteIndex = 0; siteIndex < 3; siteIndex++)
                {
                    UrbanFabricForm form = UrbanFabricCompiler.Resolve(
                        intent, Seed, runIndex, siteIndex);
                    UrbanFabricCompiler.Validate(intent, form);
                    Assert.That(form.Storeys, Is.InRange(intent.MinStoreys, intent.MaxStoreys));
                    Assert.Greater(form.WidthDm, 0);
                    Assert.Greater(form.DepthDm, 0);
                    Assert.Greater(form.RoofHeightDm, 0);
                    samples++;
                }
            }

            Assert.Greater(samples, 0);
        }

        private static IReadOnlyList<string> ReadReferences(string relativeAsmdef)
        {
            string path = Path.Combine(
                WorldGenRuntimeRoot,
                relativeAsmdef.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "Missing WorldGen asmdef: " + path);

            string json = File.ReadAllText(path);
            Match block = ReferencesRegex.Match(json);
            if (!block.Success)
                return new string[0];

            return QuotedStringRegex.Matches(block.Groups["value"].Value)
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value)
                .ToArray();
        }
    }
}
