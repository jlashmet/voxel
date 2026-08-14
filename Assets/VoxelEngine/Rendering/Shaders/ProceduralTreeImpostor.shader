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

            float Ellipse(float2 p, float2 centre, float2 radius)
            {
                float2 q = (p - centre) / radius;
                return 1.0 - dot(q, q);
            }

            float TreeMask(float2 uv)
            {
                // UV is the complete tree bounds. Keep the trunk visible low in the card and use
                // several overlapping canopy lobes instead of filling the whole rectangle with a
                // single opaque oval. Small deterministic gaps preserve the airy character of the
                // geometry LOD at the 300 m handoff.
                float2 p = uv;

                float trunkWidth = lerp(0.040, 0.085, saturate((0.48 - p.y) / 0.48));
                float trunk = min(p.y - 0.01, 0.53 - p.y);
                trunk = min(trunk, trunkWidth - abs(p.x - 0.5));

                float crown = Ellipse(p, float2(0.50, 0.72), float2(0.31, 0.29));
                crown = max(crown, Ellipse(p, float2(0.31, 0.66), float2(0.20, 0.20)));
                crown = max(crown, Ellipse(p, float2(0.69, 0.65), float2(0.19, 0.21)));
                crown = max(crown, Ellipse(p, float2(0.43, 0.88), float2(0.19, 0.14)));
                crown = max(crown, Ellipse(p, float2(0.61, 0.86), float2(0.18, 0.15)));

                float edgeBreakup = 0.055 * sin(p.x * 39.0 + p.y * 17.0)
                                  + 0.040 * sin(p.x * 13.0 - p.y * 43.0);
                crown += edgeBreakup;

                // Cut deterministic holes through the crown so the far card does not become a
                // solid green disk. The holes are intentionally low frequency so they survive
                // downsampling and MSAA at long range.
                float gapField = sin(p.x * 31.0 + p.y * 7.0)
                               * sin(p.y * 29.0 - p.x * 9.0);
                if (p.y > 0.48 && gapField > 0.73)
                    crown = -1.0;

                return max(trunk, crown);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float damage = saturate(_Damage);
                float mask = TreeMask(input.uv);
                clip(mask - lerp(0.0, 0.12, damage));

                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb,
                                      saturate(abs(n.y) * 0.50 + 0.25));
                float3 colour = input.color.rgb * lerp(1.0, 0.72, damage);
                float trunkBlend = 1.0 - smoothstep(0.38, 0.55, input.uv.y);
                float3 trunkColour = float3(0.23, 0.14, 0.075);
                colour = lerp(colour, trunkColour, trunkBlend * 0.82);
                float3 lit = colour * (ambient * 0.54 + (0.34 + ndl * 0.56));
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
