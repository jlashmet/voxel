Shader "Hidden/VoxelEngine/IndexedIndirectContract"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            StructuredBuffer<float3> _Positions;
            float _YOffset;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float4 color : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                uint commandID = GetCommandID(0);
                float3 position = _Positions[GetIndirectVertexID(vertexID)];
                position.y += _YOffset;

                Varyings output;
                output.positionCS = float4(position.xy, 0.0, 1.0);
                if (commandID == 0u) output.color = float4(1, 0, 0, 1);
                else if (commandID == 1u) output.color = float4(0, 1, 0, 1);
                else if (commandID == 2u) output.color = float4(0, 0.35, 1, 1);
                else if (commandID == 3u) output.color = float4(1, 1, 0, 1);
                else output.color = float4(1, 0, 1, 1);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
