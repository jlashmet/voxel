Shader "SceneIssue/WorldbuildingGalleryGrassReference"
{
    Properties
    {
        _GrassPlayerPositionWS ("Player Position WS", Vector) = (0,0,0,0)
        _GrassCameraRightWS ("Camera Right WS", Vector) = (1,0,0,0)
        _GrassPushRadius ("Interaction Radius", Float) = 1.05
        _GrassTime ("Deterministic Grass Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "UniversalMaterialType"="Unlit"
        }

        Cull Off
        ZWrite On
        Blend One Zero

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;

                // Packed by the CPU construction path.
                // uv0 = (rootOS.x, rootOS.z)
                float2 uv0        : TEXCOORD0;
                // uv1 = (rootOS.y, baseLateralOffset)
                float2 uv1        : TEXCOORD1;
                // uv2 = (localVerticalOffset, tipFactor)
                float2 uv2        : TEXCOORD2;
                // uv3 = (randomPhase, reserved)
                float2 uv3        : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                half fogFactor    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassPlayerPositionWS;
                float4 _GrassCameraRightWS;
                float _GrassPushRadius;
                float _GrassTime;
            CBUFFER_END

            float Smooth01(float x)
            {
                x = saturate(x);
                return x * x * (3.0 - 2.0 * x);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // CPU emits the root once and packs it into UV channels. The
                // per-frame blade shape is reconstructed in world space here.
                float3 rootOS = float3(input.uv0.x, input.uv1.x, input.uv0.y);
                float3 rootWS = TransformObjectToWorld(rootOS);

                float baseLateral = input.uv1.y;
                float localY = input.uv2.x;
                float tip = input.uv2.y;
                float phase = input.uv3.x;

                float2 rightWS = _GrassCameraRightWS.xz;
                float rightLen = max(length(rightWS), 1e-4);
                rightWS /= rightLen;

                float2 playerDelta = rootWS.xz - _GrassPlayerPositionWS.xz;
                float playerDistance = length(playerDelta) + 1e-4;
                float push = 1.0 - playerDistance / max(_GrassPushRadius, 1e-4);
                push = Smooth01(push);
                float2 away = playerDelta / playerDistance;
                float awaySide = dot(away, rightWS);

                // Exact coherent-wave constants from the approved GPU browser
                // prototype. Keep these until visual parity is established.
                float gust = sin(_GrassTime * 0.82 + rootWS.x * 0.26 + rootWS.z * 0.10) * 0.050;
                float wave = sin(_GrassTime * 0.46 - rootWS.x * 0.08 + rootWS.z * 0.18) * 0.024;
                float local = sin(_GrassTime * 1.06 + phase) * 0.005;
                float wind = gust + wave + local;

                float lateral =
                    baseLateral +
                    wind * tip +
                    awaySide * push * 0.22 * tip;

                float3 finalWS = float3(
                    rootWS.x + rightWS.x * lateral,
                    rootWS.y + localY - push * 0.055 * tip,
                    rootWS.z + rightWS.y * lateral
                );

                output.positionCS = TransformWorldToHClip(finalWS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Camera/light-invariant stylized output matching the accepted
                // browser renderer. Regional Perlin variation is already baked
                // into vertex colors by the CPU construction stage.
                float3 c = input.color.rgb;
                c = c * float3(1.08, 1.10, 1.03) + float3(0.015, 0.018, 0.004);
                c = saturate(c);
                c = MixFog(c, input.fogFactor);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
