using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarTerrainSharedTexturePresentationTests
    {
        private const string FarTerrainShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader";
        private const string FarTerrainSourcePath =
            "Assets/Scenes/Showcase/VoxelFarTerrain.cs";

        [Test]
        public void FarTerrainReusesVoxelSurfaceTextureSamplingContract()
        {
            string shader = File.ReadAllText(ProjectPath(FarTerrainShaderPath));
            string farTerrain = File.ReadAllText(ProjectPath(FarTerrainSourcePath));

            StringAssert.Contains("_materialIdsScratch", farTerrain,
                "Far terrain must retain the exact application-owned material ID instead of discarding it after resolving vertex albedo.");
            StringAssert.Contains("materialIds[i] = new Vector2(material, 0f)", farTerrain,
                "Every sampled far vertex must publish its exact semantic material ID alongside colour.");
            StringAssert.Contains("mesh.uv2 = materialIds", farTerrain,
                "Far terrain must publish material identity through a dedicated mesh channel rather than reconstructing it from interpolated RGB.");
            StringAssert.Contains("float2 materialData : TEXCOORD1", shader,
                "The far shader must read the dedicated per-vertex material channel.");
            StringAssert.Contains("nointerpolation float material : TEXCOORD2", shader,
                "Far triangles must use one exact material row instead of interpolating semantic IDs across fragments.");
            StringAssert.Contains("output.material = input.materialData.x", shader,
                "The vertex stage must forward the explicit material ID to the fragment stage.");
            StringAssert.Contains("uint material = min((uint)round(input.material), 31u)", shader,
                "The fragment stage must select the shared presentation row from the explicit material ID.");
            StringAssert.DoesNotContain("ResolveMaterialFromAlbedo", shader,
                "Interpolated vertex colour is presentation data, not a stable semantic material key.");
            StringAssert.Contains("float4 _MaterialSampling[32]", shader,
                "Far terrain must consume the shared material texture-layer and texture-weight policy.");
            StringAssert.Contains("float4 _MaterialSurface[32]", shader,
                "Far terrain must consume the shared material texture scale/triplanar policy.");
            StringAssert.Contains("TEXTURE2D_ARRAY(_AlbedoTextures)", shader,
                "Far terrain must sample the renderer-owned material texture array rather than inventing a second grass texture path.");
            StringAssert.Contains("SurfaceUV", shader,
                "Far terrain must use the same dominant-axis/world-space texture basis used by SmoothSurface.");
            StringAssert.Contains("input.positionWS / max(_VoxelSize, 1e-4)", shader,
                "Far terrain texture coordinates must be derived from world position in base-voxel units, independent of clipmap spacing.");
            StringAssert.Contains("hitDistance / 350.0", shader,
                "Far terrain must retain the same distance attenuation used by the near surface texture contribution at the handoff.");
        }

        [Test]
        public void SharedPresentationPublisherRunsBeforeOpaqueFarTerrain()
        {
            MethodInfo resolver = typeof(VoxelRenderFeature).GetMethod(
                "ResolveSurfacePassEvent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolver, Is.Not.Null,
                "The renderer must own one explicit scheduling rule for shared presentation state.");

            var lateConfigured = (RenderPassEvent)resolver.Invoke(
                null, new object[] { RenderPassEvent.BeforeRenderingTransparents });
            Assert.That(lateConfigured, Is.EqualTo(RenderPassEvent.BeforeRenderingOpaques),
                "A late configured near-surface pass would publish material tables only after ordinary far-terrain opaque draws.");

            var alreadyEarly = (RenderPassEvent)resolver.Invoke(
                null, new object[] { RenderPassEvent.AfterRenderingPrePasses });
            Assert.That(alreadyEarly, Is.EqualTo(RenderPassEvent.AfterRenderingPrePasses),
                "An intentionally earlier renderer event must not be delayed just to satisfy far-terrain consumers.");
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
