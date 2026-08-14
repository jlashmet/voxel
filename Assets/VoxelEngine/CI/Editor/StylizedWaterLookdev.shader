Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.04,0.34,0.58,1)
        _MidColor ("Mid Color", Color) = (0.08,0.60,0.80,1)
        _ShallowColor ("Shallow Color", Color) = (0.30,0.82,0.93,1)
        _FoamColor ("Foam Color", Color) = (0.90,0.97,0.98,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.22
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.36
        _Alpha ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "StylizedMaskedWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_ReferenceTex);
            SAMPLER(sampler_ReferenceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ReferenceTex_ST;
                float4 _ReferenceTex_TexelSize;
                float4 _DeepColor;
                float4 _MidColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float _FlowSpeed;
                float _FlowStrength;
                float _Shimmer;
                float _EdgeFoam;
                float _Alpha;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                            lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x),f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k=0;k<4;k++) { v += noise(p)*a; p=p*2.01+float2(13.7,8.9); a*=0.5; }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _ReferenceTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                half rawMask = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv).r;
                half mask = smoothstep(0.10h, 0.46h, rawMask);
                clip(mask - 0.07h);

                float time = _Time.y * _FlowSpeed;
                float2 texel = _ReferenceTex_TexelSize.xy * 2.5;
                half xL = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(-texel.x,0)).r;
                half xR = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2( texel.x,0)).r;
                half yD = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0,-texel.y)).r;
                half yU = SAMPLE_TEXTURE2D(_ReferenceTex, sampler_ReferenceTex, uv + float2(0, texel.y)).r;

                float horizontalEdge = abs(xR-xL);
                float verticalEdge = abs(yU-yD);
                float fallLocal = saturate((horizontalEdge-verticalEdge)*3.8 + smoothstep(0.67,0.95,uv.y)*0.12);

                float broad = fbm(uv*float2(7.0,9.0)+float2(time*0.18,-time*0.26));
                float fine = fbm(uv*float2(18.0,21.0)+float2(-time*0.12,time*0.19)+5.2);
                float depth = saturate(0.22 + (1.0-uv.y)*0.40 + broad*0.32);
                half3 color = lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.28,0.70,depth));
                color = lerp(color,_DeepColor.rgb,smoothstep(0.68,0.98,depth));

                // Broad painterly patches instead of a uniform caustic lattice.
                float patches = smoothstep(0.48,0.78,broad*0.70+fine*0.45);
                color = lerp(color,lerp(color,_ShallowColor.rgb,0.42),patches*0.46*(1.0-fallLocal));

                // Calm pools get sparse horizontal broken highlights.
                float poolWave = sin(uv.y*78.0 + broad*11.0 + uv.x*7.0 + time*1.8);
                poolWave = pow(saturate(poolWave*0.5+0.5),18.0);
                float poolBreak = smoothstep(0.50,0.72,fine);
                color = lerp(color,_FoamColor.rgb,poolWave*poolBreak*_Shimmer*(1.0-fallLocal)*0.72);

                // Falls get vertically flowing, soft white-blue ribs with irregular spacing.
                float fallPhase = uv.x*72.0 + broad*17.0 - time*5.4;
                float ribs = pow(saturate(sin(fallPhase)*0.5+0.5),7.0);
                float fallMist = smoothstep(0.45,0.72,fbm(uv*float2(24,9)+float2(0,-time*1.6)));
                color = lerp(color,_FoamColor.rgb,fallLocal*(ribs*0.30 + fallMist*0.18));

                // Foam belongs on selected lips/impact edges, not around the entire silhouette.
                float topLip = saturate((rawMask-yU)*4.5);
                float bottomImpact = saturate((rawMask-yD)*4.0) * fallLocal;
                float foamNoise = smoothstep(0.42,0.68,fbm(uv*34.0+float2(time*0.4,-time*0.7)));
                float lipFoam = saturate((topLip*0.72 + bottomImpact*0.42) * foamNoise * _EdgeFoam);
                color = lerp(color,_FoamColor.rgb,lipFoam);

                // Small isolated glints, deliberately sparse.
                float glint = pow(saturate(sin((uv.x*1.7-uv.y)*54.0 + fine*8.0 + time)*0.5+0.5),30.0);
                color = lerp(color,_FoamColor.rgb,glint*smoothstep(0.60,0.78,broad)*0.16);

                return half4(saturate(color), saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}