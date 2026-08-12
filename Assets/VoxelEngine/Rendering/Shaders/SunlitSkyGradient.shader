Shader "VoxelEngine/SunlitSkyGradient"
{
    Properties
    {
        _BottomColor ("Bottom", Color) = (0.50,0.80,0.96,1)
        _HorizonColor ("Horizon", Color) = (0.30,0.68,0.94,1)
        _TopColor ("Top", Color) = (0.12,0.47,0.86,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "SkyGradient"
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _HorizonColor;
                float4 _TopColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float t : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.t = saturate(input.positionOS.y + 0.5);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half t = saturate(input.t);
                half3 lower = lerp(_BottomColor.rgb, _HorizonColor.rgb, smoothstep(0.0h, 0.48h, t));
                half3 upper = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.42h, 1.0h, t));
                half blend = smoothstep(0.38h, 0.62h, t);
                return half4(lerp(lower, upper, blend), 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
