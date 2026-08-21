Shader "Hidden/VoxelEngine/SmoothSurface"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _AlbedoTextures("Material Albedo Array", 2DArray) = "white" {}
        _NormalTextures("Material Normal Array", 2DArray) = "bump" {}
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
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct SurfaceVertex
            {
                float3 position;
                float3 normal;
                uint material;
                uint active;
            };

            StructuredBuffer<SurfaceVertex> _SurfaceVertices;
            float4 _BaseColor;
            float4 _MaterialAlbedo[32];
            float4 _MaterialSampling[32];
            float4 _MaterialSurface[32];
            float4 _MaterialVariation[32];
            float4 _CoatingTint[16];
            float4 _CoatingSampling[16];
            float4 _CoatingResponse[16];
            float4 _SurfacePattern[32];
            float4 _SurfaceJointColour[32];
            float4 _SurfaceDetailResponse[32];
            TEXTURE2D_ARRAY(_AlbedoTextures); SAMPLER(sampler_AlbedoTextures);
            TEXTURE2D_ARRAY(_NormalTextures); SAMPLER(sampler_NormalTextures);

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _VoxelSize;
            float _DebugCoverage;

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
                InitIndirectDrawArgs(0);
                Varyings output;
                SurfaceVertex vertex = _SurfaceVertices[GetIndirectVertexID(vertexID)];
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

            float3 SampleSurfaceNormal(float4 sampling, float4 surface,
                                       float3 surfaceNormal, float3 hitVoxel)
            {
                float2 uv = SurfaceUV(surfaceNormal, hitVoxel) * surface.x;
                float3 packed = SAMPLE_TEXTURE2D_ARRAY(_NormalTextures, sampler_NormalTextures,
                                                       uv, sampling.y).rgb;
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
                uint material = min(input.material & 0xFFu, 31u);
                uint coating = min((input.material >> 8u) & 0xFFu, 15u);
                uint surfaceStyle = min((input.material >> 16u) & 0xFFu, 31u);
                uint packedSurface = (input.material >> 24u) & 0xFFu;
                uint surfaceFlags = packedSurface & 0x07u;
                float surfaceDetail = float(packedSurface >> 3u) / 31.0;
                float4 materialSampling = _MaterialSampling[material];
                float4 materialSurface = _MaterialSurface[material];
                float4 materialVariation = _MaterialVariation[material];
                float3 albedo = _MaterialAlbedo[material].rgb;

                float3 hitVoxel = input.positionWS / max(_VoxelSize, 1e-4);
                float hitDistance = length(input.positionWS - GetCameraPositionWS());

                float3 mappedNormal = SampleSurfaceNormal(materialSampling, materialSurface,
                                                          normal, hitVoxel);
                float normalStrength = materialSurface.y;
                normalStrength *= 1.0 - smoothstep(18.0, 64.0, hitDistance);
                normal = normalize(lerp(normal, mappedNormal, normalStrength));

                float3 textured = SampleMaterialAlbedo(materialSampling, materialSurface,
                                                       hitVoxel, normal);
                float textureWeight = materialSampling.w
                                    * lerp(1.0, 0.44, saturate(hitDistance / 350.0));
                float3 directTexture = lerp(albedo, textured, textureWeight);
                float textureLuminance = dot(textured, float3(0.2126, 0.7152, 0.0722));
                float detail = clamp(textureLuminance / max(materialVariation.x, 0.08),
                                     0.68, 1.24);
                float3 chroma = textured / max(textureLuminance, 0.08);
                float3 luminanceTexture = albedo
                                        * lerp(1.0, detail, materialVariation.y)
                                        * lerp(1.0, chroma, materialVariation.z);
                albedo = lerp(directTexture, luminanceTexture, saturate(materialSurface.w));

                // Coatings are presentation overlays. They never replace the base material ID
                // used by destruction/collision and arrive independently in packed attributes.
                float4 coatingSampling = _CoatingSampling[coating];
                float4 coatingResponse = _CoatingResponse[coating];
                float3 coatingTexture = SampleAlbedoLayer(coatingSampling.x,
                    SurfaceUV(faceNormal, hitVoxel) * coatingSampling.y);
                float3 coatingColour = lerp(_CoatingTint[coating].rgb,
                    coatingTexture * _CoatingTint[coating].rgb, coatingSampling.z);
                float upward = smoothstep(-0.15, 0.65, normal.y);
                float orientation = lerp(coatingResponse.x, coatingResponse.y, upward);
                float coatingNoise = sin(dot(hitVoxel, float3(0.19, 0.13, 0.23)))
                                   * sin(dot(hitVoxel, float3(-0.071, 0.113, 0.053)) + 1.7);
                float coatingAmount = saturate(coatingSampling.w * orientation
                                             * (1.0 + coatingNoise * coatingResponse.z));
                albedo = lerp(albedo, coatingColour, coatingAmount);

                // Surface patterns are indexed by style, rather than recognizing a style ID.
                float4 pattern = _SurfacePattern[surfaceStyle];
                float course = abs(frac(hitVoxel.y / max(pattern.y, 1.0)) - 0.5);
                float stagger = floor(hitVoxel.y / max(pattern.y, 1.0)) * pattern.z * 0.5;
                float vertical = abs(frac((hitVoxel.x + hitVoxel.z + stagger)
                                   / max(pattern.z, 1.0)) - 0.5);
                float joint = 1.0 - smoothstep(0.035, 0.095, min(course, vertical));
                float preservePattern = ((surfaceFlags & 2u) != 0u) ? 1.0 : 0.0;
                float patternAmount = pattern.x * pattern.w * joint * preservePattern;
                albedo = lerp(albedo, _SurfaceJointColour[surfaceStyle].rgb, patternAmount);

                // Authored detail is generic scalar data. The style row supplies its response;
                // no feature or material identity is compiled into this shader.
                float4 detailResponse = _SurfaceDetailResponse[surfaceStyle];
                // Detail codes are style-independent authoring channels: zero is neutral,
                // 1..15 select signed per-piece variation, and 16..31 describe continuous
                // high detail such as seams or wear. The style row controls every response.
                float detailCode = surfaceDetail * 31.0;
                float pieceMask = step(0.5, detailCode) * (1.0 - step(15.5, detailCode));
                float pieceSignal = clamp((detailCode - 8.0) / 7.0, -1.0, 1.0);
                float pieceVariation = pieceSignal * detailResponse.z * pieceMask;
                albedo *= 1.0 + pieceVariation;
                float detailMask = smoothstep(1.0 - saturate(detailResponse.w), 1.0,
                                              surfaceDetail);
                albedo = lerp(albedo, _SurfaceJointColour[surfaceStyle].rgb,
                              detailMask * detailResponse.x);

                float fineNoise = sin(dot(hitVoxel, float3(0.33, 0.27, 0.39)) + material * 0.71)
                                * sin(dot(hitVoxel, float3(-0.21, 0.43, 0.17)) - material * 0.37);
                float macroNoise = sin(dot(hitVoxel, float3(0.041, 0.029, 0.037)) + material)
                                 * sin(dot(hitVoxel, float3(-0.023, 0.035, 0.031)));
                albedo *= 1.0 + fineNoise * materialVariation.w * 0.24
                              + macroNoise * materialVariation.w;

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
                float coatingRoughnessWeight = step(0.0, coatingResponse.w) * coatingAmount;
                float roughness = lerp(materialSurface.z, coatingResponse.w,
                                       coatingRoughnessWeight);
                roughness = lerp(roughness, detailResponse.y,
                                 step(0.0, detailResponse.y) * detailMask);
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
