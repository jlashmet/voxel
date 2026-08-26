using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarTerrainSharedTexturePresentationTests
    {
        private const string FarTerrainShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader";

        [Test]
        public void FarTerrainReusesVoxelSurfaceTextureSamplingContract()
        {
            string shader = File.ReadAllText(ProjectPath(FarTerrainShaderPath));

            StringAssert.Contains("ResolveMaterialFromAlbedo", shader,
                "Far terrain must recover the semantic material represented by its authoritative vertex albedo instead of treating interpolated colour as a second grass-texturing system.");
            StringAssert.Contains("float4 _MaterialAlbedo[32]", shader,
                "Far terrain must resolve material identity from the same authoritative albedo table as the voxel surface.");
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

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
