Shader "VoxelEngine/FarTerrain"
{
    // Shading for the clipmap rings that stand in for the voxel world beyond the streaming
    // radius.
    //
    // The one thing this must do that URP/Lit does not is respect vertex colour. The rings carry
    // their material in COLOR, sampled from the same VoxelPresentationCatalogue albedo the near
    // field reads, because the far field previously had no material channel at all and rendered
    // the entire range as one flat grey.
    //
    // The lighting deliberately matches ProceduralTreeBark rather than going through the full URP
    // lit path: the far field is thousands of square kilometres of terrain that is never closer
    // than the streaming radius, so a sun term plus a sky-gradient ambient is all the fidelity
    // that survives the distance, and it keeps the horizon consistent with the foliage in front
    // of it.
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
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float4 _AerialColour;
            float _AerialDistance;

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

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, sun));
                float skyT = saturate(n.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);

                float3 lit = input.color.rgb * (ambient * 0.42 + (0.34 + ndl * 0.66));

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
