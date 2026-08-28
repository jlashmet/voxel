Shader "VoxelEngine/ProceduralVegetationGrass"
{
    Properties
    {
        _GrassPlayerPositionWS ("Player Position WS", Vector) = (0,0,0,0)
        _GrassCameraRightWS ("Camera Right WS", Vector) = (1,0,0,0)
        _GrassPushRadius ("Interaction Radius", Float) = 0
        _GrassTime ("Grass Time", Float) = 0
        [HideInInspector] _UseValidationAnimationTime ("Use Validation Animation Time", Float) = 0
        [HideInInspector] _ValidationAnimationTime ("Validation Animation Time", Float) = 0
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                // Packed by the CPU construction path, matching the supplied reference:
                // uv0 = (rootOS.x, rootOS.z)
                float2 uv0 : TEXCOORD0;
                // uv1 = (rootOS.y, baseLateralOffset)
                float2 uv1 : TEXCOORD1;
                // uv2 = (localVerticalOffset, tipFactor)
                float2 uv2 : TEXCOORD2;
                // uv3 = (randomPhase, reserved)
                float2 uv3 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassPlayerPositionWS;
                float4 _GrassCameraRightWS;
                float _GrassPushRadius;
                float _GrassTime;
                float _UseValidationAnimationTime;
                float _ValidationAnimationTime;
            CBUFFER_END

            // The gallery can contain more than one player. The existing production bridge already
            // publishes this bounded array, so the migrated single-player reference math is applied
            // independently and the strongest local influence wins.
            int _GrassInteractorCount;
            float4 _GrassInteractorPositions[64];

            float Smooth01(float x)
            {
                x = saturate(x);
                return x * x * (3.0 - 2.0 * x);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // CPU emits the root once and packs it into UV channels. The per-frame blade shape
                // is reconstructed in world space here, exactly like the supplied browser shader.
                float3 rootOS = float3(input.uv0.x, input.uv1.x, input.uv0.y);
                float3 rootWS = TransformObjectToWorld(rootOS);

                float baseLateral = input.uv1.y;
                float localY = input.uv2.x;
                float tip = input.uv2.y;
                float phase = input.uv3.x;

                // The reference receives camera-right from the browser host. In Unity derive the
                // same world-space direction from the active camera so ribbons remain readable as
                // the player orbits the gallery without adding a per-camera CPU update path.
                float2 toCamera = _WorldSpaceCameraPos.xz - rootWS.xz;
                float cameraDistance = max(length(toCamera), 1e-4);
                float2 rightWS = float2(toCamera.y, -toCamera.x) / cameraDistance;

                float strongestPush = 0.0;
                float strongestAwaySide = 0.0;
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= _GrassInteractorCount) break;
                    float4 interactor = _GrassInteractorPositions[i];
                    float radius = max(interactor.w, 1e-4);
                    float2 playerDelta = rootWS.xz - interactor.xz;
                    float playerDistance = length(playerDelta) + 1e-4;
                    float push = Smooth01(1.0 - playerDistance / radius);
                    if (push > strongestPush)
                    {
                        float2 away = playerDelta / playerDistance;
                        strongestPush = push;
                        strongestAwaySide = dot(away, rightWS);
                    }
                }

                float grassTime = _UseValidationAnimationTime > 0.5
                    ? _ValidationAnimationTime
                    : _Time.y;

                // Exact coherent-wave constants from the supplied/approved reference.
                float gust = sin(grassTime * 0.82 + rootWS.x * 0.26 + rootWS.z * 0.10) * 0.050;
                float wave = sin(grassTime * 0.46 - rootWS.x * 0.08 + rootWS.z * 0.18) * 0.024;
                float local = sin(grassTime * 1.06 + phase) * 0.005;
                float wind = gust + wave + local;

                float lateral =
                    baseLateral +
                    wind * tip +
                    strongestAwaySide * strongestPush * 0.22 * tip;

                float3 finalWS = float3(
                    rootWS.x + rightWS.x * lateral,
                    rootWS.y + localY - strongestPush * 0.055 * tip,
                    rootWS.z + rightWS.y * lateral);

                output.positionCS = TransformWorldToHClip(finalWS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Camera/light-invariant stylized output matching the supplied reference. Regional
                // variation is baked into vertex colors by the construction-only CPU mesh step.
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
