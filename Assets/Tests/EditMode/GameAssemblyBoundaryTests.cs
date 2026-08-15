using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GameAssemblyBoundaryTests
    {
        private static readonly Regex ReferencesBlock = new Regex(
            "\\\"references\\\"\\s*:\\s*\\[(.*?)\\]",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex JsonString = new Regex(
            "\\\"([^\\\"]+)\\\"",
            RegexOptions.Compiled);

        [Test]
        public void GameApiAssembliesNeverReferenceRuntimeAssemblies()
        {
            foreach (string path in GameAsmdefs().Where(IsApiAssemblyPath))
            {
                foreach (string reference in References(path))
                {
                    Assert.That(reference.EndsWith(".Runtime", StringComparison.Ordinal), Is.False,
                        path + " is an Api assembly and may not reference Runtime assembly '" + reference + "'.");
                }
            }
        }

        [Test]
        public void GameRuntimeAssembliesReferenceOtherGameSystemsOnlyThroughApis()
        {
            foreach (string path in GameAsmdefs().Where(IsRuntimeAssemblyPath))
            {
                foreach (string reference in References(path).Where(value => value.StartsWith("Game.", StringComparison.Ordinal)))
                {
                    Assert.That(reference.EndsWith(".Api", StringComparison.Ordinal), Is.True,
                        path + " is Runtime code and references Game assembly '" + reference +
                        "'. Cross-system Game dependencies must go through Api assemblies.");
                }
            }
        }

        [Test]
        public void GameContentAssembliesDoNotReachIntoRuntime()
        {
            foreach (string path in GameAsmdefs().Where(path =>
                         !IsApiAssemblyPath(path) && !IsRuntimeAssemblyPath(path)))
            {
                foreach (string reference in References(path))
                {
                    Assert.That(reference.EndsWith(".Runtime", StringComparison.Ordinal), Is.False,
                        path + " is production content and may not wire Runtime assembly '" + reference + "'.");
                }
            }
        }

        [Test]
        public void CurrentGameApiDependencyDirectionHasNoReverseEdges()
        {
            Dictionary<string, string[]> referencesByName = GameAsmdefs()
                .Where(IsApiAssemblyPath)
                .ToDictionary(AssemblyName, path => References(path).ToArray(), StringComparer.Ordinal);

            Assert.That(referencesByName["Game.Cutscenes.Api"], Is.Empty,
                "Cutscenes is the lowest-level authored choreography contract and must not know about campaign/world/story systems.");

            Assert.That(referencesByName["Game.WorldBuilder.Api"],
                Is.EqualTo(new[] { "Game.Cutscenes.Api" }),
                "WorldBuilder may consume cutscene definitions but Cutscenes must remain independent of WorldBuilder.");

            Assert.That(referencesByName["Game.Story.Api"],
                Is.EqualTo(new[] { "Game.WorldBuilder.Api" }),
                "Story runtime contracts consume compiled campaign identities; WorldBuilder must not depend on Story runtime contracts.");
        }

        private static IEnumerable<string> GameAsmdefs()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Game");
            return Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static bool IsApiAssemblyPath(string path) =>
            Normalize(path).Contains("/Api/", StringComparison.Ordinal);

        private static bool IsRuntimeAssemblyPath(string path) =>
            Normalize(path).Contains("/Runtime/", StringComparison.Ordinal);

        private static string Normalize(string path) => path.Replace('\\', '/');

        private static string AssemblyName(string path)
        {
            string text = File.ReadAllText(path);
            Match match = Regex.Match(text, "\\\"name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            if (!match.Success)
                throw new InvalidOperationException("Assembly definition has no name: " + path);
            return match.Groups[1].Value;
        }

        private static IEnumerable<string> References(string path)
        {
            string text = File.ReadAllText(path);
            Match block = ReferencesBlock.Match(text);
            if (!block.Success)
                yield break;

            foreach (Match match in JsonString.Matches(block.Groups[1].Value))
                yield return match.Groups[1].Value;
        }
    }
}
