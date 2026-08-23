using UnityEngine;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    public enum VegetationShaderClass : byte
    {
        Foliage,
        Surface,
        Vine,
        Woody,
    }

    public readonly struct VegetationRenderStyle
    {
        public readonly VegetationShaderClass ShaderClass;
        public readonly Color BaseColor;
        public readonly Color SecondaryColor;
        public readonly Color EmissionColor;
        public readonly float EmissionStrength;
        public readonly float Shape;
        public readonly float WindStrength;

        public VegetationRenderStyle(
            VegetationShaderClass shaderClass,
            Color baseColor,
            Color secondaryColor,
            Color emissionColor,
            float emissionStrength,
            float shape,
            float windStrength)
        {
            ShaderClass = shaderClass;
            BaseColor = baseColor;
            SecondaryColor = secondaryColor;
            EmissionColor = emissionColor;
            EmissionStrength = emissionStrength;
            Shape = shape;
            WindStrength = windStrength;
        }
    }

    /// <summary>
    /// Shared shader/material bridge for lightweight vegetation. Species stay semantic; the renderer
    /// maps them onto a small number of growth-form shaders so GPU batching remains practical.
    /// </summary>
    public static class ProceduralVegetationMaterials
    {
        public const string FoliageShaderName = "VoxelEngine/ProceduralVegetationFoliage";
        public const string SurfaceShaderName = "VoxelEngine/ProceduralVegetationSurface";
        public const string VineShaderName = "VoxelEngine/ProceduralVine";

        private static Material s_Foliage;
        private static Material s_Surface;
        private static Material s_Vine;
        private static bool s_ReportedMissing;

        public static bool Ensure()
        {
            if (s_Foliage != null && s_Surface != null && s_Vine != null)
                return true;

            Shader foliage = Shader.Find(FoliageShaderName);
            Shader surface = Shader.Find(SurfaceShaderName);
            Shader vine = Shader.Find(VineShaderName);
            if (foliage == null || surface == null || vine == null)
            {
                // Once, not per call: see ProceduralTreeMaterials.Ensure for what per-call
                // logging costs when a spawn path retries this every frame.
                if (!s_ReportedMissing)
                {
                    s_ReportedMissing = true;
                    if (foliage == null) Debug.LogError($"Vegetation shader was not found: {FoliageShaderName}");
                    if (surface == null) Debug.LogError($"Vegetation shader was not found: {SurfaceShaderName}");
                    if (vine == null) Debug.LogError($"Vegetation shader was not found: {VineShaderName}");
                }
                return false;
            }

            s_ReportedMissing = false;

            s_Foliage = Create(foliage, "Procedural Vegetation Foliage (Shared Runtime)");
            s_Surface = Create(surface, "Procedural Vegetation Surface (Shared Runtime)");
            s_Vine = Create(vine, "Procedural Vine (Shared Runtime)");
            return true;
        }

        public static Material MaterialFor(VegetationKind kind)
        {
            VegetationRenderStyle style = StyleFor(kind);
            if (style.ShaderClass == VegetationShaderClass.Woody)
                return ProceduralTreeMaterials.Bark;

            if (!Ensure()) return null;
            switch (style.ShaderClass)
            {
                case VegetationShaderClass.Surface: return s_Surface;
                case VegetationShaderClass.Vine: return s_Vine;
                default: return s_Foliage;
            }
        }

        public static void Configure(MaterialPropertyBlock block, VegetationKind kind)
        {
            if (block == null) return;
            VegetationRenderStyle style = StyleFor(kind);
            block.SetColor("_BaseColor", style.BaseColor);
            block.SetColor("_TipColor", style.SecondaryColor);
            block.SetColor("_SecondaryColor", style.SecondaryColor);
            block.SetColor("_EmissionColor", style.EmissionColor);
            block.SetFloat("_EmissionStrength", style.EmissionStrength);
            block.SetFloat("_Shape", style.Shape);
            block.SetFloat("_WindStrength", style.WindStrength);
            block.SetFloat("_Leafiness", style.ShaderClass == VegetationShaderClass.Vine ? 0.62f : 0.35f);
        }

        public static void ApplyLighting()
        {
            if (!Ensure()) return;
            ApplyLighting(s_Foliage);
            ApplyLighting(s_Surface);
            ApplyLighting(s_Vine);
        }

        public static VegetationRenderStyle StyleFor(VegetationKind kind)
        {
            VegetationProfile profile = VegetationCatalogue.Get(kind);
            VegetationShaderClass shaderClass = Classify(kind, profile.GrowthForm);
            float shape = ShapeFor(kind, profile.GrowthForm);
            Color baseColor = BaseColorFor(kind, profile.Traits);
            Color secondary = SecondaryColorFor(kind, baseColor);
            Color emission = Color.black;
            float emissionStrength = 0f;

            if ((profile.Traits & VegetationTraits.Luminous) != 0)
            {
                emission = EmissionColorFor(kind);
                emissionStrength = 1.6f;
            }

            float wind = shaderClass == VegetationShaderClass.Vine ? 0.14f : 0.22f;
            if ((profile.Traits & VegetationTraits.Woody) != 0) wind *= 0.45f;
            if ((profile.Traits & VegetationTraits.Dead) != 0) wind *= 0.65f;

            return new VegetationRenderStyle(shaderClass, baseColor, secondary, emission, emissionStrength, shape, wind);
        }

        private static VegetationShaderClass Classify(VegetationKind kind, VegetationGrowthForm growthForm)
        {
            switch (kind)
            {
                case VegetationKind.Moss:
                case VegetationKind.FallenLeaves:
                case VegetationKind.PineNeedles:
                case VegetationKind.Lichen:
                case VegetationKind.LilyPad:
                case VegetationKind.Algae:
                case VegetationKind.TrunkMoss:
                case VegetationKind.StarMoss:
                    return VegetationShaderClass.Surface;
                case VegetationKind.FallenLog:
                case VegetationKind.ExposedRoot:
                case VegetationKind.DeadBranch:
                case VegetationKind.DanglingRoot:
                    return VegetationShaderClass.Woody;
            }

            // Ivy is catalogued as a climber, so keep it on the branched vine path. Treating it as
            // a surface creeper turns each semantic ivy instance into a large circular wall patch.
            switch (growthForm)
            {
                case VegetationGrowthForm.Creeper: return VegetationShaderClass.Surface;
                case VegetationGrowthForm.Climber:
                case VegetationGrowthForm.Hanger: return VegetationShaderClass.Vine;
                case VegetationGrowthForm.Root:
                case VegetationGrowthForm.Debris: return VegetationShaderClass.Woody;
                default: return VegetationShaderClass.Foliage;
            }
        }

        private static float ShapeFor(VegetationKind kind, VegetationGrowthForm growthForm)
        {
            if (kind == VegetationKind.Flower || kind == VegetationKind.ManaBloom) return 2f;
            if (growthForm == VegetationGrowthForm.Fungus) return 3f;
            if (growthForm == VegetationGrowthForm.Shrub) return 4f;
            if (growthForm == VegetationGrowthForm.Frond) return 1f;
            return 0f;
        }

        private static Color BaseColorFor(VegetationKind kind, VegetationTraits traits)
        {
            if ((traits & VegetationTraits.Dead) != 0) return new Color(0.34f, 0.27f, 0.14f, 1f);
            switch (kind)
            {
                case VegetationKind.Flower: return new Color(0.84f, 0.28f, 0.46f, 1f);
                case VegetationKind.Mushroom: return new Color(0.58f, 0.34f, 0.20f, 1f);
                case VegetationKind.BerryBush: return new Color(0.20f, 0.39f, 0.13f, 1f);
                case VegetationKind.ThornBush: return new Color(0.18f, 0.31f, 0.10f, 1f);
                case VegetationKind.Ivy:
                case VegetationKind.Vine:
                case VegetationKind.ClimbingVine:
                case VegetationKind.HangingVine:
                    return new Color(0.28f, 0.55f, 0.10f, 1f);
                case VegetationKind.Lichen: return new Color(0.48f, 0.54f, 0.27f, 1f);
                case VegetationKind.Algae: return new Color(0.12f, 0.31f, 0.20f, 1f);
                case VegetationKind.Glowshroom: return new Color(0.30f, 0.30f, 0.62f, 1f);
                case VegetationKind.ManaBloom: return new Color(0.35f, 0.28f, 0.82f, 1f);
                case VegetationKind.CrystalShrub: return new Color(0.23f, 0.58f, 0.66f, 1f);
                case VegetationKind.WispReed: return new Color(0.22f, 0.55f, 0.46f, 1f);
                case VegetationKind.MoonFern: return new Color(0.26f, 0.42f, 0.59f, 1f);
                case VegetationKind.EmberThorn: return new Color(0.55f, 0.20f, 0.08f, 1f);
                case VegetationKind.StarMoss: return new Color(0.18f, 0.47f, 0.40f, 1f);
                case VegetationKind.ArcaneVine: return new Color(0.25f, 0.20f, 0.58f, 1f);
                default: return new Color(0.17f, 0.42f, 0.12f, 1f);
            }
        }

        private static Color SecondaryColorFor(VegetationKind kind, Color baseColor)
        {
            switch (kind)
            {
                case VegetationKind.Flower: return new Color(1.00f, 0.73f, 0.28f, 1f);
                case VegetationKind.Ivy:
                case VegetationKind.Vine:
                case VegetationKind.ClimbingVine:
                case VegetationKind.HangingVine:
                    return new Color(0.53f, 0.72f, 0.18f, 1f);
                case VegetationKind.ManaBloom: return new Color(0.72f, 0.58f, 1.00f, 1f);
                case VegetationKind.Glowshroom: return new Color(0.55f, 0.84f, 1.00f, 1f);
                case VegetationKind.EmberThorn: return new Color(0.96f, 0.44f, 0.12f, 1f);
                default: return Color.Lerp(baseColor, Color.white, 0.22f);
            }
        }

        private static Color EmissionColorFor(VegetationKind kind)
        {
            switch (kind)
            {
                case VegetationKind.EmberThorn: return new Color(1.00f, 0.20f, 0.03f, 1f);
                case VegetationKind.ManaBloom:
                case VegetationKind.ArcaneVine: return new Color(0.40f, 0.25f, 1.00f, 1f);
                case VegetationKind.Glowshroom: return new Color(0.20f, 0.75f, 1.00f, 1f);
                default: return new Color(0.25f, 1.00f, 0.72f, 1f);
            }
        }

        private static Material Create(Shader shader, string name)
        {
            return new Material(shader)
            {
                name = name,
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
        }

        private static void ApplyLighting(Material material)
        {
            Vector3 sun = VoxelRenderBridge.SunDirection;
            material.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0f));
            material.SetColor("_SkyHorizon", VoxelRenderBridge.SkyHorizon);
            material.SetColor("_SkyZenith", VoxelRenderBridge.SkyZenith);
        }
    }
}
