using UnityEngine;
using VoxelEngine.AmbientLife.Api;

namespace VoxelEngine.Rendering.Runtime.AmbientLife
{
    public enum AmbientVisualShape : byte
    {
        Mote = 0,
        Butterfly = 1,
        CompactInsect = 2,
        Dragonfly = 3,
        GroundInsect = 4,
        Frog = 5,
        BirdOrBat = 6,
        Spore = 7,
        Wisp = 8,
        Emberfly = 9,
    }

    public readonly struct AmbientLifeRenderStyle
    {
        public readonly AmbientVisualShape Shape;
        public readonly Color BaseColor;
        public readonly Color SecondaryColor;
        public readonly Color EmissionColor;
        public readonly float EmissionStrength;
        public readonly float FlutterSpeed;
        public readonly float SizeMetres;

        public AmbientLifeRenderStyle(
            AmbientVisualShape shape,
            Color baseColor,
            Color secondaryColor,
            Color emissionColor,
            float emissionStrength,
            float flutterSpeed,
            float sizeMetres)
        {
            Shape = shape;
            BaseColor = baseColor;
            SecondaryColor = secondaryColor;
            EmissionColor = emissionColor;
            EmissionStrength = emissionStrength;
            FlutterSpeed = flutterSpeed;
            SizeMetres = sizeMetres;
        }
    }

    /// <summary>
    /// One instanced billboard shader covers ambient insects, motes, birds and magical wisps.
    /// Shape, colour and emissive behaviour are supplied per population batch rather than through
    /// one prefab/material per species.
    /// </summary>
    public static class ProceduralAmbientLifeMaterials
    {
        public const string ShaderName = "VoxelEngine/ProceduralAmbientLife";
        private static Material s_Shared;

        public static Material Shared
        {
            get { Ensure(); return s_Shared; }
        }

        public static bool Ensure()
        {
            if (s_Shared != null) return true;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Ambient-life shader was not found: {ShaderName}");
                return false;
            }

            s_Shared = new Material(shader)
            {
                name = "Procedural Ambient Life (Shared Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            return true;
        }

        public static void Configure(MaterialPropertyBlock block, AmbientLifeKind kind)
        {
            if (block == null) return;
            AmbientLifeRenderStyle style = StyleFor(kind);
            block.SetColor("_BaseColor", style.BaseColor);
            block.SetColor("_SecondaryColor", style.SecondaryColor);
            block.SetColor("_EmissionColor", style.EmissionColor);
            block.SetFloat("_EmissionStrength", style.EmissionStrength);
            block.SetFloat("_Shape", (float)style.Shape);
            block.SetFloat("_FlutterSpeed", style.FlutterSpeed);
        }

