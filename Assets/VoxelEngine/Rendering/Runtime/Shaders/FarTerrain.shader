Shader "VoxelEngine/FarTerrain"
{
    // Shading for the clipmap rings that stand in for the voxel world beyond the streaming
    // radius.
    //
    // Ring vertices still carry the authoritative material albedo in COLOR so the startup
    // fallback and distant colour contract remain cheap. The shader resolves that colour back to
    // the renderer's material table and samples the same texture array, world-space basis, scale,
    // and distance attenuation as SmoothSurface. This keeps the near/far handoff from becoming a
    // second grass-texturing system with visibly different scale.
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
        _AerialColour ("Aerial Perspective", Color) = (0.62, 0.72, 0.86, 1)
        _AerialDistance ("Aerial Full Distance", Float) = 9000
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float4 _AerialColour;
            float _AerialDistance;

            // These are renderer-owned globals populated by VoxelRenderPass for the near surface.
            // Far terrain consumes them rather than carrying a second texture/presentation setup.
            float4 _MaterialAlbedo[32];
            float4 _MaterialSampling[32];
            float4 _MaterialSurface[32];
            TEXTURE2D_ARRAY(_AlbedoTextures); SAMPLER(sampler_AlbedoTextures);
            float _VoxelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            uint ResolveMaterialFromAlbedo(float3 vertexAlbedo)
            {
                uint bestMaterial = 0u;
                float bestError = 1e20;
                [unroll]
                for (uint material = 0u; material < 32u; material++)
                {
                    float3 delta = _MaterialAlbedo[material].rgb - vertexAlbedo;
                    float error = dot(delta, delta);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestMaterial = material;
                    }
                }
                return bestMaterial;
            }

            float2 SurfaceUV(float3 normal, float3 hitVoxel)
            {
                float3 a = abs(normal);
                if (a.y >= a.x && a.y >= a.z) return hitVoxel.xz;
                if (a.x >= a.z) return hitVoxel.zy;
                return hitVoxel.xy;
            }

            float3 SampleAlbedoLayer(float layer, float2 uv)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_AlbedoTextures, sampler_AlbedoTextures,
                                              uv, layer).rgb;
            }

            float3 SampleMaterialAlbedo(float4 sampling, float4 surface,
                                        float3 hitVoxel, float3 normal)
            {
                float layer = sampling.x;
                float scale = surface.x;
                float3 face = SampleAlbedoLayer(layer, SurfaceUV(normal, hitVoxel) * scale);
                float3 weights = pow(abs(normal), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);
                float3 triplanar = SampleAlbedoLayer(layer, hitVoxel.zy * scale) * weights.x
                                 + SampleAlbedoLayer(layer, hitVoxel.xz * scale) * weights.y
                                 + SampleAlbedoLayer(layer, hitVoxel.xy * scale) * weights.z;
                return lerp(face, triplanar, saturate(sampling.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, sun));
                float skyT = saturate(n.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);

                uint material = ResolveMaterialFromAlbedo(input.color.rgb);
                float4 materialSampling = _MaterialSampling[material];
                float4 materialSurface = _MaterialSurface[material];
                float3 hitVoxel = input.positionWS / max(_VoxelSize, 1e-4);
                float3 textured = SampleMaterialAlbedo(materialSampling, materialSurface,
                                                       hitVoxel, n);
                float hitDistance = length(input.positionWS - GetCameraPositionWS());
                float textureWeight = materialSampling.w
                                    * lerp(1.0, 0.44, saturate(hitDistance / 350.0));
                float3 albedo = lerp(input.color.rgb, textured, textureWeight);
                float3 lit = albedo * (ambient * 0.42 + (0.34 + ndl * 0.66));

                // Aerial perspective. Without it a 5 km summit reads as a cardboard cutout at
                // the same contrast as ground a hundred metres away, and the range loses all
                // sense of depth. Distance is measured to the camera, so it also hides the
                // outermost ring's low sample rate.
                float distance = hitDistance;
                float haze = saturate(distance / max(1.0, _AerialDistance));
                haze = haze * haze * 0.82;
                lit = lerp(lit, _AerialColour.rgb, haze);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
