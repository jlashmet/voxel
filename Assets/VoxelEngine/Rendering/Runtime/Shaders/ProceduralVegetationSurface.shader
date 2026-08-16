Shader "VoxelEngine/ProceduralVegetationSurface"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.34, 0.12, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.38, 0.53, 0.19, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 0
        _PatchScale ("Patch Scale", Range(0.25, 8)) = 1.8
        _Cutoff ("Cutoff", Range(0, 1)) = 0.40
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
        Offset -1, -1
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
            float4 _SecondaryColor;
            float4 _EmissionColor;
            float _EmissionStrength;
            float _PatchScale;
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p = input.uv * 2.0 - 1.0;
                float radial = 1.0 - dot(p * float2(0.92, 1.05), p * float2(0.92, 1.05));
                float2 cells = floor(input.uv * _PatchScale * 9.0 + input.positionWS.xz * 0.37);
                float noise = Hash21(cells);
                float edge = radial + (noise - 0.5) * 0.42;
                clip(edge - lerp(-0.06, 0.20, _Cutoff));

                float detail = Hash21(floor(input.positionWS.xz * _PatchScale * 5.0));
                float3 albedo = lerp(_BaseColor.rgb, _SecondaryColor.rgb, detail * 0.72);
                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(abs(n.y) * 0.62 + 0.20));
                float3 lit = albedo * (ambient * 0.50 + (0.34 + ndl * 0.66));
                float shimmer = 0.88 + 0.12 * sin(AnimationTime() * 1.2 + dot(input.positionWS, float3(0.21, 0.36, 0.17)));
                lit += _EmissionColor.rgb * _EmissionStrength * shimmer;
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
