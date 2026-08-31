// VISUAL REFERENCE ARTIFACT FOR SCENE ISSUE
// 20260829-034812-000-WaterRenderingShowcaseStylizedShaderIntegration
//
// This is a concrete Unity/URP adaptation of the waterfall look approved in the
// inline WebGL prototype. It belongs with the ticket as reference source.
//
// IMPORTANT: Do not ship this as a scene-local second water renderer. The feature
// implementation must port/adapt the relevant behavior into the canonical shared
// VoxelEngine water renderer/profile system so WorldBuilder-authored waterfalls use
// the same production water path as lakes and rivers.
//
// Visual behavior to preserve:
// - strong downward directional flow, not horizontal/lake-style panning
// - multi-scale turbulent breakup and narrow bright vertical streaks
// - deep teal body color with brighter aerated whitewater
// - localized edge foam and impact foam near the waterfall base
// - irregular/wobbling sheet edges rather than a perfect rectangular curtain
// - lower-fall mist/spray impression
// - configurable speed, turbulence, foam and opacity

Shader "SceneIssueReference/WaterfallReference"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.025, 0.20, 0.27, 0.86)
        _MidColor ("Mid Water", Color) = (0.10, 0.48, 0.62, 0.90)
        _BrightColor ("Aerated Water", Color) = (0.66, 0.90, 0.94, 0.96)
        _FoamColor ("Foam", Color) = (0.82, 0.96, 0.97, 1.0)
        _FlowSpeed ("Flow Speed", Range(0.2, 3.0)) = 1.35
        _Turbulence ("Turbulence", Range(0.0, 1.6)) = 0.72
        _FoamStrength ("Foam Strength", Range(0.0, 2.0)) = 1.10
        _MistStrength ("Mist Strength", Range(0.0, 1.5)) = 0.75
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.20)) = 0.055
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.90
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterfallReference"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _MidColor;
                half4 _BrightColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _Turbulence;
                float _FoamStrength;
                float _MistStrength;
                float _EdgeSoftness;
                float _Opacity;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int octave = 0; octave < 5; octave++)
                {
                    value += amplitude * ValueNoise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // UV convention for the reference mesh:
                // x = across waterfall sheet, y = top (1) to bottom (0).
                float2 uv = input.uv;
                float2 p = uv * 2.0 - 1.0;
                float t = _Time.y * _FlowSpeed;

                // Irregular sheet edges and a small centerline wobble.
                float edgeNoise = (Fbm(float2(p.y * 1.7 - t * 0.16, 4.0)) - 0.5)
                                * 0.11 * _Turbulence;
                float centerWobble = (Fbm(float2(p.y * 2.0 - t * 0.13, 8.0)) - 0.5)
                                   * 0.075 * _Turbulence;
                float halfWidth = 0.88 + edgeNoise;
                float sheetDistance = abs(p.x - centerWobble);
                float body = 1.0 - smoothstep(halfWidth, halfWidth + _EdgeSoftness, sheetDistance);

                // Strong downward flow: increasing time advances toward decreasing UV.y.
                float2 flowUv = float2(p.x * 5.0, p.y * 4.0 + t * 2.8);
                float warp = Fbm(float2(p.x * 2.8, p.y * 2.0 + t * 0.65));
                flowUv.x += (warp - 0.5) * 1.4 * _Turbulence;

                float broadFlow = Fbm(flowUv);
                float detailFlow = Fbm(float2(
                    p.x * 13.0 + broadFlow * 2.0,
                    p.y * 13.0 + t * 7.0));

                float streakA = pow(saturate(sin((p.x + broadFlow * 0.12) * 31.0 + detailFlow * 3.0)), 8.0);
                float streakB = pow(saturate(sin((p.x - broadFlow * 0.10) * 47.0 - detailFlow * 2.0)), 12.0);
                float streaks = saturate(streakA * 0.65 + streakB * 0.45);

                float fallingCells = Fbm(float2(p.x * 8.0, p.y * 9.0 + t * 5.0));
                float whiteWater = smoothstep(0.52, 0.84, broadFlow + fallingCells * 0.42);

                half3 water = lerp(_DeepColor.rgb, _MidColor.rgb, broadFlow);
                float aeration = saturate(whiteWater * 0.72 + streaks * _FoamStrength * 0.48);
                water = lerp(water, _BrightColor.rgb, aeration);

                // Localized foam on turbulent sheet edges.
                float edgeRatio = sheetDistance / max(halfWidth, 0.001);
                float edgeFoam = smoothstep(0.74, 1.04, edgeRatio) * body;
                water = lerp(water, _FoamColor.rgb, saturate(edgeFoam * _FoamStrength * 0.75));

                // Breaking water near the lip/top.
                float lip = exp(-pow((p.y - 0.91) * 11.0, 2.0)) * body;
                lip *= 0.6 + 0.7 * Fbm(float2(p.x * 12.0 - t * 1.2, t * 0.8));
                water = lerp(water, _FoamColor.rgb, saturate(lip * _FoamStrength * 0.75));

                // Impact churn at the base of the vertical sheet.
                float floorBand = exp(-pow((p.y + 0.83) * 8.3, 2.0));
                float impactNoise = Fbm(float2(p.x * 5.7 - t * 0.8, p.y * 7.0 + t * 0.9));
                float impact = floorBand * smoothstep(0.25, 0.86, impactNoise + body * 0.5);
                impact *= smoothstep(0.92, 0.15, abs(p.x));
                water = lerp(water, _FoamColor.rgb, saturate(impact * _FoamStrength));

                // Soft lower-sheet spray/mist impression. Production implementation may
                // move this to particles/volumetrics while keeping the same semantic profile.
                float2 mistUv = float2(p.x * 2.2 + sin(t * 0.35) * 0.2, p.y * 3.1 - t * 0.35);
                float mistNoise = Fbm(mistUv + Fbm(mistUv * 1.9));
                float mistHeight = smoothstep(-0.25, -0.92, p.y);
                float mistWidth = smoothstep(1.25, 0.12, abs(p.x));
                float mist = smoothstep(0.46, 0.72, mistNoise) * mistHeight * mistWidth * _MistStrength;
                water = lerp(water, _FoamColor.rgb, saturate(mist * 0.55));

                float alpha = body * _Opacity;
                alpha = max(alpha, saturate(impact * 0.42 + mist * 0.18));

                return half4(water, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
