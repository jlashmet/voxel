Shader "VoxelEngine/ProceduralTreeImpostor"
{
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
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
            float _Damage;

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
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float CrownMask(float2 uv)
            {
                float2 p = uv * 2.0 - 1.0;

                // A broad, irregular crown envelope rather than the per-leaf mask used by the
                // near foliage shader. This is intentionally cheap: the final version can replace
                // this analytic silhouette with a species/angle impostor atlas without changing
                // the standing-tree submission architecture.
                float y = p.y;
                float width = 0.70 + 0.20 * (1.0 - y * y);
                float crown = 1.0 - (p.x * p.x / (width * width) + y * y * 0.92);
                float scallop = 0.08 * sin(p.x * 14.0 + p.y * 5.0)
                              + 0.05 * sin(p.y * 17.0 - p.x * 4.0);
                return crown + scallop;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float damage = saturate(_Damage);
                float mask = CrownMask(input.uv);
                clip(mask - lerp(0.02, 0.28, damage));

                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb,
                                      saturate(abs(n.y) * 0.50 + 0.25));
                float3 colour = input.color.rgb * lerp(1.0, 0.72, damage);
                float3 lit = colour * (ambient * 0.54 + (0.34 + ndl * 0.56));
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
