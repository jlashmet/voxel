Shader "VoxelEngine/ProceduralVine"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.12, 0.30, 0.08, 1)
        _TipColor ("Tip Color", Color) = (0.31, 0.52, 0.15, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.16
        _Leafiness ("Leafiness", Range(0, 1)) = 0.55
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
            float _WindStrength;
            float _Leafiness;
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
                float phase = dot(positionWS.xz, float2(0.41, 0.29)) + AnimationTime() * 1.35 + input.uv.y * 7.0;
                float freeEnd = smoothstep(0.18, 1.0, input.uv.y);
                positionWS.x += sin(phase) * _WindStrength * 0.07 * freeEnd;
                positionWS.z += cos(phase * 0.73) * _WindStrength * 0.05 * freeEnd;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // A continuous narrow stem with alternating broad leaf lobes reads as ivy at hero
                // distance. The previous high-frequency sine mask produced a mechanical ladder of
                // identical holes, especially when several climber cards overlapped.
                float x = input.uv.x * 2.0 - 1.0;
                float along = saturate(input.uv.y);
                const float leafCount = 5.0;
                float scaled = min(along * leafCount, leafCount - 0.0001);
                float leafIndex = floor(scaled);
                float leafLocalY = frac(scaled);
                float leafY = abs(leafLocalY * 2.0 - 1.0);
                float leafProfile = sqrt(saturate(1.0 - leafY * leafY));
                float side = fmod(leafIndex, 2.0) < 1.0 ? -1.0 : 1.0;
                float leafCentre = side * 0.33;
                float leafHalfWidth = (0.14 + 0.34 * _Leafiness) * leafProfile;
                float leafMask = leafHalfWidth - abs(x - leafCentre);
                float stemMask = 0.075 - abs(x);
                float mask = max(stemMask, leafMask);
                clip(mask - lerp(-0.035, 0.035, _Cutoff));

                float leafHighlight = saturate(leafProfile * 0.65 + 0.20);
                float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb,
                                     saturate(along * 0.58 + leafHighlight * 0.30));
                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(abs(n.y) * 0.62 + 0.20));
                float3 lit = albedo * (ambient * 0.48 + (0.36 + ndl * 0.64));
                float pulse = 0.86 + 0.14 * sin(AnimationTime() * 1.4 + input.uv.y * 9.0);
                lit += _EmissionColor.rgb * _EmissionStrength * pulse;
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
