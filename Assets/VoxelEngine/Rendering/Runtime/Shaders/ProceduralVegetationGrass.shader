Shader "VoxelEngine/ProceduralVegetationGrass"
{
    Properties
    {
        _GrassPlayerPositionWS ("Player Position WS", Vector) = (100000,100000,100000,1)
        _GrassCameraRightWS ("Camera Right WS", Vector) = (1,0,0,0)
        _GrassPushRadius ("Interaction Radius", Float) = 1.05
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
                float2 uv0        : TEXCOORD0; // rootOS.x, rootOS.z
                float2 uv1        : TEXCOORD1; // rootOS.y, baseLateralOffset
                float2 uv2        : TEXCOORD2; // localVerticalOffset, tipFactor
                float2 uv3        : TEXCOORD3; // randomPhase, reserved
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
            CBUFFER_END

            // The gameplay-facing registry already supports multiple characters. The supplied
            // reference shader describes one local push; evaluate that same equation for each
            // registered interactor and use only the strongest influence at a blade root.
            float4 _GrassInteractorPositions[64];
            int _GrassInteractorCount;
            float _ValidationAnimationTime;
            float _UseValidationAnimationTime;

            float Smooth01(float x)
            {
                x = saturate(x);
                return x * x * (3.0 - 2.0 * x);
            }

            void ConsiderInteractor(
                float2 rootXZ,
                float2 rightWS,
                float3 interactorWS,
                float radius,
                inout float bestPush,
                inout float bestAwaySide)
            {
                float2 delta = rootXZ - interactorWS.xz;
                float distanceToInteractor = length(delta) + 1e-4;
                float push = 1.0 - distanceToInteractor / max(radius, 1e-4);
                push = Smooth01(push);
                if (push <= bestPush) return;

                float2 away = delta / distanceToInteractor;
                bestPush = push;
                bestAwaySide = dot(away, rightWS);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // Exact packed construction contract from the supplied reference shader.
                float3 rootOS = float3(input.uv0.x, input.uv1.x, input.uv0.y);
                float3 rootWS = TransformObjectToWorld(rootOS);
                float baseLateral = input.uv1.y;
                float localY = input.uv2.x;
                float tip = input.uv2.y;
                float phase = input.uv3.x;

                float2 rightWS = _GrassCameraRightWS.xz;
                rightWS /= max(length(rightWS), 1e-4);

                float push = 0.0;
                float awaySide = 0.0;
                ConsiderInteractor(
                    rootWS.xz,
                    rightWS,
                    _GrassPlayerPositionWS.xyz,
                    _GrassPushRadius,
                    push,
                    awaySide);

                int count = min(_GrassInteractorCount, 64);
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= count) break;
                    float4 interactor = _GrassInteractorPositions[i];
                    ConsiderInteractor(
                        rootWS.xz,
                        rightWS,
                        interactor.xyz,
                        max(interactor.w, 0.05),
                        push,
                        awaySide);
                }

                // Production uses the engine-managed GPU clock. Validation can override that clock
                // explicitly so deterministic captures can compare two semantic wind states without
                // depending on wall-clock frame advancement.
                float presentationTime = _UseValidationAnimationTime > 0.5
                    ? _ValidationAnimationTime
                    : _Time.y;
                float gust = sin(presentationTime * 0.82 + rootWS.x * 0.26 + rootWS.z * 0.10) * 0.050;
                float wave = sin(presentationTime * 0.46 - rootWS.x * 0.08 + rootWS.z * 0.18) * 0.024;
                float local = sin(presentationTime * 1.06 + phase) * 0.005;
                float wind = gust + wave + local;

                float lateral =
                    baseLateral +
                    wind * tip +
                    awaySide * push * 0.22 * tip;

                float3 finalWS = float3(
                    rootWS.x + rightWS.x * lateral,
                    rootWS.y + localY - push * 0.055 * tip,
                    rootWS.z + rightWS.y * lateral);

                output.positionCS = TransformWorldToHClip(finalWS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Regional variation is packed into vertex colours at construction time, matching
                // the supplied reference's camera/light-invariant pixel-art presentation.
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
