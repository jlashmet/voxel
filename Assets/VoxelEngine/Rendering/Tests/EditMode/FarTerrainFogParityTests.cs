using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DetailedTerrainTintRegressionTests
    {
        private const string SmoothShader = "VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader";
        private const string FarShader = "VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader";

        [Test]
        [Category("Rendering")]
        public void DetailedTerrainDoesNotBlendSkyColourByCameraDistance()
        {
            string smooth = ReadShader(SmoothShader);
            string far = ReadShader(FarShader);

            StringAssert.DoesNotContain("smoothstep(60.0, 300.0", smooth,
                "Detailed terrain must not tint itself toward the sky by camera distance.");
            StringAssert.DoesNotContain("distanceFog *=", smooth,
                "Detailed terrain must not retain the removed camera-distance fog path.");
            StringAssert.DoesNotContain("lit = lerp(lit, SkyColour(viewDirection), saturate(distanceFog));", smooth,
                "Detailed terrain must keep material/lighting colour instead of a blue sky blend.");
            StringAssert.Contains("SkyColour(normal)", smooth,
                "Removing the distance tint must not remove normal-oriented sky ambient lighting.");

            StringAssert.Contains("float haze = saturate(distance / max(1.0, _AerialDistance));", far,
                "Far terrain must retain its intentional long-range aerial perspective.");
            StringAssert.Contains("haze = haze * haze * 0.82;", far,
                "Far terrain must retain its native long-range haze shaping.");
            StringAssert.DoesNotContain("smoothstep(60.0, 300.0", far,
                "The removed detailed-terrain tint must not be propagated to far terrain.");
        }

        private static string ReadShader(string assetRelativePath)
        {
            string path = Path.Combine(Application.dataPath, assetRelativePath);
            Assert.That(File.Exists(path), Is.True, $"Missing shader source at {path}");
            return File.ReadAllText(path);
        }
    }
}
