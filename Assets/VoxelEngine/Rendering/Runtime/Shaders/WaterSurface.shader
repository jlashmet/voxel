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
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
            uint _SurfaceVertexBase;

            TEXTURE2D(_SkyTexture);
            SAMPLER(sampler_SkyTexture);

            float4 _WaterShallow[32];
            float4 _WaterDeep[32];
            float4 _WaterMotion[32];
            float4 _WaterDetail[32];
            float4 _WaterFoam[32];
            float4 _WaterCascade[32];

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
                float4 topology : TEXCOORD3;
                float2 sprayUv : TEXCOORD4;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                SurfaceVertex vertex = _SurfaceVertices[
                    _SurfaceVertexBase + _SurfaceIndices[_SurfaceIndexBase + vertexID]];
                uint topologyFlags = vertex.material >> 24;
                Varyings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.normalWS = normalize(vertex.normal);
                output.material = min(vertex.material & 0xFFu, 31u);
                output.topology = float4(
                    (topologyFlags & 1u) != 0u ? 1.0 : 0.0,
                    (topologyFlags & 2u) != 0u ? 1.0 : 0.0,
                    (topologyFlags & 4u) != 0u ? 1.0 : 0.0,
                    (topologyFlags & 8u) != 0u ? 1.0 : 0.0);
                output.sprayUv = float2(
                    (vertex.active & 1u) != 0u ? 1.0 : 0.0,
                    (vertex.active & 2u) != 0u ? 1.0 : 0.0);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1,0)), u.x),
                            lerp(Hash21(i + float2(0,1)), Hash21(i + float2(1,1)), u.x), u.y);
            }

            float Fbm2(float2 p)
            {
                return ValueNoise(p) * 0.67 + ValueNoise(p * 2.07 + 19.1) * 0.33;
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

            float2 FlowAxis(float4 motion)
            {
                float2 flow = motion.yz;
                float lengthSq = dot(flow, flow);
                return lengthSq > 0.001 ? flow * rsqrt(lengthSq) : float2(1, 0);
            }

            float3 AnimatedNormal(float3 p, float3 baseNormal, float4 motion,
                                  float4 detail, float4 cascade)
            {
                float3 n = normalize(baseNormal);
                float3 tangent = abs(n.y) < 0.9 ? normalize(cross(float3(0,1,0), n))
                                                : float3(1,0,0);
                float3 bitangent = normalize(cross(n, tangent));
                float profile = motion.x;
                float speed = max(0.01, motion.w);
                float scale = max(0.05, detail.x);
                float strength = detail.y;
                float phaseA;
                float phaseB;

                if (profile > 2.5)
                {
                    float lateral = p.x + p.z * 0.73;
                    float descendingWarp = Fbm2(float2(lateral * 1.15,
                        p.y * 0.34 - _WaterTime * speed * 0.72));
                    phaseA = lateral * (4.8 / scale) + descendingWarp * cascade.x * 4.2;
                    phaseB = lateral * (8.3 / scale) - descendingWarp * cascade.x * 3.1
                           + p.y * 0.22 - _WaterTime * speed * 0.95;
                }
                else if (profile > 1.5)
                {
                    float2 flow = FlowAxis(motion);
                    float along = dot(p.xz, flow);
                    float across = dot(p.xz, float2(-flow.y, flow.x));
                    phaseA = along * (3.2 / scale) - _WaterTime * speed * 2.4
                           + sin(across * 1.7) * 0.45;
                    phaseB = along * (5.1 / scale) - _WaterTime * speed * 3.25
                           - across * 1.35;
                }
                else
                {
                    phaseA = dot(p.xz, float2(0.86, 0.47)) * (1.6 / scale)
                           + _WaterTime * speed * 1.5;
                    phaseB = dot(p.xz, float2(-0.38, 1.04)) * (2.1 / scale)
                           - _WaterTime * speed * 1.12;
                }

                float2 wave = float2(sin(phaseA), cos(phaseB)) * strength;
                return normalize(n + tangent * wave.x + bitangent * wave.y);
            }

            float SceneDepthGap(Varyings input)
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(uv);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterEye = -TransformWorldToView(input.positionWS).z;
                return max(0.0, sceneEye - waterEye);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // Spray is deliberately excluded from the depth-writing body pass. It is rendered
                // by VoxelWaterSpray below, where translucent feathering cannot stamp fan-shaped
                // holes into subsequently blended water through the depth buffer.
                if (input.topology.w > 0.5)
                    clip(-1.0);

                uint material = min(input.material, 31u);
                float4 shallow = _WaterShallow[material];
                float4 deep = _WaterDeep[material];
                float4 motion = _WaterMotion[material];
                float4 detail = _WaterDetail[material];
                float4 foamParams = _WaterFoam[material];
                float4 cascade = _WaterCascade[material];
                float waterfallMask = step(2.5, motion.x);
                bool waterfall = waterfallMask > 0.5;
                bool flowing = motion.x > 1.5 && !waterfall;

                float3 normal = AnimatedNormal(input.positionWS, input.normalWS,
                                               motion, detail, cascade);
                float3 toCamera = normalize(_CameraPosition.xyz - input.positionWS);
                float ndotv = saturate(abs(dot(normal, toCamera)));
                float fresnel = 0.025 + 0.975 * pow(1.0 - ndotv, 5.0);

                float depthGap = SceneDepthGap(input);
                float depthT = saturate(depthGap / max(0.05, deep.w));
                float contact = 1.0 - smoothstep(0.02, max(0.08, deep.w * 0.48), depthGap);

                float2 flow = FlowAxis(motion);
                float2 flowUv = flowing
                    ? input.positionWS.xz * max(0.25, foamParams.z)
                        - flow * (_WaterTime * motion.w * max(0.1, foamParams.w))
                    : input.positionWS.xz * max(0.25, foamParams.z)
                        + float2(_WaterTime * 0.04, -_WaterTime * 0.035);
                float surfacePattern = Fbm2(flowUv);
                float surfaceFoam = smoothstep(0.68, 0.93, surfacePattern) * foamParams.x;
                float contactFoam = contact * foamParams.y;

                float verticalFacing = 1.0 - saturate(abs(input.normalWS.y) * 2.2);
                float lateral = input.positionWS.x + input.positionWS.z * 0.73;
                float lipTopology = saturate(input.topology.x);
                float impactTopology = saturate(input.topology.y);
                float edgeTopology = saturate(input.topology.z);

                // The curtain carrier is intentionally warped before any narrow streak is formed.
                // Fixed-frequency world-space stripes remain phase-aligned across overlapping voxel
                // bands and read as parallel slabs; descending multi-scale warp breaks that shared
                // phase while retaining coherent downward motion.
                float descend = input.positionWS.y * 0.52 - _WaterTime * motion.w * 2.15;
                float broadWarp = Fbm2(float2(lateral * 0.74 + 17.4, descend * 0.44));
                float fineWarp = Fbm2(float2(lateral * 1.61 - 9.7,
                                             descend * 0.71 + broadWarp * 1.9));
                float warpedLateral = lateral
                                    + (broadWarp - 0.5) * (0.30 + cascade.x * 0.52)
                                    + (fineWarp - 0.5) * 0.14;
                float broadFlow = Fbm2(float2(warpedLateral * 1.32, descend));
                float fallingCells = Fbm2(float2(warpedLateral * 3.20 + broadFlow * 1.8,
                                                  descend * 0.92));
                float downwardStreaks = waterfall
                    ? smoothstep(0.44, 0.84, broadFlow * 0.64 + fallingCells * 0.58) : 0.0;
                float threadNoise = Fbm2(float2(warpedLateral * 3.45 + 7.3,
                                                descend * 0.66 + fineWarp * 1.2));
                float threadCarrier = 0.5 + 0.5 * sin(warpedLateral * 18.0
                                    + broadWarp * 9.0 + descend * 0.28);
                float brightThreads = waterfall
                    ? pow(saturate(threadCarrier), 7.0)
                      * smoothstep(0.48, 0.78, threadNoise)
                      * smoothstep(0.36, 0.80, fallingCells)
                    : 0.0;
                float breakupNoise = Fbm2(float2(warpedLateral * 2.15 + 31.7,
                                                  descend * 0.36 + broadFlow));
                float breakupCells = Fbm2(float2(warpedLateral * 5.4 + fallingCells * 1.7,
                                                  descend * 1.18 - _WaterTime * 0.24));
                float fragmentedFlow = smoothstep(0.34, 0.72, breakupCells);
                float sheetCoverage = waterfall
                    ? saturate(0.07 + downwardStreaks * 0.42
                              + brightThreads * 0.18 + breakupNoise * cascade.x * 0.18
                              + fragmentedFlow * cascade.x * 0.28)
                    : 1.0;

                // The body pass writes depth, so alpha alone cannot create waterfall breakup:
                // nearly transparent front fragments still hide overlapping bands. Punch actual
                // coverage holes only on vertical Waterfall faces while retaining more of the
                // authored lip/impact boundary where reusable foam and spray originate.
                if (waterfall && verticalFacing > 0.5)
                {
                    float boundaryTopology = saturate(lipTopology + impactTopology);
                    float coverageCutoff = lerp(0.18, 0.10, boundaryTopology);
                    clip(sheetCoverage - coverageCutoff);
                }

                float aeration = waterfall
                    ? saturate(0.03 + cascade.x * (broadFlow * 0.24
                              + downwardStreaks * 0.38 + brightThreads * 0.48
                              + fragmentedFlow * 0.22))
                    : 0.0;

                float lipFoam = waterfallMask * verticalFacing * lipTopology * cascade.y
                              * smoothstep(0.32, 0.68, broadFlow);
                float impactNoise = Fbm2(float2(warpedLateral * 2.8,
                    descend * 0.22 - _WaterTime * 0.35));
                float impactFoam = waterfallMask * verticalFacing * impactTopology * cascade.z
                                 * smoothstep(0.22, 0.62, impactNoise);
                float edgeBreakup = waterfallMask * verticalFacing * edgeTopology * cascade.y
                                  * (0.34 + 0.66 * (1.0 - smoothstep(0.30, 0.70, sheetCoverage)));
                float mistNoise = Fbm2(float2(warpedLateral * 0.92 + 41.3,
                    descend * 0.18 - _WaterTime * 0.58));
                float mist = waterfallMask * verticalFacing * impactTopology * cascade.w
                           * smoothstep(0.30, 0.72, mistNoise);

                float foam = saturate(surfaceFoam + contactFoam + aeration * 0.22
                                    + lipFoam + impactFoam * 0.92
                                    + edgeBreakup * 0.50 + brightThreads * 0.18);

                float3 body = lerp(shallow.rgb, deep.rgb, depthT);
                if (waterfall)
                {
                    float verticalEnergy = verticalFacing * saturate(
                        downwardStreaks * 0.44 + brightThreads * 0.58
                      + aeration * 0.28 + fragmentedFlow * 0.14);
                    body = lerp(body, float3(0.18, 0.52, 0.66), verticalFacing * 0.42);
                    body = lerp(body, float3(0.86, 0.96, 0.98), verticalEnergy * 0.36);
                    body *= 0.76 + 0.25 * broadFlow;
                }

                float3 reflectedDirection = reflect(-toCamera, normal);
                float3 reflectedSky = SkyReflection(reflectedDirection);
                float3 refractedDirection = refract(-toCamera, normal, 0.75);
                float3 refractedSky = SkyReflection(refractedDirection);
                body = lerp(body, refractedSky, saturate(detail.z * 8.0) * (1.0 - fresnel));

                float3 halfVector = normalize(normalize(_SunDirection.xyz) + toCamera);
                float specPower = lerp(38.0, 150.0, saturate(detail.w));
                float sunSpecular = pow(saturate(dot(normal, halfVector)), specPower);
                float3 specularColour = float3(1.0, 0.88, 0.69) * sunSpecular * 0.68;

                float3 colour = lerp(body, reflectedSky, saturate(fresnel * 0.82 + 0.08));
                colour += specularColour;
                colour = lerp(colour, float3(0.86, 0.96, 0.98), foam * 0.68);
                colour += float3(0.70, 0.84, 0.86) * mist * 0.34;

                float baseAlpha = lerp(shallow.a * 0.72, shallow.a, depthT);
                float alpha = saturate(lerp(baseAlpha, 0.94, fresnel * 0.34)
                                     + foam * 0.14 + mist * 0.07);
                if (waterfall)
                {
                    float cascadeAlpha = lerp(0.05, 0.58, sheetCoverage)
                                       + brightThreads * 0.06 + aeration * 0.06
                                       + foam * 0.09 + mist * 0.04;
                    alpha = lerp(alpha, saturate(cascadeAlpha), verticalFacing);
                }
                return float4(colour, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VoxelWaterSpray"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertSpray
            #pragma fragment FragSpray
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
            uint _SurfaceVertexBase;
            float4 _WaterMotion[32];
            float4 _WaterCascade[32];
            float _WaterTime;

            struct SprayVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                nointerpolation uint material : TEXCOORD1;
                nointerpolation float spray : TEXCOORD2;
                float2 sprayUv : TEXCOORD3;
            };

            SprayVaryings VertSpray(uint vertexID : SV_VertexID)
            {
                SurfaceVertex vertex = _SurfaceVertices[
                    _SurfaceVertexBase + _SurfaceIndices[_SurfaceIndexBase + vertexID]];
                uint topologyFlags = vertex.material >> 24;
                SprayVaryings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.material = min(vertex.material & 0xFFu, 31u);
                output.spray = (topologyFlags & 8u) != 0u ? 1.0 : 0.0;
                output.sprayUv = float2(
                    (vertex.active & 1u) != 0u ? 1.0 : 0.0,
                    (vertex.active & 2u) != 0u ? 1.0 : 0.0);
                return output;
            }

            float HashSpray21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueSprayNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(HashSpray21(i), HashSpray21(i + float2(1,0)), u.x),
                            lerp(HashSpray21(i + float2(0,1)), HashSpray21(i + float2(1,1)), u.x), u.y);
            }

            float FbmSpray2(float2 p)
            {
                return ValueSprayNoise(p) * 0.67
                     + ValueSprayNoise(p * 2.07 + 19.1) * 0.33;
            }

            float4 FragSpray(SprayVaryings input) : SV_Target
            {
                if (input.spray < 0.5)
                    clip(-1.0);

                uint material = min(input.material, 31u);
                float4 motion = _WaterMotion[material];
                if (motion.x <= 2.5)
                    clip(-1.0);
                float4 cascade = _WaterCascade[material];
                float2 sprayUv = saturate(input.sprayUv);
                float acrossEnvelope = saturate(4.0 * sprayUv.x * (1.0 - sprayUv.x));
                float riseEnvelope = smoothstep(0.015, 0.16, sprayUv.y)
                                   * (1.0 - smoothstep(0.54, 1.0, sprayUv.y));
                float softEnvelope = pow(acrossEnvelope, 0.55) * pow(riseEnvelope, 0.62);
                float sprayLateral = input.positionWS.x + input.positionWS.z * 0.73;
                float sprayRise = input.positionWS.y * 0.64 - _WaterTime * motion.w * 0.74;
                float sprayNoise = FbmSpray2(float2(sprayLateral * 1.38 + 53.2, sprayRise));
                float sprayBreakup = FbmSpray2(float2(sprayLateral * 3.10 - 11.7,
                                                       sprayRise * 0.62 + _WaterTime * 0.31));
                float sprayDensity = saturate(cascade.w) * softEnvelope
                                   * smoothstep(0.25, 0.70,
                                                sprayNoise * 0.72 + sprayBreakup * 0.48);
                clip(sprayDensity - 0.035);
                float3 sprayColour = lerp(float3(0.62, 0.82, 0.87),
                                          float3(0.94, 0.99, 1.00),
                                          saturate(sprayDensity * 2.1));
                return float4(sprayColour, saturate(sprayDensity * 0.62));
            }
            ENDHLSL
        }
    }
}
