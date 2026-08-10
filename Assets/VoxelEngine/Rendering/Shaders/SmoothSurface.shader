Shader "Hidden/VoxelEngine/SmoothSurface"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _StoneTexture("Stone", 2D) = "white" {}
        _WoodTexture("Wood", 2D) = "white" {}
        _SandTexture("Sand", 2D) = "white" {}
        _RockTexture("Rock", 2D) = "white" {}
        _SlateTexture("Slate", 2D) = "white" {}
        _GrassTexture("Grass", 2D) = "white" {}
        _DirtTexture("Dirt", 2D) = "white" {}
        _DarkStoneTexture("Dark Stone", 2D) = "white" {}
        _StoneNormal("Stone Normal", 2D) = "bump" {}
        _WoodNormal("Wood Normal", 2D) = "bump" {}
        _SandNormal("Sand Normal", 2D) = "bump" {}
        _RockNormal("Rock Normal", 2D) = "bump" {}
        _SlateNormal("Slate Normal", 2D) = "bump" {}
        _GrassNormal("Grass Normal", 2D) = "bump" {}
        _DirtNormal("Dirt Normal", 2D) = "bump" {}
        _DarkStoneNormal("Dark Stone Normal", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "SmoothVoxelSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

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
            float4 _BaseColor;
            float4 _MaterialColours[18];
            TEXTURE2D(_StoneTexture);      SAMPLER(sampler_StoneTexture);
            TEXTURE2D(_WoodTexture);
            TEXTURE2D(_SandTexture);
            TEXTURE2D(_RockTexture);
            TEXTURE2D(_SlateTexture);
            TEXTURE2D(_GrassTexture);
            TEXTURE2D(_DirtTexture);
            TEXTURE2D(_DarkStoneTexture);
            TEXTURE2D(_StoneNormal);
            TEXTURE2D(_WoodNormal);
            TEXTURE2D(_SandNormal);
            TEXTURE2D(_RockNormal);
            TEXTURE2D(_SlateNormal);
            TEXTURE2D(_GrassNormal);
            TEXTURE2D(_DirtNormal);
            TEXTURE2D(_DarkStoneNormal);

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _VoxelSize;
            float _DebugCoverage;

            // These remain bound because cutaway/material helpers and future cheap shadowing use
            // the authoritative sparse world. The old fragment-time 48-step voxel shadow march
            // is intentionally not used: doing dozens of brick lookups for every covered pixel
            // defeated the point of rasterising the extracted surface.
            StructuredBuffer<int> _RegionWindow;
            StructuredBuffer<int> _BrickRefs;
            StructuredBuffer<uint> _BrickVoxels;
            float4 _WindowOrigin;
            uint _WindowX;
            uint _WindowY;
            uint _WindowZ;
            uint _CutawayEnabled;
            float4 _CutawayMinVoxel;
            float4 _CutawayMaxVoxel;
            uint _LocalLightCount;
            float4 _LocalLights[20];
            float4 _LocalLightColours[20];
            uint _FlashlightEnabled;
            float4 _FlashlightPosition;
            float4 _FlashlightDirection;
            float4 _FlashlightColour;
            float _FlashlightRange;
            float _FlashlightInnerCos;
            float _FlashlightOuterCos;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalNS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                nointerpolation uint material : TEXCOORD2;
                float occlusion : TEXCOORD3;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                SurfaceVertex vertex = _SurfaceVertices[_SurfaceIndices[_SurfaceIndexBase + vertexID]];
                Varyings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.normalNS = normalize(vertex.normal);
                output.material = vertex.material;
                output.occlusion = ((vertex.active >> 8) & 0xFFu) * (1.0 / 255.0);
                return output;
            }

            float3 SkyColour(float3 direction)
            {
                return lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(direction.y * 0.5 + 0.5));
            }

            float3 SampleTextureAt(uint material, float2 uv)
            {
                if (material == 1u)  return SAMPLE_TEXTURE2D(_StoneTexture, sampler_StoneTexture, uv).rgb;
                if (material == 2u)  return SAMPLE_TEXTURE2D(_WoodTexture, sampler_StoneTexture, uv).rgb;
                if (material == 3u)  return SAMPLE_TEXTURE2D(_SandTexture, sampler_StoneTexture, uv).rgb;
                if (material == 7u)  return SAMPLE_TEXTURE2D(_SlateTexture, sampler_StoneTexture, uv).rgb;
                if (material == 8u)  return SAMPLE_TEXTURE2D(_SlateTexture, sampler_StoneTexture, uv).rgb
                                           * float3(1.18, 0.68, 0.58);
                if (material == 10u) return SAMPLE_TEXTURE2D(_GrassTexture, sampler_StoneTexture, uv).rgb;
                if (material == 13u) return SAMPLE_TEXTURE2D(_DirtTexture, sampler_StoneTexture, uv).rgb;
                if (material == 14u) return SAMPLE_TEXTURE2D(_GrassTexture, sampler_StoneTexture, uv).rgb * 0.72;
                return 1.0;
            }

            float2 SurfaceUV(float3 normal, float3 hitVoxel)
            {
                float3 a = abs(normal);
                if (a.y >= a.x && a.y >= a.z) return hitVoxel.xz / 36.0;
                if (a.x >= a.z) return hitVoxel.zy / 36.0;
                return hitVoxel.xy / 36.0;
            }

            float3 SampleSurfaceTexture(uint material, float2 uv, float3 hitVoxel,
                                        float hitDistance, float3 fallback, float lod)
            {
                float3 texel = fallback;
                if (material == 6u)
                {
                    float exteriorWeathering = smoothstep(110.0, 170.0, hitVoxel.y)
                                             * (1.0 - smoothstep(120.0, 220.0, hitDistance));
                    float3 caveRock = SAMPLE_TEXTURE2D(_RockTexture, sampler_StoneTexture, uv).rgb;
                    float3 agedMasonry = SAMPLE_TEXTURE2D(_DarkStoneTexture, sampler_StoneTexture, uv).rgb;
                    texel = lerp(caveRock, agedMasonry, exteriorWeathering);
                }
                else if (material == 1u || material == 2u || material == 3u || material == 7u
                      || material == 8u || material == 10u || material == 13u || material == 14u)
                {
                    texel = SampleTextureAt(material, uv);
                }
                return texel;
            }

            float3 SampleTriplanarSurfaceTexture(uint material, float3 hitVoxel, float3 normal,
                                                 float hitDistance, float3 fallback, float lod)
            {
                float3 weights = pow(abs(normal), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);
                float3 alongX = SampleSurfaceTexture(material, hitVoxel.zy / 36.0, hitVoxel,
                                                     hitDistance, fallback, lod);
                float3 alongY = SampleSurfaceTexture(material, hitVoxel.xz / 36.0, hitVoxel,
                                                     hitDistance, fallback, lod);
                float3 alongZ = SampleSurfaceTexture(material, hitVoxel.xy / 36.0, hitVoxel,
                                                     hitDistance, fallback, lod);
                return alongX * weights.x + alongY * weights.y + alongZ * weights.z;
            }

            float3 SamplePackedNormal(uint material, float2 uv, float3 hitVoxel, float hitDistance)
            {
                if (material == 1u)  return SAMPLE_TEXTURE2D(_StoneNormal, sampler_StoneTexture, uv).rgb;
                if (material == 2u)  return SAMPLE_TEXTURE2D(_WoodNormal, sampler_StoneTexture, uv).rgb;
                if (material == 3u)  return SAMPLE_TEXTURE2D(_SandNormal, sampler_StoneTexture, uv).rgb;
                if (material == 7u || material == 8u)
                                     return SAMPLE_TEXTURE2D(_SlateNormal, sampler_StoneTexture, uv).rgb;
                if (material == 10u || material == 14u)
                                     return SAMPLE_TEXTURE2D(_GrassNormal, sampler_StoneTexture, uv).rgb;
                if (material == 13u) return SAMPLE_TEXTURE2D(_DirtNormal, sampler_StoneTexture, uv).rgb;
                if (material == 6u)
                {
                    float exteriorWeathering = smoothstep(110.0, 170.0, hitVoxel.y)
                                             * (1.0 - smoothstep(120.0, 220.0, hitDistance));
                    float3 caveNormal = SAMPLE_TEXTURE2D(_RockNormal, sampler_StoneTexture, uv).rgb;
                    float3 masonryNormal = SAMPLE_TEXTURE2D(_DarkStoneNormal, sampler_StoneTexture, uv).rgb;
                    return lerp(caveNormal, masonryNormal, exteriorWeathering);
                }
                return float3(0.5, 0.5, 1.0);
            }

            float3 SampleSurfaceNormal(uint material, float2 uv, float3 surfaceNormal,
                                       float3 hitVoxel, float hitDistance)
            {
                float3 packed = SamplePackedNormal(material, uv, hitVoxel, hitDistance);
                float3 tangentNormal = normalize(packed * 2.0 - 1.0);
                float3 a = abs(surfaceNormal);
                float3 tangent;
                float3 bitangent;
                if (a.y >= a.x && a.y >= a.z)
                {
                    tangent = float3(1, 0, 0);
                    bitangent = float3(0, 0, 1);
                }
                else if (a.x >= a.z)
                {
                    tangent = float3(0, 0, 1);
                    bitangent = float3(0, 1, 0);
                }
                else
                {
                    tangent = float3(1, 0, 0);
                    bitangent = float3(0, 1, 0);
                }

                return normalize(surfaceNormal * max(tangentNormal.z, 0.18)
                               + tangent * tangentNormal.x + bitangent * tangentNormal.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_DebugCoverage > 0.5)
                    return half4(normalize(input.normalNS) * 0.5 + 0.5, 1.0);

                float3 faceNormal = normalize(input.normalNS);
                float3 normal = faceNormal;
                uint material = min(input.material, 17u);
                float3 albedo = _MaterialColours[material].rgb;
                if (dot(albedo, albedo) < 1e-6) albedo = 1.0;

                float3 hitVoxel = input.positionWS / max(_VoxelSize, 1e-4);
                float hitDistance = length(input.positionWS - GetCameraPositionWS());

                float textureLod = clamp(log2(max(1.0, hitDistance * 0.36)), 0.0, 8.0);
                float2 surfaceUv = SurfaceUV(normal, hitVoxel);
                bool smoothMaterial = material == 1u || material == 3u || material == 5u
                                   || material == 6u || material == 10u || material == 14u;
                float3 mappedNormal = SampleSurfaceNormal(material, surfaceUv, normal,
                                                          hitVoxel, hitDistance);
                float normalStrength = (material == 1u || material == 6u) ? 0.18 : 0.16;
                normalStrength *= 1.0 - smoothstep(18.0, 64.0, hitDistance);
                normal = normalize(lerp(normal, mappedNormal, normalStrength));

                float3 textured = smoothMaterial
                    ? SampleTriplanarSurfaceTexture(material, hitVoxel, normal,
                                                    hitDistance, albedo, textureLod)
                    : SampleSurfaceTexture(material, surfaceUv, hitVoxel,
                                           hitDistance, albedo, textureLod);
                float textureWeight = lerp(0.28, 0.12, saturate(hitDistance / 350.0));

                if (material == 1u)
                {
                    float textureLuminance = dot(textured, float3(0.2126, 0.7152, 0.0722));
                    float detail = clamp(textureLuminance / 0.68, 0.74, 1.20);
                    float3 chroma = textured / max(textureLuminance, 0.08);
                    albedo *= lerp(1.0, detail, 0.58);
                    albedo *= lerp(1.0, chroma, 0.06);
                }
                else if (material == 6u)
                {
                    float textureLuminance = dot(textured, float3(0.2126, 0.7152, 0.0722));
                    float detail = clamp(textureLuminance / 0.58, 0.68, 1.24);
                    float3 chroma = textured / max(textureLuminance, 0.08);
                    albedo *= lerp(1.0, detail, 0.72);
                    albedo *= lerp(1.0, chroma, 0.025);
                }
                else
                {
                    albedo = lerp(albedo, textured, textureWeight);
                }

                if (material == 1u || material == 6u)
                {
                    float fineNoise = sin(dot(hitVoxel, float3(0.33, 0.27, 0.39)) + material * 0.71)
                                    * sin(dot(hitVoxel, float3(-0.21, 0.43, 0.17)) - material * 0.37);
                    float macroNoise = sin(dot(hitVoxel, float3(0.041, 0.029, 0.037)) + material)
                                     * sin(dot(hitVoxel, float3(-0.023, 0.035, 0.031)));
                    albedo *= (0.99 + fineNoise * 0.018) * (0.97 + macroNoise * 0.075);
                }

                float ndotl = saturate(dot(normal, _SunDirection.xyz));
                float fill = saturate(dot(normal, -_SunDirection.xyz)) * 0.06;

                float3 groundBounce = float3(0.26, 0.24, 0.21);
                float hemi = normal.y * 0.5 + 0.5;
                float3 ambient = lerp(groundBounce, SkyColour(normal), hemi) * 0.42;

                // Do not raymarch voxel shadows in the fragment shader. That was up to 48 sparse
                // brick/voxel lookups for every covered pixel and made the raster path nearly as
                // expensive as the renderer it replaced. Proper cheap/cached shadows can be
                // layered back later without coupling them to surface visibility.
                float shadow = 1.0;
                float ao = lerp(0.24, 1.0, input.occlusion);
                float3 sunColour = float3(1.00, 0.95, 0.86);

                float3 lit = albedo * (ambient * lerp(0.58, 1.0, ao)
                                     + sunColour * ndotl * shadow * 0.88
                                     + fill * ao);

                float3 viewToCamera = normalize(GetCameraPositionWS() - input.positionWS);
                float roughness = material == 17u ? 0.10
                                : material == 4u || material == 15u ? 0.24
                                : material == 7u || material == 8u ? 0.42
                                : material == 11u || material == 16u ? 0.18
                                : material == 14u ? 0.48 : 0.76;
                float3 halfVector = normalize(_SunDirection.xyz + viewToCamera);
                float specular = pow(saturate(dot(normal, halfVector)), lerp(96.0, 9.0, roughness))
                               * lerp(0.42, 0.035, roughness) * shadow * ndotl;
                lit += sunColour * specular;

                float3 hitMetres = input.positionWS;
                [loop]
                for (uint lightIndex = 0u; lightIndex < min(_LocalLightCount, 20u); lightIndex++)
                {
                    float3 toLight = _LocalLights[lightIndex].xyz - hitMetres;
                    float distanceToLight = length(toLight);
                    float radius = max(_LocalLights[lightIndex].w, 0.01);
                    float attenuation = saturate(1.0 - distanceToLight / radius);
                    attenuation *= attenuation;
                    float facing = saturate(dot(normal, toLight / max(distanceToLight, 0.001)));
                    float shaped = attenuation * (0.18 + facing * 0.82);
                    lit += albedo * _LocalLightColours[lightIndex].rgb
                         * (_LocalLightColours[lightIndex].w * shaped);
                }

                if (_FlashlightEnabled != 0u)
                {
                    float3 lightToHit = hitMetres - _FlashlightPosition.xyz;
                    float flashlightDistance = length(lightToHit);
                    float3 beamDirection = lightToHit / max(flashlightDistance, 0.001);
                    float cone = smoothstep(_FlashlightOuterCos, _FlashlightInnerCos,
                                            dot(beamDirection, normalize(_FlashlightDirection.xyz)));
                    float rangeFade = saturate(1.0 - flashlightDistance / max(_FlashlightRange, 0.01));
                    rangeFade *= rangeFade;
                    float flashlightFacing = saturate(dot(normal, -beamDirection));
                    float flashlightShape = cone * rangeFade * (0.12 + flashlightFacing * 0.88);
                    lit += albedo * _FlashlightColour.rgb * (_FlashlightColour.w * flashlightShape);
                }

                float3 toCamera = GetCameraPositionWS() - input.positionWS;
                float cameraDistance = length(toCamera);
                float cameraBounce = saturate(dot(normal, normalize(toCamera)))
                                   * saturate(1.0 - cameraDistance / 18.0) * 0.24;
                lit += albedo * cameraBounce * float3(1.16, 0.78, 0.46);

                if (material == 4u) lit += albedo * 0.10;
                if (material == 12u) lit += albedo * 0.34;

                lit *= 1.02;
                lit = saturate((lit * (2.51 * lit + 0.03))
                             / (lit * (2.43 * lit + 0.59) + 0.14));
                float gradedLuminance = dot(lit, float3(0.2126, 0.7152, 0.0722));
                lit = lerp(gradedLuminance.xxx, lit, 0.88);

                float3 viewDirection = normalize(input.positionWS - GetCameraPositionWS());
                float distanceFog = smoothstep(60.0, 300.0, hitDistance) * 0.40;
                float lowAltitude = 1.0 - smoothstep(32.0, 72.0, hitVoxel.y * _VoxelSize);
                distanceFog *= lerp(0.82, 1.12, lowAltitude);
                lit = lerp(lit, SkyColour(viewDirection), saturate(distanceFog));

                return half4(lit * _BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
