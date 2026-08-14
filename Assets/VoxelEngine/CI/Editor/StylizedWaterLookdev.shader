Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.035,0.30,0.55,1)
        _MidColor ("Mid Color", Color) = (0.07,0.58,0.79,1)
        _ShallowColor ("Shallow Color", Color) = (0.30,0.82,0.94,1)
        _FoamColor ("Foam Color", Color) = (0.96,0.99,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.30
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.52
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
                p = frac(p * float2(123.34,345.45));
                p += dot(p,p+34.345);
                return frac(p.x*p.y);
            }

            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3.0-2.0*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                            lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x),f.y);
            }

            float fbm(float2 p)
            {
                float v=0.0,a=0.5;
                [unroll] for(int k=0;k<5;k++) { v+=noise(p)*a; p=p*2.03+float2(13.7,8.9); a*=0.5; }
                return v;
            }

            float boxMask(float2 uv,float2 center,float2 halfSize,float feather)
            {
                float2 d=abs(uv-center)-halfSize;
                return 1.0-smoothstep(-feather,feather,max(d.x,d.y));
            }

            float authoredFallMask(float2 uv)
            {
                float f=0.0;
                f=max(f,boxMask(uv,float2(0.735,0.925),float2(0.090,0.070),0.018));
                f=max(f,boxMask(uv,float2(0.635,0.760),float2(0.095,0.102),0.020));
                f=max(f,boxMask(uv,float2(0.815,0.640),float2(0.052,0.068),0.017));
                f=max(f,boxMask(uv,float2(0.285,0.575),float2(0.080,0.098),0.020));
                f=max(f,boxMask(uv,float2(0.755,0.535),float2(0.040,0.047),0.015));
                return saturate(f);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=TRANSFORM_TEX(input.uv,_ReferenceTex);
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                half rawMask=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv).r;
                half mask=smoothstep(0.10h,0.46h,rawMask);
                clip(mask-0.07h);

                float time=_Time.y*_FlowSpeed;
                float fall=authoredFallMask(uv)*mask;
                float pool=1.0-fall;

                float broadA=fbm(uv*float2(6.3,7.8)+float2(time*0.10,-time*0.14));
                float broadB=fbm(uv*float2(10.7,12.7)+float2(-time*0.08,time*0.11)+4.9);
                float detail=fbm(uv*float2(29.0,31.0)+float2(time*0.22,-time*0.28)+11.4);
                float micro=fbm(uv*float2(52.0,45.0)+float2(-time*0.20,time*0.16)+2.8);

                float depth=saturate(0.15+(1.0-uv.y)*0.38+broadA*0.44-broadB*0.13);
                half3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.22,0.64,depth));
                color=lerp(color,_DeepColor.rgb,smoothstep(0.61,0.93,depth));

                // Broad brushy paint variation on horizontal water.
                float palePatch=smoothstep(0.53,0.76,broadB+(detail-0.5)*0.28);
                float deepPatch=smoothstep(0.57,0.80,broadA-(detail-0.5)*0.22);
                color=lerp(color,_ShallowColor.rgb,palePatch*0.30*pool);
                color=lerp(color,_DeepColor.rgb,deepPatch*0.22*pool);

                // Two bands of broken horizontal foam strokes plus small flecks.
                float lineA=pow(saturate(sin(uv.y*118.0+uv.x*7.0+broadA*14.0+time*1.2)*0.5+0.5),20.0);
                float lineB=pow(saturate(sin(uv.y*61.0-uv.x*13.0+broadB*11.0-time*0.75)*0.5+0.5),24.0);
                float lineC=pow(saturate(sin(uv.y*176.0+detail*16.0+time*1.7)*0.5+0.5),29.0);
                float breakup=smoothstep(0.49,0.70,detail)*smoothstep(0.42,0.65,micro+0.08);
                float poolFoam=saturate((lineA*0.68+lineB*0.48+lineC*0.24)*breakup*pool);
                color=lerp(color,_FoamColor.rgb,poolFoam*0.56);

                // Tiny irregular white brush dabs across larger pools.
                float dabCell=hash21(floor(uv*float2(61.0,79.0)));
                float dabs=step(0.86,dabCell)*smoothstep(0.48,0.69,micro)*pool;
                dabs*=smoothstep(0.18,0.55,mask);
                color=lerp(color,_FoamColor.rgb,dabs*0.38);

                // Waterfall sheets: bright vertical ribbons separated by blue troughs.
                float fallNoise=fbm(float2(uv.x*30.0+broadA*2.0,uv.y*8.0-time*2.7));
                float ribs=pow(saturate(sin(uv.x*91.0+fallNoise*16.0-time*8.2)*0.5+0.5),5.5);
                float thin=pow(saturate(sin(uv.x*166.0+detail*12.0-time*10.7)*0.5+0.5),9.0);
                float fallWhite=saturate(ribs*0.62+thin*0.34);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.70);
                color=lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*smoothstep(0.50,0.76,broadB)*0.24);

                // Turbulent lip and impact foam.
                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*5.8)*fall;
                float bottom=saturate((rawMask-mDn)*5.2)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.0);
                float foamNoise=smoothstep(0.36,0.64,fbm(uv*43.0+float2(time*0.4,-time*0.7)));
                color=lerp(color,_FoamColor.rgb,saturate((topLip+bottom*0.76)*foamNoise)*0.94);

                // Chipped pool shoreline accent, modest rather than sticker-like.
                float chipped=edgeAny*foamNoise*pool*_EdgeFoam*0.34;
                color=lerp(color,_FoamColor.rgb,chipped);

                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
