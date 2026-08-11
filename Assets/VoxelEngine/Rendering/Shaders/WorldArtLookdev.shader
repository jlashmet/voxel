Shader "VoxelEngine/WorldArtLookdev"
{
    Properties
    {
        _MainTex ("Surface Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _TextureScale ("World Texture Scale", Float) = 0.45
        _TextureInfluence ("Texture Influence", Range(0,1)) = 0.14
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _TopLight ("Upward Surface Lift", Range(0,0.4)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "WorldArtForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _TextureScale;
                float _TextureInfluence;
                float _Smoothness;
                float _TopLight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 weights = pow(abs(normalWS), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);

                float s = _TextureScale;
                float3 xSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.zy * s).rgb;
                float3 ySample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.xz * s).rgb;
                float3 zSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.xy * s).rgb;
                float3 textureSample = xSample * weights.x + ySample * weights.y + zSample * weights.z;

                // The source textures are allowed to supply only quiet value variation. Their hue
                // and small-scale contrast are deliberately suppressed: from gameplay distance the
                // eye should read "green mound" or "warm old stone" before it reads a texture.
                float textureValue = dot(textureSample, float3(0.299, 0.587, 0.114));
                float detail = lerp(1.0, lerp(0.78, 1.18, textureValue), _TextureInfluence);
                float macro = 1.0 + 0.035
                    * sin(input.positionWS.x * 0.71 + input.positionWS.z * 0.37)
                    * sin(input.positionWS.z * 0.53 + input.positionWS.y * 0.29);

                // The capture's old tints predated this material model and can exceed 1. Keep the
                // palette deliberately restrained instead of letting those values blow out.
                float3 albedo = saturate(_Tint.rgb * 0.72) * detail * macro;

                float top = saturate(normalWS.y);
                albedo *= lerp(1.0 - _TopLight * 0.25, 1.0 + _TopLight * 0.55, top);

                Light mainLight = GetMainLight(input.shadowCoord);
                float ndl = saturate(dot(normalWS, mainLight.direction));
                float halfLambert = 0.38 + 0.62 * ndl;
                float shadow = lerp(0.58, 1.0, mainLight.shadowAttenuation);
                float3 direct = mainLight.color * halfLambert * shadow
                              * mainLight.distanceAttenuation * 0.68;
                float3 ambient = SampleSH(normalWS) * 0.48;

                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDirection = normalize(mainLight.direction + viewDirection);
                float specPower = lerp(10.0, 96.0, _Smoothness);
                float specular = pow(saturate(dot(normalWS, halfDirection)), specPower)
                               * _Smoothness * mainLight.shadowAttenuation * 0.24;

                return half4(saturate(albedo * (ambient + direct) + specular * mainLight.color), 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack Off
}
