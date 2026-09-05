using System;
using UnityEngine;
using VoxelEngine.Rendering.Api;

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
        public readonly Vector4 Sampling;
        public readonly Vector4 Surface;
        public readonly Vector4 Variation;

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
        public readonly Vector4 Sampling;
        public readonly Vector4 Response;

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
        public readonly Vector4 Pattern;
        public readonly Vector4 JointColour;
        public readonly Vector4 DetailResponse;

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
    /// Water classification is likewise derived solely from installed presentation rows.
    /// </summary>
    public static class VoxelPresentationCatalogue
    {
        public const int MaxMaterials = 32;
        public const int MaxCoatings = 16;
        public const int MaxSurfaceStyles = 32;

        private const int MossCoatingTextureLayer = 5;
        private const int BuiltInSurfaceTextureLayerCount = 8;

        private static Texture2D[] s_AdditionalAlbedoLayers = Array.Empty<Texture2D>();
        private static Texture2D[] s_AdditionalNormalLayers = Array.Empty<Texture2D>();

        public static readonly Vector4[] MaterialAlbedo = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialSampling = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialSurface = new Vector4[MaxMaterials];
        public static readonly Vector4[] MaterialVariation = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterShallow = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterDeep = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterMotion = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterDetail = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterFoam = new Vector4[MaxMaterials];
        public static readonly Vector4[] WaterCascade = new Vector4[MaxMaterials];
        public static uint WaterMaterialMask { get; private set; }
        public static readonly Vector4[] CoatingTint = new Vector4[MaxCoatings];
        public static readonly Vector4[] CoatingSampling = new Vector4[MaxCoatings];
        public static readonly Vector4[] CoatingResponse = new Vector4[MaxCoatings];
        public static readonly Vector4[] SurfacePattern = new Vector4[MaxSurfaceStyles];
        public static readonly Vector4[] SurfaceJointColour = new Vector4[MaxSurfaceStyles];
        public static readonly Vector4[] SurfaceDetailResponse = new Vector4[MaxSurfaceStyles];

        /// <summary>Number of semantic-free extra texture slots configured by the active renderer asset.</summary>
        public static int AdditionalTextureLayerCount => s_AdditionalAlbedoLayers.Length;

        static VoxelPresentationCatalogue()
        {
            for (int i = 0; i < MaxMaterials; i++)
                SetMaterial(i, new VoxelMaterialPresentation(Color.white));

            SetCoating(0, new VoxelCoatingPresentation(Color.white));
            SetCoating(1, new VoxelCoatingPresentation(new Color(0.25f, 0.39f, 0.12f),
                MossCoatingTextureLayer, 1f / 7f, 0f, 0.66f, 0.03f, 1f, 0.12f, 0.72f));
            SetCoating(2, new VoxelCoatingPresentation(new Color(0.88f, 0.91f, 0.94f),
                blendStrength: 0.88f, verticalFloor: 0f, verticalCeiling: 1f, roughness: 0.72f));
            SetCoating(3, new VoxelCoatingPresentation(new Color(0.08f, 0.07f, 0.06f),
                blendStrength: 0.58f, noiseStrength: 0.18f, roughness: 0.9f));
            SetCoating(4, new VoxelCoatingPresentation(new Color(0.12f, 0.20f, 0.23f),
                blendStrength: 0.30f, roughness: 0.18f));
            SetCoating(5, new VoxelCoatingPresentation(new Color(2.8f, 0.42f, 0.025f),
                blendStrength: 0.94f, verticalFloor: 0.78f, verticalCeiling: 1f,
                noiseStrength: 0.38f, roughness: 0.12f));

            SetSurface(1, new VoxelSurfacePresentation(false, jointColour: new Color(0.18f, 0.16f, 0.14f),
                detailColourBlend: 0.10f, detailRoughness: 0.92f,
                detailVariation: 0.08f, detailWidth: 0.42f));

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

        internal static void ResetWater()
        {
            WaterMaterialMask = 0u;
            Array.Clear(WaterShallow, 0, WaterShallow.Length);
            Array.Clear(WaterDeep, 0, WaterDeep.Length);
            Array.Clear(WaterMotion, 0, WaterMotion.Length);
            Array.Clear(WaterDetail, 0, WaterDetail.Length);
            Array.Clear(WaterFoam, 0, WaterFoam.Length);
            Array.Clear(WaterCascade, 0, WaterCascade.Length);
        }

        internal static void SetWater(int id, in WaterPresentationDefinition value)
        {
            if (!value.IsWater) return;
            WaterMaterialMask |= 1u << id;
            WaterShallow[id] = ToVector4(value.Shallow);
            WaterDeep[id] = ToVector4(value.Deep);
            WaterMotion[id] = ToVector4(value.Motion);
            WaterDetail[id] = ToVector4(value.Detail);
            WaterFoam[id] = ToVector4(value.Foam);
            WaterCascade[id] = ToVector4(value.Cascade);
        }

        public static bool IsWaterMaterial(byte materialIndex) =>
            materialIndex < MaxMaterials && (WaterMaterialMask & (1u << materialIndex)) != 0;

        private static Vector4 ToVector4(Unity.Mathematics.float4 value) =>
            new(value.x, value.y, value.z, value.w);

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

        /// <summary>
        /// Installs optional extra texture layers for the active renderer asset. Layer positions are
        /// deliberately semantic-free; application material definitions own the opaque layer numbers.
        /// </summary>
        public static void ConfigureAdditionalTextureLayers(Texture2D[] albedo, Texture2D[] normals)
        {
            int count = albedo?.Length ?? 0;
            if (BuiltInSurfaceTextureLayerCount + count > MaxMaterials)
                throw new ArgumentOutOfRangeException(nameof(albedo),
                    $"Surface texture layer count exceeds renderer capacity {MaxMaterials}.");

            s_AdditionalAlbedoLayers = new Texture2D[count];
            s_AdditionalNormalLayers = new Texture2D[count];
            if (count > 0)
                Array.Copy(albedo, s_AdditionalAlbedoLayers, count);
            if (normals != null && count > 0)
                Array.Copy(normals, s_AdditionalNormalLayers, Math.Min(count, normals.Length));
        }

        public static Texture2DArray BuildTextureArray(Texture2D[] sources, bool linear)
        {
            Texture2D[] additional = linear ? s_AdditionalNormalLayers : s_AdditionalAlbedoLayers;
            int baseCount = sources?.Length ?? 0;
            int totalCount = baseCount + additional.Length;
            if (totalCount == 0) return null;

            var combined = new Texture2D[totalCount];
            if (baseCount > 0)
                Array.Copy(sources, combined, baseCount);
            if (additional.Length > 0)
                Array.Copy(additional, 0, combined, baseCount, additional.Length);

            Texture2D first = Array.Find(combined, texture => texture != null);
            if (first == null) return null;
            int width = Mathf.Min(first.width, 1024);
            int height = Mathf.Min(first.height, 1024);
            var array = new Texture2DArray(width, height, combined.Length,
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
                for (int layer = 0; layer < combined.Length; layer++)
                {
                    Texture source = combined[layer] != null ? combined[layer] : first;
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
