Shader "VoxelEngine/FarTerrain"
{
    // Shading for the clipmap rings that stand in for the voxel world beyond the streaming
    // radius.
    //
    // Clipmap vertices already carry the exact base albedo from the installed renderer material
    // catalogue. Resolve that opaque catalogue row once in the vertex stage, then use the same
    // semantic-free presentation rows as SmoothSurface. Fine and macro response are evaluated per
    // fragment in stable world/voxel coordinates, so material detail frequency is independent of
    // clipmap vertex spacing and never asks the far terrain system for denser geometry or residency.
    //
    // Procedural spatial detail is derivative-filtered. This is the analytic equivalent of choosing
    // an appropriate texture mip: high frequencies fade once a pixel footprint spans them, avoiding
    // kilometre-range shimmer while preserving macro albedo/roughness and subtle detail-normal cues.
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
        _AerialColour ("Aerial Perspective", Color) = (0.62, 0.72, 0.86, 1)
        _AerialDistance ("Aerial Full Distance", Float) = 9000
        _VoxelSize ("Voxel Size", Float) = 0.1
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
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _MaterialAlbedo[32];
            float4 _MaterialSampling[32];
            float4 _MaterialSurface[32];
            float4 _MaterialVariation[32];
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float4 _AerialColour;
            float _AerialDistance;
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
                nointerpolation uint material : TEXCOORD2;
            };

            uint ResolveMaterialRow(float3 vertexAlbedo)
            {
                // Far-world composition currently supplies the renderer catalogue's exact base
                // albedo. Do the lookup per vertex rather than per fragment; the fixed 32-row
                // catalogue keeps this cost bounded by clipmap vertex count, not screen coverage.
                uint bestMaterial = 0u;
                float bestError = 1e20;
                [unroll]
                for (uint material = 0u; material < 32u; material++)
                {
                    float3 delta = vertexAlbedo - _MaterialAlbedo[material].rgb;
                    float error = dot(delta, delta);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestMaterial = material;
                    }
                }
                return bestMaterial;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.material = ResolveMaterialRow(input.color.rgb);
                return output;
            }

            float WaveNoise2(float2 p, float seed)
            {
                float a = sin(dot(p, float2(1.71, 2.37)) + seed * 0.73);
                float b = sin(dot(p, float2(-2.11, 1.43)) - seed * 0.41);
                return a * b;
            }

            float SpatialNoise(float3 positionVoxel, float3 normalWS, float frequency, float seed, float triplanarBlend)
            {
                float3 p = positionVoxel * frequency;
                float planar = WaveNoise2(p.xz, seed);

                float3 weights = pow(abs(normalWS), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 1e-4);
                float triX = WaveNoise2(p.zy, seed + 3.1);
                float triY = WaveNoise2(p.xz, seed + 7.3);
                float triZ = WaveNoise2(p.xy, seed + 11.7);
                float triplanar = dot(float3(triX, triY, triZ), weights);
                return lerp(planar, triplanar, saturate(triplanarBlend));
            }

            float SpatialDetailFilter(float3 positionVoxel, float frequency)
            {
                // Derivative footprint is an analytic mip selector for procedural world-space
                // detail. Once one pixel spans roughly one pattern period the high-frequency term
                // fades continuously instead of aliasing or shimmering during camera motion.
                float3 scaled = positionVoxel * frequency;
                float footprint = max(length(ddx(scaled)), length(ddy(scaled)));
                return 1.0 - smoothstep(0.35, 1.25, footprint);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                uint material = min(input.material, 31u);
                float3 baseAlbedo = _MaterialAlbedo[material].rgb;
                float4 sampling = _MaterialSampling[material];
                float4 surface = _MaterialSurface[material];
                float4 variation = _MaterialVariation[material];

                // Material UV scale is shared application presentation. Evaluate it against stable
                // voxel/world coordinates per fragment so a 96-cell clipmap triangle and a denser
                // inner triangle have the same apparent material frequency.
                float3 hitVoxel = input.positionWS / max(_VoxelSize, 1e-4);
                float detailFrequency = max(abs(surface.x), 1.0 / 512.0);
                float macroFrequency = max(detailFrequency * 0.12, 1.0 / 2048.0);
                float projectionBlend = sampling.z >= 0.5 ? 1.0 : 0.35;
                float detailFade = SpatialDetailFilter(hitVoxel, detailFrequency);
                float macroFade = SpatialDetailFilter(hitVoxel, macroFrequency);

                float fineNoise = SpatialNoise(
                    hitVoxel, n, detailFrequency, material * 0.71 + 1.0, projectionBlend);
                float macroNoise = SpatialNoise(
                    hitVoxel, n, macroFrequency, material * 1.37 + 5.0, 1.0);

                float luminanceVariation = fineNoise * variation.y * 0.12 * detailFade
                                         + macroNoise * variation.w * 0.22 * macroFade;
                float3 chromaAxis = float3(fineNoise, macroNoise, -fineNoise)
                                  * variation.z * 0.035 * detailFade;
                float3 albedo = saturate(baseAlbedo * (1.0 + luminanceVariation) + chromaAxis);

                // A tiny derivative-filtered tangent perturbation supplies the far material's
                // configured detail-normal response without changing clipmap geometry. It fades by
                // the same footprint rule as albedo detail, so distant pixels converge to the
                // geometric normal instead of sparkling.
                float3 detailVector = float3(
                    SpatialNoise(hitVoxel + float3(17.0, 0.0, 0.0), n, detailFrequency, material + 13.0, 1.0),
                    SpatialNoise(hitVoxel + float3(0.0, 23.0, 0.0), n, detailFrequency, material + 19.0, 1.0),
                    SpatialNoise(hitVoxel + float3(0.0, 0.0, 29.0), n, detailFrequency, material + 29.0, 1.0));
                detailVector -= n * dot(detailVector, n);
                float detailNormalStrength = saturate(abs(surface.y))
                                           * saturate(abs(variation.y))
                                           * detailFade * 0.35;
                n = normalize(n + detailVector * detailNormalStrength);

                float roughness = saturate(surface.z
                    + macroNoise * variation.w * 0.07 * macroFade
                    + fineNoise * variation.y * 0.035 * detailFade);

                float3 sun = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, sun));
                float skyT = saturate(n.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);
                float sunResponse = lerp(0.72, 0.58, roughness);

                float3 lit = albedo * (ambient * 0.42 + (0.34 + ndl * sunResponse));

                // Aerial perspective. Without it a 5 km summit reads as a cardboard cutout at
                // the same contrast as ground a hundred metres away, and the range loses all
                // sense of depth. Distance is measured to the camera, so it also hides the
                // outermost ring's low sample rate.
                float distance = length(input.positionWS - _WorldSpaceCameraPos);
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
