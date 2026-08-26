using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseLodPresentationTests
    {
        [Test]
        public void FlagshipShowcaseKeepsFullResolutionFineBand()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "VoxelShowcase.unity");
            Assert.That(File.Exists(scenePath), Is.True,
                "VoxelShowcase scene must exist for its presentation regression.");

            string yaml = File.ReadAllText(scenePath);
            Match match = Regex.Match(
                yaml, @"(?m)^\s*m_DetailBandScale:\s*([-+0-9.eE]+)\s*$");
            Assert.That(match.Success, Is.True,
                "VoxelShowcase must serialize its LOD detail-band scale explicitly.");

            float scale = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            const float configuredFineOuterMetres = 96f;
            float liveFineOuterMetres = configuredFineOuterMetres * scale;

            Assert.That(liveFineOuterMetres,
                Is.GreaterThanOrEqualTo(configuredFineOuterMetres - 0.001f),
                $"The flagship showcase shrinks its finest terrain band to "
              + $"{liveFineOuterMetres:F1} m (scale {scale:F2}). The saved SceneIssue 032832 "
              + "view shows the resulting coarse handoff in the marked mid-ground; keep the "
              + "full 96 m fine band for this visual acceptance scene.");
        }
    }
}
