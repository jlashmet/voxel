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
        _UsePreviewTime ("Use Preview Time", Float) = 0
        _PreviewTime ("Preview Time", Float) = 0
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
                float _UsePreviewTime;
                float _PreviewTime;
            CBUFFER_END

            float hash21(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}            
            float noise(float2 p){float2 ii=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);return lerp(lerp(hash21(ii),hash21(ii+float2(1,0)),f.x),lerp(hash21(ii+float2(0,1)),hash21(ii+float2(1,1)),f.x),f.y);}            
            float fbm(float2 p){float v=0.0,a=0.5;[unroll]for(int k=0;k<5;k++){v+=noise(p)*a;p=p*2.03+float2(13.7,8.9);a*=0.5;}return v;}
            float boxMask(float2 uv,float2 c,float2 h,float f){float2 d=abs(uv-c)-h;return 1.0-smoothstep(-f,f,max(d.x,d.y));}

            float authoredFallMask(float2 uv)
            {
                float f=0.0;
                f=max(f,boxMask(uv,float2(0.765,0.935),float2(0.075,0.060),0.014));
                f=max(f,boxMask(uv,float2(0.690,0.790),float2(0.075,0.075),0.016));
                f=max(f,boxMask(uv,float2(0.295,0.615),float2(0.070,0.075),0.016));
                f=max(f,boxMask(uv,float2(0.815,0.555),float2(0.030,0.040),0.012));
                return saturate(f);
            }

            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=TRANSFORM_TEX(input.uv,_ReferenceTex);return o;}

            half4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                half rawMask=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv).r;
                half mask=smoothstep(0.10h,0.46h,rawMask);
                clip(mask-0.07h);

                float sourceTime=lerp(_Time.y,_PreviewTime,step(0.5,_UsePreviewTime));
                float time=sourceTime*_FlowSpeed;
                float fall=authoredFallMask(uv)*mask;
                float pool=1.0-fall;

                float2 poolUvA=uv+float2(time*0.090+sin(uv.y*11.0+time*1.7)*_FlowStrength,time*0.012);
                float2 poolUvB=uv+float2(-time*0.052+sin(uv.y*7.0-time*1.2)*_FlowStrength*0.7,-time*0.009);
                float2 fallUv=uv+float2(sin(uv.y*24.0+time*5.0)*_FlowStrength*0.8,time*0.62);

                float broadA=fbm(poolUvA*float2(6.3,7.8));
                float broadB=fbm(poolUvB*float2(10.7,12.7)+4.9);
                float detail=fbm(poolUvA*float2(29.0,31.0)+11.4);
                float micro=fbm(poolUvB*float2(52.0,45.0)+2.8);

                float depth=saturate(0.15+(1.0-uv.y)*0.38+broadA*0.44-broadB*0.13);
                half3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.22,0.64,depth));
                color=lerp(color,_DeepColor.rgb,smoothstep(0.61,0.93,depth));

                float palePatch=smoothstep(0.53,0.76,broadB+(detail-0.5)*0.28);
                float deepPatch=smoothstep(0.57,0.80,broadA-(detail-0.5)*0.22);
                color=lerp(color,_ShallowColor.rgb,palePatch*0.30*pool);
                color=lerp(color,_DeepColor.rgb,deepPatch*0.22*pool);

                float lineA=pow(saturate(sin(poolUvA.y*118.0+poolUvA.x*7.0+broadA*14.0)*0.5+0.5),18.0);
                float lineB=pow(saturate(sin(poolUvB.y*61.0-poolUvB.x*13.0+broadB*11.0)*0.5+0.5),22.0);
                float lineC=pow(saturate(sin(poolUvA.y*176.0+detail*16.0)*0.5+0.5),27.0);
                float breakup=smoothstep(0.46,0.68,detail)*smoothstep(0.40,0.64,micro+0.08);
                float poolFoam=saturate((lineA*0.78+lineB*0.56+lineC*0.30)*breakup*pool);
                color=lerp(color,_FoamColor.rgb,poolFoam*0.68);

                float dabCell=hash21(floor(poolUvA*float2(61.0,79.0)));
                float dabs=step(0.82,dabCell)*smoothstep(0.46,0.68,micro)*pool*smoothstep(0.18,0.55,mask);
                color=lerp(color,_FoamColor.rgb,dabs*0.46);

                float fallNoise=fbm(float2(fallUv.x*30.0+broadA*2.0,fallUv.y*8.0));
                float ribs=pow(saturate(sin(uv.x*91.0+fallNoise*16.0)*0.5+0.5),5.5);
                float thin=pow(saturate(sin(uv.x*166.0+detail*12.0)*0.5+0.5),9.0);
                float downPulseA=pow(saturate(sin((uv.y+time*0.76)*54.0+fallNoise*9.0)*0.5+0.5),4.2);
                float downPulseB=pow(saturate(sin((uv.y+time*1.08)*92.0+detail*7.0)*0.5+0.5),7.0);
                float fallWhite=saturate((ribs*0.62+thin*0.34)*(0.56+downPulseA*0.34)+downPulseB*0.22);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.74);
                color=lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*smoothstep(0.50,0.76,broadB)*0.24);

                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*5.8)*fall;
                float bottom=saturate((rawMask-mDn)*5.2)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.0);
                float foamNoise=smoothstep(0.36,0.64,fbm(fallUv*43.0));
                color=lerp(color,_FoamColor.rgb,saturate((topLip+bottom*0.76)*foamNoise)*0.96);

                float chipped=edgeAny*smoothstep(0.34,0.62,fbm(poolUvB*43.0))*pool*_EdgeFoam*0.40;
                color=lerp(color,_FoamColor.rgb,chipped);

                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
