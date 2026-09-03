Shader "VoxelEngine/FarTerrain"
{
    // Shading for the clipmap rings that stand in for the voxel world beyond the streaming
    // radius.
    //
    // Clipmap vertices already carry the exact base albedo from the installed renderer material
    // catalogue. Resolve that opaque catalogue row once in the vertex stage, then use the same
    // semantic-free presentation rows and deterministic world-space variation as SmoothSurface.
    // This preserves the existing application-owned terrain-family selection while avoiding the
    // old far-only flat-colour path that made mountains read as smooth dough.
    //
    // The far pass intentionally omits close-range normal/texture sampling: those frequencies do
    // not survive kilometres of distance. Material family, macro/fine variation, geometric slope,
    // lighting and aerial perspective remain continuous with the near renderer without increasing
    // clipmap geometry density.
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

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                uint material = min(input.material, 31u);
                float3 baseAlbedo = _MaterialAlbedo[material].rgb;
                float4 variation = _MaterialVariation[material];

                // Match SmoothSurface's deterministic fine/macro material response in the same
                // voxel-space coordinates. Geometric clipmap normals supply the shared slope input;
                // application terrain-family choice remains the opaque material row selected by
                // deterministic terrain generation/composition.
                float3 hitVoxel = input.positionWS / max(_VoxelSize, 1e-4);
                float fineNoise = sin(dot(hitVoxel, float3(0.33, 0.27, 0.39)) + material * 0.71)
                                * sin(dot(hitVoxel, float3(-0.21, 0.43, 0.17)) - material * 0.37);
                float macroNoise = sin(dot(hitVoxel, float3(0.041, 0.029, 0.037)) + material)
                                 * sin(dot(hitVoxel, float3(-0.023, 0.035, 0.031)));
                float3 albedo = baseAlbedo
                              * (1.0 + fineNoise * variation.w * 0.24
                                     + macroNoise * variation.w);

                float3 sun = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, sun));
                float skyT = saturate(n.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);
                float roughness = saturate(_MaterialSurface[material].z);
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
