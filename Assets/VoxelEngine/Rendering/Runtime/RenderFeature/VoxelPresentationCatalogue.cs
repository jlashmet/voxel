using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>Projection used by a material row. The shader interprets the value generically.</summary>
    public enum VoxelTextureProjection : byte
    {
        Face = 0,
        Triplanar = 1,
    }

    /// <summary>
    /// GPU-facing presentation data for one base material. IDs select rows; no material identity
    /// is compiled into the shader.
    /// </summary>
    public readonly struct VoxelMaterialPresentation
    {
        public readonly Vector4 Albedo;
        public readonly Vector4 Sampling;  // albedo layer, normal layer, projection, texture blend
        public readonly Vector4 Surface;   // UV scale, normal strength, roughness, luminance-only
        public readonly Vector4 Variation; // luminance pivot, detail, chroma, macro variation

        public VoxelMaterialPresentation(Color albedo, int albedoLayer = 0, int normalLayer = 0,
            VoxelTextureProjection projection = VoxelTextureProjection.Face,
            float textureBlend = 0f, float uvScale = 1f / 36f, float normalStrength = 0f,
            float roughness = 0.76f, bool luminanceOnly = false, float luminancePivot = 0.68f,
            float detailStrength = 0f, float chromaStrength = 0f, float macroVariation = 0f)
        {
            Albedo = new Vector4(albedo.r, albedo.g, albedo.b, 1f);
            Sampling = new Vector4(albedoLayer, normalLayer, (float)projection, textureBlend);
            Surface = new Vector4(uvScale, normalStrength, roughness, luminanceOnly ? 1f : 0f);
            Variation = new Vector4(luminancePivot, detailStrength, chromaStrength, macroVariation);
        }
    }

    /// <summary>Presentation data for a coating row, independent from structural material.</summary>
    public readonly struct VoxelCoatingPresentation
    {
        public readonly Vector4 Tint;
        public readonly Vector4 Sampling; // texture layer, UV scale, texture weight, blend strength
        public readonly Vector4 Response; // vertical floor, vertical ceiling, noise, roughness

        public VoxelCoatingPresentation(Color tint, int textureLayer = 0,
            float uvScale = 1f / 28f, float textureWeight = 0f, float blendStrength = 0f,
            float verticalFloor = 1f, float verticalCeiling = 1f,
            float noiseStrength = 0f, float roughness = -1f)
        {
            Tint = new Vector4(tint.r, tint.g, tint.b, 1f);
            Sampling = new Vector4(textureLayer, uvScale, textureWeight, blendStrength);
            Response = new Vector4(verticalFloor, verticalCeiling, noiseStrength, roughness);
        }
    }

    /// <summary>Optional shading pattern associated with a reconstruction style row.</summary>
    public readonly struct VoxelSurfacePresentation
    {
        public readonly Vector4 Pattern; // enabled, course height, block width, strength
        public readonly Vector4 JointColour;
        public readonly Vector4 DetailResponse; // seam colour, roughness, piece variation, seam width

        public VoxelSurfacePresentation(bool enabled, float courseHeight = 5f,
            float blockWidth = 9f, float strength = 0f, Color jointColour = default,
            float detailColourBlend = 0f, float detailRoughness = -1f,
            float detailVariation = 0f, float detailWidth = 1f)
        {
            Pattern = new Vector4(enabled ? 1f : 0f, courseHeight, blockWidth, strength);
            JointColour = new Vector4(jointColour.r, jointColour.g, jointColour.b, 1f);
            DetailResponse = new Vector4(detailColourBlend, detailRoughness,
                                         detailVariation, detailWidth);
        }
    }

    /// <summary>
    /// Renderer-owned GPU lookup storage. Base-material rows intentionally start neutral: the
    /// application/game installs its semantic-free <c>MaterialPresentationDefinition</c> rows
    /// through Composition. Rendering therefore knows how to render material properties but not
    /// which game materials exist or which opaque index means stone, wood, water, and so on.
    ///
    /// Coating and reconstruction-style presentation remain code-backed here as a separate
    /// migration concern. Texture arrays are still assembled from the render feature's existing
    /// serialized texture sources.
    /// </summary>
    public static class VoxelPresentationCatalogue
    {
        public const int MaxMaterials = 32;
        public const int MaxCoatings = 16;
        public const int MaxSurfaceStyles = 32;

        // Existing texture-array layer used by the renderer-owned moss coating presentation.
        // Base-material texture-layer selection is game-owned and contains no named constants here.
        private const int MossCoatingTextureLayer = 5;

        public static readonly Vector4[] MaterialAlbedo = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialSampling = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialSurface = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialVariation = new Vector4[MaxMaterials];
        public static readonly Vector4[] CoatingTint = new Vector4[MaxCoatings];
        public static readonly Vector4[] CoatingSampling = new Vector4[MaxCoatings];
        public static readonly Vector4[] CoatingResponse = new Vector4[MaxCoatings];
        public static readonly Vector4[] SurfacePattern = new Vector4[MaxSurfaceStyles];
        public static readonly Vector4[] SurfaceJointColour = new Vector4[MaxSurfaceStyles];
        public static readonly Vector4[] SurfaceDetailResponse = new Vector4[MaxSurfaceStyles];

        static VoxelPresentationCatalogue()
        {
            // Every material row is a valid neutral slot. Game/application composition replaces
            // the rows it owns before they are rendered; no game semantic identity lives here.
            for (int i = 0; i < MaxMaterials; i++)
                SetMaterial(i, new VoxelMaterialPresentation(Color.white));

            SetCoating(0, new VoxelCoatingPresentation(Color.white));
            // Layer 5 is the same authored grass artwork used by the ground material. Keep its
            // physical texel density identical so crossing a coating boundary changes moss tint,
            // not the apparent size of the grass blades and flowers.
            SetCoating(1, new VoxelCoatingPresentation(new Color(0.25f, 0.39f, 0.12f),
                MossCoatingTextureLayer, 1f / 7f, 0.86f, 0.66f, 0.03f, 1f, 0.12f, 0.72f));
            SetCoating(2, new VoxelCoatingPresentation(new Color(0.88f, 0.91f, 0.94f),
                blendStrength: 0.88f, verticalFloor: 0f, verticalCeiling: 1f, roughness: 0.72f));
            SetCoating(3, new VoxelCoatingPresentation(new Color(0.08f, 0.07f, 0.06f),
                blendStrength: 0.58f, noiseStrength: 0.18f, roughness: 0.9f));
            SetCoating(4, new VoxelCoatingPresentation(new Color(0.12f, 0.20f, 0.23f),
                blendStrength: 0.30f, roughness: 0.18f));
            // HDR-hot noisy overlay used while a flammable voxel is actively burning.
            SetCoating(5, new VoxelCoatingPresentation(new Color(2.8f, 0.42f, 0.025f),
                blendStrength: 0.94f, verticalFloor: 0.78f, verticalCeiling: 1f,
                noiseStrength: 0.38f, roughness: 0.12f));

            SetSurface(5, new VoxelSurfacePresentation(false, 5f, 9f, 0f,
                new Color(0.34f, 0.31f, 0.24f), detailColourBlend: 0.48f,
                detailRoughness: 0.94f, detailVariation: 0.18f, detailWidth: 0.62f));
        }

        private static void SetMaterial(int id, in VoxelMaterialPresentation value)
        {
            MaterialAlbedo[id] = value.Albedo;
            MaterialSampling[id] = value.Sampling;
            MaterialSurface[id] = value.Surface;
            MaterialVariation[id] = value.Variation;
        }

        private static void SetCoating(int id, in VoxelCoatingPresentation value)
        {
            CoatingTint[id] = value.Tint;
            CoatingSampling[id] = value.Sampling;
            CoatingResponse[id] = value.Response;
        }

        private static void SetSurface(int id, in VoxelSurfacePresentation value)
        {
            SurfacePattern[id] = value.Pattern;
            SurfaceJointColour[id] = value.JointColour;
            SurfaceDetailResponse[id] = value.DetailResponse;
        }

        public static Texture2DArray BuildTextureArray(Texture2D[] sources, bool linear)
        {
            Texture2D first = Array.Find(sources, texture => texture != null);
            if (first == null) return null;
            // Runtime normalization is a migration bridge for the existing separately serialized
            // textures. Cap it so eight uncompressed 2K layers per channel do not quietly consume
            // hundreds of MB. A later import pipeline can bake compressed arrays without changing
            // the catalogue or shader contract.
            int width = Mathf.Min(first.width, 1024);
            int height = Mathf.Min(first.height, 1024);
            var array = new Texture2DArray(width, height, sources.Length,
                TextureFormat.RGBA32, false, linear)
            {
                name = linear ? "Voxel normal texture array" : "Voxel albedo texture array",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32, linear ? RenderTextureReadWrite.Linear
                                                  : RenderTextureReadWrite.sRGB);
            try
            {
                for (int layer = 0; layer < sources.Length; layer++)
                {
                    Texture source = sources[layer] != null ? sources[layer] : first;
                    Graphics.Blit(source, temporary);
                    Graphics.CopyTexture(temporary, 0, 0, array, layer, 0);
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temporary);
            }
            return array;
        }
    }
}
