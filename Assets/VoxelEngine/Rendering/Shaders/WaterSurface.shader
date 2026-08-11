Shader "Hidden/VoxelEngine/WaterSurface"
{
    Properties
    {
        _SkyTexture("Sky Panorama", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "VoxelWaterSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SurfaceVertex
            {
                float3 position;
                float3 normal;
                uint material;
                uint active;
            };

            StructuredBuffer<SurfaceVertex> _SurfaceVertices;
            StructuredBuffer<uint> _SurfaceIndices;
            uint _SurfaceIndexBase;

            TEXTURE2D(_SkyTexture);
            SAMPLER(sampler_SkyTexture);

            float4 _CameraPosition;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _WaterTime;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                nointerpolation uint material : TEXCOORD2;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                SurfaceVertex vertex = _SurfaceVertices[_SurfaceIndices[_SurfaceIndexBase + vertexID]];
                Varyings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.normalWS = normalize(vertex.normal);
                output.material = vertex.material;
                return output;
            }

            float3 GradientSky(float3 direction)
            {
                return lerp(_SkyHorizon.rgb, _SkyZenith.rgb,
                            saturate(direction.y * 0.5 + 0.5));
            }

            float3 SkyReflection(float3 direction)
            {
                float2 skyUv = float2(atan2(direction.x, direction.z) * 0.159154943 + 0.5,
                                      asin(clamp(direction.y, -1.0, 1.0)) * 0.318309886 + 0.5);
                float3 painted = SAMPLE_TEXTURE2D_LOD(_SkyTexture, sampler_SkyTexture, skyUv, 0).rgb;
                float luminance = dot(painted, float3(0.2126, 0.7152, 0.0722));
                painted = lerp(luminance.xxx, painted, 0.46);
                return lerp(painted, GradientSky(direction), 0.48);
            }

            float3 AnimatedNormal(float3 p, float3 baseNormal, uint material)
            {
                float3 n = normalize(baseNormal);
                float3 tangent = abs(n.y) < 0.9 ? normalize(cross(float3(0,1,0), n))
                                                : float3(1,0,0);
                float3 bitangent = normalize(cross(n, tangent));

                float phaseA;
                float phaseB;
                float strength;
                if (material == 16u)
                {
                    // Cascades flow down the surface and ripple across it.
                    phaseA = p.y * 3.2 - _WaterTime * 4.6 + dot(p.xz, float2(1.1, 0.7));
                    phaseB = p.y * 5.7 - _WaterTime * 6.1 + dot(p.xz, float2(-0.8, 1.3));
                    strength = 0.16;
                }
                else
                {
                    phaseA = dot(p.xz, float2(0.86, 0.47)) * 2.0 + _WaterTime * 1.25;
                    phaseB = dot(p.xz, float2(-0.38, 1.04)) * 2.6 - _WaterTime * 0.93;
                    strength = 0.075;
                }

                float2 wave = float2(sin(phaseA), cos(phaseB)) * strength;
                return normalize(n + tangent * wave.x + bitangent * wave.y);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normal = AnimatedNormal(input.positionWS, input.normalWS, input.material);
                float3 toCamera = normalize(_CameraPosition.xyz - input.positionWS);
                float ndotv = saturate(dot(normal, toCamera));
                float fresnel = 0.035 + 0.965 * pow(1.0 - ndotv, 5.0);

                float3 reflectedDirection = reflect(-toCamera, normal);
                float3 reflectedSky = SkyReflection(reflectedDirection);

                bool cascade = input.material == 16u;
                float3 body = cascade ? float3(0.055, 0.27, 0.32)
                                      : float3(0.025, 0.115, 0.15);

                float3 halfVector = normalize(normalize(_SunDirection.xyz) + toCamera);
                float sunSpecular = pow(saturate(dot(normal, halfVector)), cascade ? 56.0 : 110.0);
                float3 specularColour = float3(1.0, 0.86, 0.66) * sunSpecular * 0.65;

                float foam = cascade
                           ? saturate(0.35 + 0.35 * sin(input.positionWS.y * 5.0 - _WaterTime * 5.5)
                                      + 0.18 * sin(dot(input.positionWS.xz, float2(2.7, 1.9))))
                           : 0.0;

                float3 colour = lerp(body, reflectedSky, saturate(fresnel * 0.88 + 0.08));
                colour += specularColour;
                if (cascade) colour = lerp(colour, float3(0.72, 0.88, 0.90), foam * 0.34);

                float alpha = cascade ? 0.72 : lerp(0.58, 0.86, fresnel);
                return float4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
