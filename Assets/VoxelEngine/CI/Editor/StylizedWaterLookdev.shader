Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.018,0.20,0.48,1)
        _MidColor ("Mid Color", Color) = (0.025,0.58,0.84,1)
        _ShallowColor ("Shallow Color", Color) = (0.32,0.88,0.99,1)
        _FoamColor ("Foam Color", Color) = (0.995,1,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.34
        _EdgeFoam ("Edge Foam", Range(0,1)) = 0.36
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
                float halfLen=minLen+(maxLen-minLen)*r3; float thick=0.032+0.044*r4;
                float slope=(hash21(cell+seed+37.2)-0.5)*0.42;
                float dx=abs(f.x-cx); float dy=abs((f.y-cy)+slope*(f.x-cx));
                float xs=1.0-smoothstep(halfLen,halfLen+0.046,dx); float ys=1.0-smoothstep(thick,thick+0.020,dy);
                return xs*ys*step(density,r0);
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                half rawMask=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv).r;
                half mask=smoothstep(0.10h,0.46h,rawMask); clip(mask-0.07h);
                float sourceTime=lerp(_Time.y,_PreviewTime,step(0.5,_UsePreviewTime));
                float time=sourceTime*_FlowSpeed;
                float fall=authoredFallMask(uv)*mask; float pool=1.0-fall;
                float2 poolUvA=uv+float2(time*0.090+sin(uv.y*11.0+time*1.7)*_FlowStrength,time*0.012);
                float2 poolUvB=uv+float2(-time*0.050+sin(uv.y*7.0-time*1.2)*_FlowStrength*0.7,-time*0.009);
                float2 fallUv=uv+float2(sin(uv.y*24.0+time*5.0)*_FlowStrength*0.8,time*0.64);
                float broadA=fbm(poolUvA*float2(7.0,8.5)); float broadB=fbm(poolUvB*float2(11.0,13.0)+4.9); float detail=fbm(poolUvA*float2(25.0,29.0)+11.4);
                float depth=saturate(0.07+(1.0-uv.y)*0.16+broadA*0.38+broadB*0.16);
                half3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.22,0.59,depth));
                color=lerp(color,_DeepColor.rgb,smoothstep(0.68,0.92,depth));
                color=lerp(color,_ShallowColor.rgb,pool*0.08);
                float foregroundLift=smoothstep(0.54,0.86,uv.x)*smoothstep(0.56,0.92,1.0-uv.y)*pool;
                color=lerp(color,_ShallowColor.rgb,foregroundLift*0.44);
                half4 authored=SAMPLE_TEXTURE2D(_AuthoredWaterTex,sampler_AuthoredWaterTex,uv);
                float authoredValid=smoothstep(0.04,0.28,authored.a)*pool;
                float authoredLum=dot(authored.rgb,float3(0.299,0.587,0.114));
                float authoredWhite=smoothstep(0.50,0.71,authoredLum)*authoredValid;
                float darkSuppress=1.0-foregroundLift*0.82;
                float authoredDark=(1.0-smoothstep(0.34,0.53,authoredLum))*authoredValid*darkSuppress;
                float authoredLightMid=smoothstep(0.44,0.60,authoredLum)*(1.0-smoothstep(0.68,0.82,authoredLum))*authoredValid;
                float authoredDarkMid=smoothstep(0.28,0.42,authoredLum)*(1.0-smoothstep(0.50,0.62,authoredLum))*authoredValid*darkSuppress;
                color=lerp(color,_ShallowColor.rgb,authoredLightMid*0.50);
                color=lerp(color,_DeepColor.rgb,authoredDarkMid*0.40);
                color=lerp(color,_FoamColor.rgb,authoredWhite*0.94);
                color=lerp(color,_DeepColor.rgb,authoredDark*0.58);
                float tonalFlow=(broadA-0.5)*0.15+(broadB-0.5)*0.08;
                color=lerp(color,_ShallowColor.rgb,saturate(tonalFlow)*0.10*pool);
                color=lerp(color,_DeepColor.rgb,saturate(-tonalFlow)*0.08*pool*darkSuppress);
                float cluster=smoothstep(0.45,0.64,fbm(poolUvA*float2(8.0,10.0)+2.1));
                float cluster2=smoothstep(0.50,0.67,fbm(poolUvB*float2(12.0,9.0)+6.7));
                float marksA=brushMark(poolUvA,20.0,54.0,1.4,0.72,0.050,0.15);
                float marksB=brushMark(poolUvB+float2(0.04,0.02),31.0,82.0,7.8,0.80,0.036,0.11);
                float flecks=brushMark(poolUvB+float2(0.09,-0.04),50.0,112.0,29.3,0.88,0.022,0.066);
                float poolWhite=saturate((marksA*0.84+marksB*0.69+flecks*0.58)*cluster)*pool;
                color=lerp(color,_FoamColor.rgb,poolWhite*0.88);
                float warp=(broadA-0.5)*0.024+(detail-0.5)*0.007;
                float wave=sin((poolUvA.y+warp)*76.0+sin(poolUvA.x*18.0+time*0.7)*0.65)*0.5+0.5;
                float bandCore=smoothstep(0.82,0.94,wave);
                float bandBreak=smoothstep(0.60,0.73,fbm(poolUvA*float2(19.0,8.0)+3.8));
                float bandBreak2=smoothstep(0.64,0.76,fbm(poolUvB*float2(29.0,11.0)+14.2));
                float lateralBreak=smoothstep(0.54,0.69,fbm(float2(poolUvA.x*37.0+time*0.8,poolUvA.y*5.0)));
                float brokenBands=bandCore*max(bandBreak,bandBreak2*0.65)*lateralBreak*pool;
                color=lerp(color,_FoamColor.rgb,brokenBands*0.43);
                float sparkleA=smoothstep(0.70,0.86,fbm(poolUvA*float2(39.0,31.0)+float2(time*0.45,4.3)))*cluster;
                float sparkleB=smoothstep(0.75,0.89,fbm(poolUvB*float2(53.0,37.0)+12.1))*cluster2;
                float sparkles=saturate(sparkleA*0.72+sparkleB*0.48)*pool;
                color=lerp(color,_FoamColor.rgb,sparkles*0.50);
                float fallNoise=fbm(float2(fallUv.x*31.0+broadA*2.0,fallUv.y*8.0));
                float ribs=pow(saturate(sin(uv.x*88.0+fallNoise*17.0)*0.5+0.5),4.5);
                float thin=pow(saturate(sin(uv.x*168.0+detail*13.0)*0.5+0.5),7.5);
                float downPulseA=pow(saturate(sin((uv.y+time*0.82)*55.0+fallNoise*9.0)*0.5+0.5),3.8);
                float downPulseB=pow(saturate(sin((uv.y+time*1.12)*93.0+detail*7.0)*0.5+0.5),5.8);
                float fallWhite=saturate((ribs*0.76+thin*0.42)*(0.55+downPulseA*0.45)+downPulseB*0.28);
                color=lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*0.50);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.92);
                float lip=authoredLipMask(uv)*mask;
                float lipNoise=fbm(float2(uv.x*48.0+time*0.58,uv.y*11.0));
                float lipSegments=smoothstep(0.45,0.62,lipNoise+0.14*sin(uv.x*79.0+time));
                color=lerp(color,_FoamColor.rgb,lip*lipSegments*0.64);
                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*7.4)*fall; float bottom=saturate((rawMask-mDn)*6.2)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.0);
                float foamNoise=smoothstep(0.34,0.61,fbm(fallUv*42.0));
                color=lerp(color,_FoamColor.rgb,saturate((topLip*1.05+bottom*0.88)*foamNoise));
                float edgeBreak=smoothstep(0.52,0.72,fbm(poolUvB*float2(56.0,43.0)+4.2));
                float chipped=edgeAny*edgeBreak*pool*_EdgeFoam*0.20;
                color=lerp(color,_FoamColor.rgb,chipped);
                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
