Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.025,0.25,0.54,1)
        _MidColor ("Mid Color", Color) = (0.045,0.60,0.84,1)
        _ShallowColor ("Shallow Color", Color) = (0.35,0.88,0.99,1)
        _FoamColor ("Foam Color", Color) = (0.99,0.998,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.34
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.44
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
            TEXTURE2D(_ReferenceTex); SAMPLER(sampler_ReferenceTex);
            TEXTURE2D(_AuthoredWaterTex); SAMPLER(sampler_AuthoredWaterTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ReferenceTex_ST; float4 _ReferenceTex_TexelSize;
                float4 _DeepColor; float4 _MidColor; float4 _ShallowColor; float4 _FoamColor;
                float _FlowSpeed; float _FlowStrength; float _Shimmer; float _EdgeFoam; float _Alpha;
                float _UsePreviewTime; float _PreviewTime;
            CBUFFER_END
            float hash21(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
            float noise(float2 p){float2 ii=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);return lerp(lerp(hash21(ii),hash21(ii+float2(1,0)),f.x),lerp(hash21(ii+float2(0,1)),hash21(ii+float2(1,1)),f.x),f.y);}
            float fbm(float2 p){float v=0.0,a=0.5;[unroll]for(int k=0;k<5;k++){v+=noise(p)*a;p=p*2.03+float2(13.7,8.9);a*=0.5;}return v;}
            float boxMask(float2 uv,float2 c,float2 h,float f){float2 d=abs(uv-c)-h;return 1.0-smoothstep(-f,f,max(d.x,d.y));}
            float authoredFallMask(float2 uv){float f=0.0;f=max(f,boxMask(uv,float2(0.765,0.935),float2(0.075,0.060),0.014));f=max(f,boxMask(uv,float2(0.690,0.790),float2(0.075,0.075),0.016));f=max(f,boxMask(uv,float2(0.295,0.615),float2(0.070,0.075),0.016));f=max(f,boxMask(uv,float2(0.815,0.555),float2(0.030,0.040),0.012));return saturate(f);}
            float authoredLipMask(float2 uv){float f=0.0;f=max(f,boxMask(uv,float2(0.765,0.974),float2(0.078,0.004),0.004));f=max(f,boxMask(uv,float2(0.690,0.842),float2(0.078,0.004),0.004));f=max(f,boxMask(uv,float2(0.295,0.664),float2(0.074,0.004),0.004));f=max(f,boxMask(uv,float2(0.815,0.584),float2(0.034,0.003),0.003));return saturate(f);}
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=TRANSFORM_TEX(input.uv,_ReferenceTex);return o;}
            float brushMark(float2 p,float sx,float sy,float seed,float density,float minLen,float maxLen)
            {
                float2 g=p*float2(sx,sy); float2 cell=floor(g); float2 f=frac(g);
                float r0=hash21(cell+seed),r1=hash21(cell+seed+3.7),r2=hash21(cell+seed+9.3),r3=hash21(cell+seed+17.9),r4=hash21(cell+seed+27.1);
                float cx=0.16+0.68*r1,cy=0.16+0.68*r2;
                float halfLen=minLen+(maxLen-minLen)*r3; float thick=0.038+0.052*r4;
                float slope=(hash21(cell+seed+37.2)-0.5)*0.44;
                float dx=abs(f.x-cx); float dy=abs((f.y-cy)+slope*(f.x-cx));
                float xs=1.0-smoothstep(halfLen,halfLen+0.052,dx); float ys=1.0-smoothstep(thick,thick+0.024,dy);
                return xs*ys*step(density,r0);
            }
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
                float2 poolUvB=uv+float2(-time*0.050+sin(uv.y*7.0-time*1.2)*_FlowStrength*0.7,-time*0.009);
                float2 fallUv=uv+float2(sin(uv.y*24.0+time*5.0)*_FlowStrength*0.8,time*0.64);

                float broadA=fbm(poolUvA*float2(6.8,8.3));
                float broadB=fbm(poolUvB*float2(10.8,12.8)+4.9);
                float detail=fbm(poolUvA*float2(25.0,29.0)+11.4);
                float paintRaw=saturate(broadA*0.53+broadB*0.32+detail*0.15);
                float poster=floor(paintRaw*8.0)/7.0;
                float depth=saturate(0.07+(1.0-uv.y)*0.18+poster*0.52);
                half3 procedural=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.22,0.62,depth));
                procedural=lerp(procedural,_DeepColor.rgb,smoothstep(0.69,0.96,depth));

                half4 authored=SAMPLE_TEXTURE2D(_AuthoredWaterTex,sampler_AuthoredWaterTex,uv);
                float authoredValid=smoothstep(0.08,0.50,authored.a);
                half3 authoredPaint=authored.rgb;
                float authoredLum=dot(authoredPaint,float3(0.299,0.587,0.114));
                authoredPaint=lerp(authoredLum.xxx,authoredPaint,1.34);
                authoredPaint=float3(authoredPaint.r*0.72, authoredPaint.g*1.12+0.018, authoredPaint.b*1.30+0.045);
                authoredPaint=saturate(authoredPaint);
                float darkPocket=1.0-smoothstep(0.48,0.77,authoredLum);
                float authoredBlend=authoredValid*(0.48+darkPocket*0.20);
                half3 color=lerp(procedural,authoredPaint,authoredBlend);

                float tonalFlow=(broadA-0.5)*0.13+(broadB-0.5)*0.08;
                color=lerp(color,_ShallowColor.rgb,saturate(tonalFlow)*0.18*pool);
                color=lerp(color,_DeepColor.rgb,saturate(-tonalFlow)*0.15*pool);

                float foregroundLift=smoothstep(0.60,0.90,uv.x)*smoothstep(0.60,0.92,1.0-uv.y);
                color=lerp(color,_ShallowColor.rgb,foregroundLift*0.22*pool);

                float cluster=smoothstep(0.50,0.67,fbm(poolUvA*float2(7.5,9.5)+2.1));
                float cluster2=smoothstep(0.54,0.70,fbm(poolUvB*float2(11.0,8.0)+6.7));
                float marksA=brushMark(poolUvA,18.0,50.0,1.4,0.75,0.06,0.18);
                float marksB=brushMark(poolUvB+float2(0.04,0.02),29.0,78.0,7.8,0.82,0.045,0.14);
                float marksC=brushMark(poolUvA+float2(-0.06,0.03),13.0,38.0,16.2,0.79,0.07,0.20);
                float flecks=brushMark(poolUvB+float2(0.09,-0.04),46.0,108.0,29.3,0.89,0.030,0.085);
                float poolWhite=saturate((marksA*0.90+marksB*0.70+marksC*0.58)*cluster + flecks*0.58*cluster2)*pool;
                color=lerp(color,_FoamColor.rgb,poolWhite*0.84);

                float foamBlobA=smoothstep(0.74,0.89,fbm(poolUvA*float2(34.0,24.0)+4.6))*cluster;
                float foamBlobB=smoothstep(0.78,0.91,fbm(poolUvB*float2(48.0,31.0)+11.8))*cluster2;
                color=lerp(color,_FoamColor.rgb,saturate(foamBlobA*0.62+foamBlobB*0.36)*pool*0.44);

                float fallNoise=fbm(float2(fallUv.x*30.0+broadA*2.0,fallUv.y*8.0));
                float ribs=pow(saturate(sin(uv.x*91.0+fallNoise*16.0)*0.5+0.5),5.0);
                float thin=pow(saturate(sin(uv.x*166.0+detail*12.0)*0.5+0.5),8.0);
                float downPulseA=pow(saturate(sin((uv.y+time*0.76)*54.0+fallNoise*9.0)*0.5+0.5),4.0);
                float downPulseB=pow(saturate(sin((uv.y+time*1.08)*92.0+detail*7.0)*0.5+0.5),6.0);
                float fallWhite=saturate((ribs*0.64+thin*0.35)*(0.58+downPulseA*0.42)+downPulseB*0.25);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.76);
                color=lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*smoothstep(0.46,0.74,broadB)*0.16);

                float lip=authoredLipMask(uv)*mask;
                float lipNoise=fbm(float2(uv.x*46.0+time*0.55,uv.y*11.0));
                float lipSegments=smoothstep(0.47,0.64,lipNoise+0.14*sin(uv.x*79.0+time));
                color=lerp(color,_FoamColor.rgb,lip*lipSegments*0.52);

                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*7.0)*fall;
                float bottom=saturate((rawMask-mDn)*6.0)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.1);
                float foamNoise=smoothstep(0.33,0.62,fbm(fallUv*40.0));
                color=lerp(color,_FoamColor.rgb,saturate((topLip*0.98+bottom*0.84)*foamNoise));
                float edgeBreak=smoothstep(0.43,0.66,fbm(poolUvB*float2(52.0,41.0)+4.2));
                float chipped=edgeAny*edgeBreak*pool*_EdgeFoam*0.30;
                color=lerp(color,_FoamColor.rgb,chipped);

                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
