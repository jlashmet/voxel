Shader "VoxelEngine/WorldArtLookdev"
{
    Properties
    {
        _MainTex ("Surface Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _TextureScale ("World Texture Scale", Float) = 0.18
        _TextureInfluence ("Texture Influence", Range(0,1)) = 0.10
        _Smoothness ("Smoothness", Range(0,1)) = 0.05
        _TopLight ("Upward Surface Lift", Range(0,0.4)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

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
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 weights = pow(abs(normalWS), 4.0h);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);

                float s = _TextureScale;
                half3 xSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.zy * s).rgb;
                half3 ySample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.xz * s).rgb;
                half3 zSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.positionWS.xy * s).rgb;
                half3 textureSample = xSample * weights.x + ySample * weights.y + zSample * weights.z;

                // The downloaded stylized textures are intentionally a whisper rather than the
                // identity of the surface. Large colour masses and silhouettes do the visual work.
                half textureValue = dot(textureSample, half3(0.299h, 0.587h, 0.114h));
                half detail = lerp(1.0h, lerp(0.84h, 1.14h, textureValue), _TextureInfluence);
                half macro = 1.0h + 0.025h
                    * sin(input.positionWS.x * 0.72h + input.positionWS.z * 0.31h)
                    * sin(input.positionWS.z * 0.47h + input.positionWS.y * 0.28h);
                half3 albedo = _Tint.rgb * detail * macro;

                half top = saturate(normalWS.y);
                albedo *= lerp(1.0h - _TopLight * 0.22h,
                               1.0h + _TopLight * 0.52h, top);

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 direct = mainLight.color * (0.34h + ndl * 0.66h)
                             * lerp(0.56h, 1.0h, shadow) * 0.86h;
                half3 ambient = SampleSH(normalWS) * 1.08h;

                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)),
                                    lerp(10.0h, 80.0h, _Smoothness))
                              * _Smoothness * shadow * 0.16h;

                half3 colour = saturate(albedo * (ambient + direct)
                                      + specular * mainLight.color);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
