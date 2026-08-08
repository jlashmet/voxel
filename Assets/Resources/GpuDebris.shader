Shader "VoxelEngine/GpuDebris"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "GpuDebris"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DebrisState
            {
                float4 positionAge;
                float4 rotation;
                float4 velocitySettled;
                float4 angularGround;
                float4 contactActive;
            };

            struct DebrisInstance
            {
                float4 localSlot;
                float4 colour;
            };

            StructuredBuffer<DebrisState> _States;
            StructuredBuffer<DebrisInstance> _Instances;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 colour : TEXCOORD0;
                half lighting : TEXCOORD1;
            };

            float3 Rotate(float4 q, float3 v)
            {
                return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                DebrisInstance instance = _Instances[input.instanceID];
                uint slot = (uint)(instance.localSlot.w + 0.5);
                DebrisState state = _States[slot];

                float3 worldPosition = state.positionAge.xyz
                    + Rotate(state.rotation, instance.localSlot.xyz
                                            + input.positionOS * (0.098 * instance.colour.a));
                if (state.contactActive.y < 0.5 || instance.colour.a <= 0.0)
                    worldPosition = float3(0.0, -100000.0, 0.0);

                float3 normalWS = Rotate(state.rotation, input.normalOS);
                Light light = GetMainLight();
                output.lighting = saturate(dot(normalWS, light.direction)) * 0.65 + 0.35;
                output.colour = instance.colour.rgb;
                output.positionCS = TransformWorldToHClip(worldPosition);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.colour * input.lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
