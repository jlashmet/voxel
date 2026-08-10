Shader "Hidden/VoxelEngine/DepthRestore"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "VoxelDepthRestore"
            ZWrite On
            ZTest Always
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // World-space hit positions written by the raymarch; w = 0 on a miss. Point-sampled:
            // interpolating positions across a silhouette would fabricate surfaces in mid-air.
            // sampler_PointClamp is declared globally by GlobalSamplers.hlsl (pulled in via
            // Core.hlsl above); redeclaring it here is a duplicate-symbol compile error on Metal.
            TEXTURE2D(_HitPosition);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                // One fullscreen triangle; the fragment's SV_Depth is the only output that matters.
                float2 corner = float2((vertexID << 1) & 2u, vertexID & 2u);
                Varyings output;
                output.uv = corner;
                output.positionCS = float4(corner * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            void Frag(Varyings input, out float outDepth : SV_Depth)
            {
                float4 hit = SAMPLE_TEXTURE2D(_HitPosition, sampler_PointClamp, input.uv);

                if (hit.w < 0.5)
                {
                    // Sky: leave the far plane so nothing rasterised later is occluded by a miss.
                    #if defined(UNITY_REVERSED_Z)
                    outDepth = 0.0;
                    #else
                    outDepth = 1.0;
                    #endif
                    return;
                }

                // The same view-projection the rasteriser uses, so the recovered depth is exact
                // on every platform convention, reversed-Z included.
                float4 clip = mul(UNITY_MATRIX_VP, float4(hit.xyz, 1.0));
                outDepth = clip.z / clip.w;
            }
            ENDHLSL
        }
    }
}
