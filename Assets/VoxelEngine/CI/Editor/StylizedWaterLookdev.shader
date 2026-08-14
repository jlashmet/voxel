Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.025,0.34,0.56,1)
        _MidColor ("Mid Color", Color) = (0.035,0.68,0.87,1)
        _ShallowColor ("Shallow Color", Color) = (0.30,0.88,0.97,1)
        _FoamColor ("Foam Color", Color) = (0.94,0.99,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.28
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.004
        _Shimmer ("Shimmer", Range(0,1)) = 0.32
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.55
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
            Name "StylizedMaskedWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_ReferenceTex);
            SAMPLER(sampler_ReferenceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ReferenceTex_ST;
                float4 _ReferenceTex_TexelSize;
                float4 _DeepColor;
                float4 _MidColor;
                float4 _ShallowColor;
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
                half mask = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv).r;
                clip(mask - 0.05h);

                float time = _Time.y * _FlowSpeed;
                float nA = fbm(uv * float2(13.0, 19.0) + float2(time * 0.33, -time * 0.71));
                float nB = fbm(uv * float2(23.0, 10.0) + float2(-time * 0.49, time * 0.27) + 7.13);

                // Broad cyan depth bands, deliberately posterized for the painted/anime read.
                float depth = saturate(0.25 + (1.0 - uv.y) * 0.48 + nA * 0.34);
                float stepped = floor(depth * 5.0) / 4.0;
                half3 color = lerp(_ShallowColor.rgb, _MidColor.rgb, saturate(stepped));
                color = lerp(color, _DeepColor.rgb, smoothstep(0.72, 1.05, depth));

                // Waterfalls favor vertical streaks; lower pools favor horizontal ribbons.
                float fallBias = smoothstep(0.48, 0.80, uv.y);
                float vertical = sin((uv.x * 78.0 + nB * 11.0) - time * 8.0);
                float horizontal = sin((uv.x * 1.3 - uv.y * 2.4) * 50.0 + nA * 8.0 + time * 2.3);
                float streak = lerp(horizontal, vertical, fallBias);
                streak = pow(saturate(streak * 0.5 + 0.5), lerp(15.0, 8.0, fallBias));
                color += _ShallowColor.rgb * streak * lerp(0.22, 0.46, fallBias);

                // Fine dual-flow shimmer breaks up large flat areas without noisy realism.
                float sparkleA = pow(saturate(sin((uv.x * 2.1 + uv.y) * 91.0 + nA * 9.0 + time * 2.1) * 0.5 + 0.5), 24.0);
                float sparkleB = pow(saturate(sin((uv.x - uv.y * 1.7) * 63.0 + nB * 7.0 - time * 1.6) * 0.5 + 0.5), 28.0);
                color += _FoamColor.rgb * (sparkleA * 0.65 + sparkleB * 0.35) * _Shimmer;

                // Screen-mask gradient creates shoreline/intersection-style foam around every
                // fragmented edge of the authored water silhouette.
                float2 texel = _ReferenceTex_TexelSize.xy * 1.8;
                half mL = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(-texel.x, 0)).r;
                half mR = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2( texel.x, 0)).r;
                half mD = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0, -texel.y)).r;
                half mU = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0,  texel.y)).r;
                half neighbor = min(min(mL, mR), min(mD, mU));
                half edge = saturate((mask - neighbor) * 3.5h);
                float foamBreakup = smoothstep(0.30, 0.68, fbm(uv * 38.0 + float2(time, -time * 0.7)) + edge * 0.45);
                color = lerp(color, _FoamColor.rgb, edge * foamBreakup * _EdgeFoam);

                return half4(saturate(color), mask * _Alpha);
            }
            ENDHLSL
        }
    }
}
