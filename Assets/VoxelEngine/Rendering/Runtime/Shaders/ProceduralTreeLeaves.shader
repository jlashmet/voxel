Shader "VoxelEngine/ProceduralTreeLeaves"
{
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.18
        _Damage ("Damage", Range(0, 1)) = 0
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

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _WindStrength;
            float _Damage;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 style : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float style : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float heightWeight = saturate(input.positionOS.y * 0.08);
                float phase = positionWS.x * 0.17 + positionWS.z * 0.13 + _Time.y * 1.65;
                positionWS.x += sin(phase) * _WindStrength * 0.12 * heightWeight;
                positionWS.z += cos(phase * 0.83) * _WindStrength * 0.08 * heightWeight;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.style = input.style.x;
                output.color = input.color;
                return output;
            }

            float LeafMask(float2 uv, float style)
            {
                float2 p = uv * 2.0 - 1.0;

                // Broad leaf: round base, slightly pointed top.
                if (style < 0.5)
                {
                    float width = lerp(0.76, 0.48, saturate(p.y * 0.5 + 0.5));
                    return 1.0 - (p.x * p.x / (width * width) + p.y * p.y);
                }

                // Needle cluster: deliberately narrow and long.
                if (style < 1.5)
                    return 1.0 - (p.x * p.x * 7.5 + p.y * p.y * 0.72);

                // Willow-like narrow leaf.
                if (style < 2.5)
                    return 1.0 - (p.x * p.x * 3.2 + p.y * p.y * 0.90);

                // Sakura blossom: five-lobed petal silhouette rather than a pink square card.
                float radius = length(p);
                float angle = atan2(p.y, p.x);
                float petalRadius = 0.69 + 0.19 * cos(angle * 5.0);
                return petalRadius - radius;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = LeafMask(input.uv, input.style);

                // Damage is presentation-only but comes from the authoritative hidden voxel proxy.
                // Raising the cutout threshold progressively strips foliage without rebuilding the
                // branch mesh every time an explosion removes another handful of legacy leaves.
                float damage = saturate(_Damage);
                clip(mask - lerp(0.015, 0.52, damage));

                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb,
                                      saturate(abs(n.y) * 0.65 + 0.20));
                float3 colour = input.color.rgb;

                if (input.style >= 2.5)
                {
                    float2 p = input.uv * 2.0 - 1.0;
                    float centre = saturate(1.0 - length(p) * 3.8);
                    colour = lerp(colour, float3(1.0, 0.90, 0.72), centre * 0.62);
                }

                colour *= lerp(1.0, 0.72, damage);
                float3 lit = colour * (ambient * 0.48 + (0.38 + ndl * 0.62));
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
