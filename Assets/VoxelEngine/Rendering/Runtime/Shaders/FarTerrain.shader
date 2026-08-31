Shader "VoxelEngine/FarTerrain"
{
    // Shading for the clipmap rings that stand in for the voxel world beyond the streaming
    // radius. Vertex colour carries the same application-owned material-family albedo used by
    // the near voxel presentation. Additional surface character is evaluated from deterministic
    // world-space coordinates so it is independent from clipmap vertex spacing and cannot swim
    // when a ring snaps around the camera.
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
        _AerialColour ("Aerial Perspective", Color) = (0.62, 0.72, 0.86, 1)
        _AerialDistance ("Aerial Full Distance", Float) = 9000
        _MacroScaleMetres ("Macro Variation Scale", Float) = 72
        _MacroStrength ("Macro Colour Strength", Range(0, 0.25)) = 0.10
        _DetailScaleMetres ("Detail Normal Scale", Float) = 14
        _DetailNormalStrength ("Detail Normal Strength", Range(0, 0.35)) = 0.16
        _DetailFadeStart ("Detail Fade Start", Float) = 1500
        _DetailFadeEnd ("Detail Fade End", Float) = 6500
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float4 _AerialColour;
            float _AerialDistance;
            float _MacroScaleMetres;
            float _MacroStrength;
            float _DetailScaleMetres;
            float _DetailNormalStrength;
            float _DetailFadeStart;
            float _DetailFadeEnd;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            float AxisSignal(float2 p)
            {
                // Two phase-shifted waves avoid a grid-aligned checker while remaining much
                // cheaper than texture-backed triplanar sampling. Coordinates are absolute world
                // metres, so clipmap origin changes cannot alter the signal.
                return sin(p.x + p.y * 0.73) * 0.58
                     + sin(p.y * 1.37 - p.x * 0.41) * 0.42;
            }

            float WorldTriplanarSignal(float3 positionWS, float3 normalWS, float scaleMetres)
            {
                float invScale = rcp(max(1.0, scaleMetres));
                float3 w = abs(normalWS);
                w = max(w, 0.001);
                w /= (w.x + w.y + w.z);
                float yz = AxisSignal(positionWS.yz * invScale * 6.2831853);
                float xz = AxisSignal(positionWS.xz * invScale * 6.2831853);
                float xy = AxisSignal(positionWS.xy * invScale * 6.2831853);
                return yz * w.x + xz * w.y + xy * w.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 geometricNormal = normalize(input.normalWS);
                float distanceMetres = length(input.positionWS - _WorldSpaceCameraPos);

                // Macro variation is intentionally much finer than the outer clipmap triangles,
                // but still landscape-scale. It modulates the already-selected semantic material
                // family instead of inventing a second far-only material identity.
                float macro = WorldTriplanarSignal(
                    input.positionWS, geometricNormal, _MacroScaleMetres);
                float macroLuminance = 1.0 + macro * _MacroStrength;
                float3 baseColour = input.color.rgb * macroLuminance;

                // Presentation-only detail normal. Its phase is absolute world space; distance
                // filtering removes the high-frequency response before it aliases at kilometre
                // range. This does not modify geometry, collision, or authoritative terrain.
                float detailFade = 1.0 - smoothstep(
                    _DetailFadeStart, max(_DetailFadeStart + 1.0, _DetailFadeEnd), distanceMetres);
                float detail = WorldTriplanarSignal(
                    input.positionWS, geometricNormal, _DetailScaleMetres);
                float3 detailVector = float3(
                    sin(detail * 2.31 + input.positionWS.z / max(1.0, _DetailScaleMetres)),
                    sin(detail * 1.73 + input.positionWS.x / max(1.0, _DetailScaleMetres)),
                    sin(detail * 2.07 + input.positionWS.y / max(1.0, _DetailScaleMetres)));
                detailVector -= geometricNormal * dot(detailVector, geometricNormal);
                float3 n = normalize(
                    geometricNormal + detailVector * (_DetailNormalStrength * detailFade));

                float3 sun = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, sun));
                float skyT = saturate(n.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);
                float3 lit = baseColour * (ambient * 0.42 + (0.34 + ndl * 0.66));

                // Aerial perspective progressively dominates the farthest rings so the outer
                // geometric sample rate does not become a high-contrast horizon artifact.
                float haze = saturate(distanceMetres / max(1.0, _AerialDistance));
                haze = haze * haze * 0.82;
                lit = lerp(lit, _AerialColour.rgb, haze);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