        public static void ApplyLighting()
        {
            if (!Ensure()) return;
            Vector3 sun = VoxelRenderBridge.SunDirection;
            s_Shared.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0f));
            s_Shared.SetColor("_SkyHorizon", VoxelRenderBridge.SkyHorizon);
            s_Shared.SetColor("_SkyZenith", VoxelRenderBridge.SkyZenith);
        }

        public static AmbientLifeRenderStyle StyleFor(AmbientLifeKind kind)
        {
            // Keep luminous populations semantically HDR (strength > 1) while preserving the
            // visually validated headroom. Emission colours are pre-scaled so colour * strength
            // is identical to the lower-strength headroom tuning used by the animation captures.
            switch (kind)
            {
                case AmbientLifeKind.Butterfly:
                    return S(AmbientVisualShape.Butterfly, C(0.95f, 0.58f, 0.18f), C(0.22f, 0.12f, 0.05f), 0f, 8f, 0.18f);
                case AmbientLifeKind.Bee:
                    return S(AmbientVisualShape.CompactInsect, C(0.92f, 0.64f, 0.12f), C(0.16f, 0.11f, 0.05f), 0f, 10f, 0.10f);
                case AmbientLifeKind.Moth:
                    return S(AmbientVisualShape.Butterfly, C(0.63f, 0.59f, 0.48f), C(0.30f, 0.27f, 0.22f), 0f, 7f, 0.16f);
                case AmbientLifeKind.Dragonfly:
                    return S(AmbientVisualShape.Dragonfly, C(0.18f, 0.52f, 0.58f), C(0.08f, 0.20f, 0.24f), 0f, 12f, 0.20f);
                case AmbientLifeKind.Beetle:
                    return S(AmbientVisualShape.GroundInsect, C(0.25f, 0.15f, 0.07f), C(0.08f, 0.06f, 0.04f), 0f, 3f, 0.09f);
                case AmbientLifeKind.Cricket:
                    return S(AmbientVisualShape.GroundInsect, C(0.28f, 0.31f, 0.12f), C(0.09f, 0.10f, 0.05f), 0f, 5f, 0.10f);
                case AmbientLifeKind.Frog:
                    return S(AmbientVisualShape.Frog, C(0.22f, 0.48f, 0.18f), C(0.10f, 0.23f, 0.08f), 0f, 3f, 0.28f);
                case AmbientLifeKind.Songbird:
                    return S(AmbientVisualShape.BirdOrBat, C(0.46f, 0.35f, 0.20f), C(0.16f, 0.12f, 0.08f), 0f, 6f, 0.34f);
                case AmbientLifeKind.Bat:
                    return S(AmbientVisualShape.BirdOrBat, C(0.16f, 0.13f, 0.18f), C(0.05f, 0.04f, 0.06f), 0f, 9f, 0.34f);
                case AmbientLifeKind.SporeMote:
                    return S(AmbientVisualShape.Spore, C(0.78f, 0.74f, 0.54f), C(0.50f, 0.46f, 0.34f), 0f, 1.5f, 0.055f);
                case AmbientLifeKind.GlowMoth:
                    return S(AmbientVisualShape.Butterfly, C(0.45f, 0.66f, 0.86f), C(0.18f, 0.25f, 0.42f), 3.0f, 7f, 0.18f, C(0.080000f, 0.192000f, 0.266667f));
                case AmbientLifeKind.Wisp:
                    return S(AmbientVisualShape.Wisp, C(0.35f, 0.70f, 0.85f), C(0.18f, 0.30f, 0.45f), 4.2f, 2.4f, 0.30f, C(0.044643f, 0.133929f, 0.178571f));
                case AmbientLifeKind.Emberfly:
                    return S(AmbientVisualShape.Emberfly, C(0.90f, 0.28f, 0.06f), C(0.40f, 0.07f, 0.02f), 4.0f, 11f, 0.12f, C(0.225000f, 0.040500f, 0.004500f));
                case AmbientLifeKind.ManaButterfly:
                    return S(AmbientVisualShape.Butterfly, C(0.48f, 0.34f, 0.94f), C(0.20f, 0.12f, 0.45f), 3.5f, 8f, 0.20f, C(0.136000f, 0.085000f, 0.242857f));
                case AmbientLifeKind.SeedLight:
                    return S(AmbientVisualShape.Wisp, C(0.76f, 0.92f, 0.60f), C(0.30f, 0.48f, 0.20f), 3.4f, 1.8f, 0.10f, C(0.113235f, 0.205882f, 0.086471f));
                case AmbientLifeKind.Firefly:
                default:
                    return S(AmbientVisualShape.Mote, C(0.86f, 0.82f, 0.34f), C(0.30f, 0.28f, 0.08f), 4.5f, 3f, 0.065f, C(0.116111f, 0.122222f, 0.034222f));
            }
        }

        private static AmbientLifeRenderStyle S(
            AmbientVisualShape shape,
            Color baseColor,
            Color secondary,
            float emissionStrength,
            float flutter,
            float size,
            Color? emission = null)
        {
            return new AmbientLifeRenderStyle(
                shape,
                baseColor,
                secondary,
                emission ?? Color.black,
                emissionStrength,
                flutter,
                size);
        }

        private static Color C(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }
    }
}
