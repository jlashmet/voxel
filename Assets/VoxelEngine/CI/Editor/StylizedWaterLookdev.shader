Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.02,0.34,0.58,1)
        _MidColor ("Mid Color", Color) = (0.03,0.68,0.88,1)
        _ShallowColor ("Shallow Color", Color) = (0.36,0.90,0.98,1)
        _FoamColor ("Foam Color", Color) = (0.94,0.99,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _WaveScale ("Wave Scale", Float) = 10
        _FoamAmount ("Foam Amount", Range(0,1)) = 0
        _FlowMode ("Flow Mode", Range(0,1)) = 0
        _Phase ("Phase", Float) = 0
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
            Name "StylizedWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float2 worldXY : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _MidColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float _FlowSpeed;
                float _WaveScale;
                float _FoamAmount;
                float _FlowMode;
                float _Phase;
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
                float v = 0;
                float a = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    v += valueNoise(p) * a;
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.uv = input.uv;
                o.color = input.color;
                o.worldXY = pos.positionWS.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed + _Phase;
                float2 uv = i.uv;

                float2 dirA = lerp(float2(0.7, 0.25), float2(0.08, -1.35), _FlowMode);
                float2 dirB = lerp(float2(-0.35, 0.62), float2(-0.12, -0.78), _FlowMode);
                float n1 = fbm((uv + dirA * t) * _WaveScale);
                float n2 = fbm((uv * 1.73 + dirB * t + 7.13) * (_WaveScale * 0.72));
                float waves = saturate(n1 * 0.62 + n2 * 0.38);

                float band = smoothstep(0.18, 0.82, waves + (uv.y - 0.5) * 0.18);
                band = floor(band * 4.0) / 3.0;
                float3 col = lerp(_DeepColor.rgb, _MidColor.rgb, saturate(band * 1.25));
                col = lerp(col, _ShallowColor.rgb, smoothstep(0.58, 0.92, waves));

                float streakCoord = lerp(uv.x + uv.y * 0.24, uv.x * 0.45 + uv.y * 2.2, _FlowMode);
                float streak = sin(streakCoord * 31.0 + n1 * 5.0 - t * lerp(2.2, 8.0, _FlowMode));
                streak = pow(saturate(streak * 0.5 + 0.5), 8.0);
                float sparkle = pow(saturate(sin((uv.x * 1.7 - uv.y) * 22.0 + n2 * 7.0 + t * 1.3) * 0.5 + 0.5), 14.0);
                col += (_ShallowColor.rgb * 0.52) * streak;
                col += (_FoamColor.rgb * 0.32) * sparkle;

                float foamNoise = fbm(float2(uv.x * 13.0 + t * 0.35, uv.y * 9.0 - t * 0.62) + _Phase * 3.1);
                float foamMask = smoothstep(0.42, 0.67, foamNoise + i.color.r * 0.42);
                float foam = saturate(_FoamAmount * foamMask);
                col = lerp(col, _FoamColor.rgb, foam);

                float alpha = _Alpha * i.color.a;
                alpha *= lerp(1.0, smoothstep(0.25, 0.52, foamNoise + 0.22), _FoamAmount * 0.82);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
