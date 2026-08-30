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
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                SurfaceVertex vertex = _SurfaceVertices[_SurfaceIndices[_SurfaceIndexBase + vertexID]];
                Varyings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.normalWS = normalize(vertex.normal);
                output.material = min(vertex.material, 31u);
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
                    float warp = (Fbm2(p.xz * (2.1 * scale) + _WaterTime * 0.17) - 0.5)
                               * cascade.x * 2.4;
                    phaseA = p.y * (4.2 / scale) - _WaterTime * speed * 4.3 + warp;
                    phaseB = p.y * (7.4 / scale) - _WaterTime * speed * 5.9
                           + dot(p.xz, float2(-1.7, 2.3)) + warp * 1.6;
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
                float ndotv = saturate(dot(normal, toCamera));
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

                float upFacing = saturate(input.normalWS.y * 2.4 - 0.45);
                float verticalFacing = 1.0 - saturate(abs(input.normalWS.y) * 2.2);
                float waterfallWarp = Fbm2(input.positionWS.xz * 2.6
                                          + float2(0, -_WaterTime * motion.w));
                float downwardStreaks = waterfall
                    ? pow(saturate(0.5 + 0.5 * sin(input.positionWS.y * 19.0
                              - _WaterTime * motion.w * 9.0
                              + waterfallWarp * 5.0)), 4.0) : 0.0;
                float aeration = waterfall
                    ? saturate(0.28 + cascade.x * (surfacePattern * 0.85 + downwardStreaks * 0.55))
                    : 0.0;

                float lipFoam = waterfallMask * upFacing * cascade.y
                              * smoothstep(0.42, 0.76, waterfallWarp);
                float impactFoam = waterfallMask * upFacing * cascade.z
                                 * smoothstep(0.30, 0.72,
                                     Fbm2(input.positionWS.xz * 4.1 - _WaterTime * 0.8));
                float edgeBreakup = waterfallMask * verticalFacing * cascade.y
                                  * smoothstep(0.62, 0.86,
                                      Fbm2(input.positionWS.xz * 3.7 + input.positionWS.yy * 0.09));
                float mist = waterfallMask * verticalFacing * cascade.w
                           * smoothstep(0.60, 0.90,
                               Fbm2(input.positionWS.xz * 1.5
                                  + float2(_WaterTime * 0.17, -_WaterTime * 0.26)));

                float foam = saturate(surfaceFoam + contactFoam + aeration * 0.42
                                    + lipFoam + impactFoam * 0.78 + edgeBreakup * 0.48);

                float3 body = lerp(shallow.rgb, deep.rgb, depthT);
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
                colour = lerp(colour, float3(0.83, 0.94, 0.96), foam * 0.82);
                colour += float3(0.55, 0.72, 0.74) * mist * 0.26;

                float baseAlpha = lerp(shallow.a * 0.72, shallow.a, depthT);
                float alpha = saturate(lerp(baseAlpha, 0.94, fresnel * 0.34)
                                     + foam * 0.14 + mist * 0.08);
                return float4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
