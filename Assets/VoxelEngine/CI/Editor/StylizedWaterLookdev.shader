Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.015,0.27,0.52,1)
        _MidColor ("Mid Color", Color) = (0.00,0.62,0.86,1)
        _ShallowColor ("Shallow Color", Color) = (0.20,0.84,0.98,1)
        _FoamColor ("Foam Color", Color) = (0.96,0.995,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.24
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.007
        _Shimmer ("Shimmer", Range(0,1)) = 0.42
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.82
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
                half rawMask = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv).r;
                half mask = smoothstep(0.14h, 0.42h, rawMask);
                clip(mask - 0.08h);

                float time = _Time.y * _FlowSpeed;
                float nA = fbm(uv * float2(15.0, 21.0) + float2(time * 0.27, -time * 0.75));
                float nB = fbm(uv * float2(27.0, 12.0) + float2(-time * 0.51, time * 0.22) + 7.13);

                // Saturated posterized body color with enough separation to read at a glance.
                float depth = saturate(0.18 + (1.0 - uv.y) * 0.52 + nA * 0.34);
                float stepped = floor(depth * 5.0) / 4.0;
                half3 color = lerp(_ShallowColor.rgb, _MidColor.rgb, saturate(stepped));
                color = lerp(color, _DeepColor.rgb, smoothstep(0.68, 1.02, depth));

                // Detect tall/narrow local regions from the mask gradient: those are treated as falls.
                float2 texel1 = _ReferenceTex_TexelSize.xy * 2.2;
                half xL = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(-texel1.x, 0)).r;
                half xR = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2( texel1.x, 0)).r;
                half yD = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0, -texel1.y)).r;
                half yU = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0,  texel1.y)).r;
                float horizontalEdge = abs(xR - xL);
                float verticalEdge = abs(yU - yD);
                float fallLocal = saturate((horizontalEdge - verticalEdge) * 3.0 + smoothstep(0.58, 0.90, uv.y) * 0.20);

                float vertical = sin(uv.x * 118.0 + nB * 14.0 - time * 9.0);
                vertical = pow(saturate(vertical * 0.5 + 0.5), 9.0);
                float ribbons = sin((uv.x * 1.15 - uv.y * 2.7) * 48.0 + nA * 9.0 + time * 2.1);
                ribbons = pow(saturate(ribbons * 0.5 + 0.5), 18.0);
                color += _ShallowColor.rgb * lerp(ribbons * 0.24, vertical * 0.55, fallLocal);

                // White vertical ribs and lip foam keep waterfalls bright and graphic.
                float fallRibs = pow(saturate(sin(uv.x * 160.0 + nB * 10.0 - time * 7.0) * 0.5 + 0.5), 13.0);
                color = lerp(color, _FoamColor.rgb, fallRibs * fallLocal * 0.34);

                // Broken sparkle/ripple accents on pools.
                float sparkleA = pow(saturate(sin((uv.x * 2.0 + uv.y) * 88.0 + nA * 9.0 + time * 2.1) * 0.5 + 0.5), 25.0);
                float sparkleB = pow(saturate(sin((uv.x - uv.y * 1.8) * 61.0 + nB * 8.0 - time * 1.7) * 0.5 + 0.5), 28.0);
                color += _FoamColor.rgb * (sparkleA * 0.7 + sparkleB * 0.3) * _Shimmer * (1.0 - fallLocal * 0.55);

                // Wider, brighter silhouette foam. Use raw mask so chipped edges become visible detail.
                float2 texel = _ReferenceTex_TexelSize.xy * 3.3;
                half mL = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(-texel.x, 0)).r;
                half mR = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2( texel.x, 0)).r;
                half mD = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0, -texel.y)).r;
                half mU = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0,  texel.y)).r;
                half neighbor = min(min(mL, mR), min(mD, mU));
                half edge = saturate((rawMask - neighbor) * 5.2h);
                float foamNoise = fbm(uv * 45.0 + float2(time * 0.7, -time));
                float foamBreakup = smoothstep(0.26, 0.62, foamNoise + edge * 0.62);
                float foam = saturate(edge * foamBreakup * _EdgeFoam * 1.5);
                color = lerp(color, _FoamColor.rgb, foam);

                color = saturate(color * 1.10);
                return half4(color, saturate(mask * _Alpha));
            }
            ENDHLSL
        }
    }
}