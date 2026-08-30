Shader "Hidden/VoxelEngine/Tests/IndirectStartInstanceProbe"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation uint instance : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                float2 position = vertexID == 0u ? float2(-1.0, -1.0)
                                : vertexID == 1u ? float2(3.0, -1.0)
                                                 : float2(-1.0, 3.0);
                Varyings output;
                output.positionCS = float4(position, 0.0, 1.0);
                output.instance = instanceID;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return input.instance > 0u ? float4(1, 0, 0, 1) : float4(0, 0, 1, 1);
            }
            ENDHLSL
        }
    }
}
