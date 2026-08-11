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
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _Tint;
            float _TextureScale;
            float _TextureInfluence;
            float _Smoothness;
            float _TopLight;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.positionWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normal);
                TRANSFER_SHADOW(output);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 weights = pow(abs(normalWS), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);

                float s = _TextureScale;
                float3 xSample = tex2D(_MainTex, input.positionWS.zy * s).rgb;
                float3 ySample = tex2D(_MainTex, input.positionWS.xz * s).rgb;
                float3 zSample = tex2D(_MainTex, input.positionWS.xy * s).rgb;
                float3 textureSample = xSample * weights.x + ySample * weights.y + zSample * weights.z;

                // Preserve the broad concept-art colour. The source textures contribute only a
                // quiet grayscale wobble so the scene reads as large painted forms, not tiling.
                float textureValue = dot(textureSample, float3(0.299, 0.587, 0.114));
                float detail = lerp(1.0, lerp(0.84, 1.14, textureValue), _TextureInfluence);
                float macro = 1.0 + 0.025
                    * sin(input.positionWS.x * 0.72 + input.positionWS.z * 0.31)
                    * sin(input.positionWS.z * 0.47 + input.positionWS.y * 0.28);
                float3 albedo = _Tint.rgb * detail * macro;

                float top = saturate(normalWS.y);
                albedo *= lerp(1.0 - _TopLight * 0.22, 1.0 + _TopLight * 0.52, top);

                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(normalWS, lightDirection));
                float attenuation = SHADOW_ATTENUATION(input);
                float3 direct = _LightColor0.rgb * (0.38 + ndl * 0.62)
                              * lerp(0.58, 1.0, attenuation) * 0.78;
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * 1.18;

                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float3 halfDirection = normalize(lightDirection + viewDirection);
                float specular = pow(saturate(dot(normalWS, halfDirection)),
                                     lerp(10.0, 80.0, _Smoothness))
                               * _Smoothness * attenuation * 0.16;

                return fixed4(saturate(albedo * (ambient + direct)
                                     + specular * _LightColor0.rgb), 1.0);
            }
            ENDCG
        }

        // The ordinary voxel surface meshes already have simple geometry, so the built-in caster
        // from Standard is enough for this visual experiment.
        UsePass "Standard/SHADOWCASTER"
    }

    FallBack "Diffuse"
}
