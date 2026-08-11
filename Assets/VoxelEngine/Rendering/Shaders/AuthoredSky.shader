Shader "Hidden/VoxelEngine/AuthoredSky"
{
    Properties
    {
        _SkyTexture("Sky Panorama", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" }
        Pass
        {
            Name "AuthoredVoxelSky"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyTexture);
            SAMPLER(sampler_SkyTexture);

            float4x4 _InvViewProj;
            float4 _CameraPosition;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                // Full-screen triangle, no mesh allocation.
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                Varyings output;
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float3 GradientSky(float3 direction)
            {
                return lerp(_SkyHorizon.rgb, _SkyZenith.rgb,
                            saturate(direction.y * 0.5 + 0.5));
            }

            float3 AuthoredSky(float3 direction)
            {
                float2 skyUv = float2(atan2(direction.x, direction.z) * 0.159154943 + 0.5,
                                      asin(clamp(direction.y, -1.0, 1.0)) * 0.318309886 + 0.5);
                float3 painted = SAMPLE_TEXTURE2D_LOD(_SkyTexture, sampler_SkyTexture, skyUv, 0).rgb;
                float luminance = dot(painted, float3(0.2126, 0.7152, 0.0722));
                painted = lerp(luminance.xxx, painted, 0.46);
                float3 sky = lerp(painted, GradientSky(direction), 0.48);

                // Preserve the broad warm sun treatment from the old raymarch presentation
                // without making sky rendering depend on the voxel renderer itself.
                float sunDot = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float broadHalo = pow(sunDot, 18.0);
                float innerHalo = pow(sunDot, 96.0);
                float disc = pow(sunDot, 900.0);
                sky += float3(1.0, 0.55, 0.23) * broadHalo * 0.12;
                sky += float3(1.0, 0.72, 0.42) * innerHalo * 0.24;
                sky += float3(1.0, 0.92, 0.72) * disc * 1.25;
                return sky;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 ndc = input.uv * 2.0 - 1.0;
#if UNITY_UV_STARTS_AT_TOP
                ndc.y = -ndc.y;
#endif
                float4 worldFar = mul(_InvViewProj,
                    float4(ndc, UNITY_RAW_FAR_CLIP_VALUE, 1.0));
                float3 world = worldFar.xyz / max(abs(worldFar.w), 1e-6);
                float3 direction = normalize(world - _CameraPosition.xyz);
                return float4(AuthoredSky(direction), 1.0);
            }
            ENDHLSL
        }
    }
}
