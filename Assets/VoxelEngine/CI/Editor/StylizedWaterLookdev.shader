Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Reference Water", 2D) = "white" {}
        _FoamColor ("Foam Color", Color) = (0.94,0.99,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.25
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.002
        _Shimmer ("Shimmer", Range(0,1)) = 0.28
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.38
        _Alpha ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "StylizedReferenceWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            TEXTURE2D(_ReferenceTex);
            SAMPLER(sampler_ReferenceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ReferenceTex_ST;
                float4 _ReferenceTex_TexelSize;
                float4 _FoamColor;
                float _FlowSpeed;
                float _FlowStrength;
                float _Shimmer;
                float _EdgeFoam;
                float _Alpha;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amp = 0.5;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    value += valueNoise(p) * amp;
                    p = p * 2.03 + float2(17.1, 9.2);
                    amp *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _ReferenceTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y * _FlowSpeed;

                // Two counter-moving procedural flow fields. The displacement is deliberately
                // subtle so the authored silhouette stays locked while the interior feels alive.
                float nA = fbm(uv * float2(12.0, 18.0) + float2(time * 0.31, -time * 0.62));
                float nB = fbm(uv * float2(21.0, 9.0) + float2(-time * 0.47, time * 0.23) + 7.13);
                float2 flow = float2(nA - 0.5, nB - 0.5) * _FlowStrength;

                half4 authored = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, saturate(uv + flow));
                half baseAlpha = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv).a;
                clip(baseAlpha - 0.001h);

                float2 texel = _ReferenceTex_TexelSize.xy * 1.7;
                half aL = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(-texel.x, 0)).a;
                half aR = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2( texel.x, 0)).a;
                half aD = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0, -texel.y)).a;
                half aU = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0,  texel.y)).a;
                half neighbor = min(min(aL, aR), min(aD, aU));
                half edge = saturate((baseAlpha - neighbor) * 4.0h);

                // Stylized sparkle bands: short, high-frequency highlights that travel across
                // the authored cyan texture without flattening its painted detail.
                float waveA = sin((uv.x * 1.9 - uv.y * 0.7) * 76.0 + nA * 8.0 + time * 2.4);
                float waveB = sin((uv.x * 0.8 + uv.y * 1.6) * 49.0 + nB * 6.0 - time * 1.7);
                float sparkle = pow(saturate(waveA * 0.5 + 0.5), 18.0) * 0.68
                              + pow(saturate(waveB * 0.5 + 0.5), 22.0) * 0.32;

                half3 color = authored.rgb;
                color += _FoamColor.rgb * sparkle * _Shimmer * saturate(baseAlpha * 1.2);
                color = lerp(color, _FoamColor.rgb, edge * _EdgeFoam);

                // Preserve the reference alpha exactly. Motion only affects interior color,
                // preventing the waterfall/pool silhouette from swimming or growing halos.
                return half4(color, baseAlpha * _Alpha);
            }
            ENDHLSL
        }
    }
}
