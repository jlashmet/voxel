Shader "VoxelEngine/ProceduralVegetationFoliage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.20, 0.45, 0.16, 1)
        _TipColor ("Tip Color", Color) = (0.42, 0.70, 0.24, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 0
        _Shape ("Shape", Range(0, 4)) = 0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.22
        _Cutoff ("Cutoff", Range(0, 1)) = 0.42
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite On
        ZTest LEqual
        AlphaToMask On

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseColor;
            float4 _TipColor;
            float4 _EmissionColor;
            float _EmissionStrength;
            float _Shape;
            float _WindStrength;
            float _Cutoff;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _ValidationAnimationTime;
            float _UseValidationAnimationTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float AnimationTime()
            {
                return _UseValidationAnimationTime > 0.5 ? _ValidationAnimationTime : _Time.y;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float bend = saturate(input.uv.y);
                float phase = dot(positionWS.xz, float2(0.31, 0.23)) + AnimationTime() * 1.85;
                float gust = sin(phase) * 0.68 + sin(phase * 0.43 + 1.7) * 0.32;
                positionWS.x += gust * _WindStrength * 0.12 * bend * bend;
                positionWS.z += cos(phase * 0.79) * _WindStrength * 0.08 * bend * bend;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Ellipse(float2 p, float2 radius)
            {
                float2 q = p / max(radius, float2(0.001, 0.001));
                return 1.0 - dot(q, q);
            }

            float PlantMask(float2 uv, float shape)
            {
                float2 p = uv * 2.0 - 1.0;

                if (shape < 0.5)
                {
                    float width = lerp(0.58, 0.055, saturate(uv.y));
                    float side = 1.0 - abs(p.x) / max(width, 0.02);
                    float tip = 1.05 - abs(p.y - 0.02);
                    return min(side, tip);
                }

                if (shape < 1.5)
                {
                    float taper = lerp(0.70, 0.24, saturate(uv.y));
                    return 1.0 - (p.x * p.x / max(taper * taper, 0.02) + p.y * p.y * 0.92);
                }

                if (shape < 2.5)
                {
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    float petals = 0.57 + 0.24 * cos(a * 6.0);
                    float stem = min(0.11 - abs(p.x), 0.35 - abs(p.y + 0.66));
                    return max(petals - r, stem);
                }

                if (shape < 3.5)
                {
                    float cap = Ellipse(p - float2(0.0, 0.36), float2(0.86, 0.42));
                    float stem = min(0.22 - abs(p.x), 0.68 - abs(p.y + 0.36));
                    return max(cap, stem);
                }

                float a = atan2(p.y, p.x);
                float r = length(p);
                float outline = 0.76 + 0.08 * sin(a * 5.0) + 0.06 * sin(a * 9.0 + 1.2);
                return outline - r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = PlantMask(input.uv, _Shape);
                clip(mask - lerp(-0.04, 0.16, _Cutoff));

                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float skyT = saturate(abs(n.y) * 0.70 + 0.18);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);

                float height = saturate(input.uv.y);
                float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, height * 0.72);
                albedo *= lerp(float3(1,1,1), input.color.rgb, 0.30);
                float3 lit = albedo * (ambient * 0.48 + (0.36 + ndl * 0.64));
                float pulse = 0.88 + 0.12 * sin(AnimationTime() * 1.6 + dot(input.positionWS, float3(0.31, 0.19, 0.27)));
                lit += _EmissionColor.rgb * _EmissionStrength * pulse;
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
