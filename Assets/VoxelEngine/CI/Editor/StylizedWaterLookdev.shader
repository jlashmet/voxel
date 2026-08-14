Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _AuthoredWaterTex ("Authored Water Paint", 2D) = "black" {}
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
                float halfLen=minLen+(maxLen-minLen)*r3; float thick=0.042+0.052*r4;
                float slope=(hash21(cell+seed+37.2)-0.5)*0.48;
                float dx=abs(f.x-cx); float dy=abs((f.y-cy)+slope*(f.x-cx));
                float xs=1.0-smoothstep(halfLen,halfLen+0.052,dx); float ys=1.0-smoothstep(thick,thick+0.024,dy);
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

                float broadA=fbm(poolUvA*float2(7.0,8.5));
                float broadB=fbm(poolUvB*float2(11.0,13.0)+4.9);
                float detail=fbm(poolUvA*float2(25.0,29.0)+11.4);
                float depth=saturate(0.07+(1.0-uv.y)*0.16+broadA*0.38+broadB*0.16);
                half3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.22,0.59,depth));
                color=lerp(color,_DeepColor.rgb,smoothstep(0.68,0.92,depth));

                // Anchor the horizontal water to the authored reference paint, then animate subtle flow through it.
                half4 authored0=SAMPLE_TEXTURE2D(_AuthoredWaterTex,sampler_AuthoredWaterTex,uv);
                float2 authoredDrift=float2(time*0.006+sin(uv.y*17.0+time)*0.0018, sin(uv.x*13.0-time*1.4)*0.0012);
                half4 authored1=SAMPLE_TEXTURE2D(_AuthoredWaterTex,sampler_AuthoredWaterTex,uv+authoredDrift);
                float authoredValid=smoothstep(0.035,0.22,max(authored0.a,authored1.a))*pool;
                half3 authoredColor=lerp(authored0.rgb,authored1.rgb,0.24);
                color=lerp(color,authoredColor,authoredValid*0.72);

                float authoredLum=dot(authoredColor,float3(0.299,0.587,0.114));
                float authoredWhite=smoothstep(0.61,0.80,authoredLum)*authoredValid;
                float authoredDark=(1.0-smoothstep(0.30,0.47,authoredLum))*authoredValid;
                float authoredCyan=smoothstep(0.43,0.60,authoredLum)*(1.0-smoothstep(0.73,0.86,authoredLum))*authoredValid;
                color=lerp(color,_ShallowColor.rgb,authoredCyan*0.28);
                color=lerp(color,_DeepColor.rgb,authoredDark*0.42);
                color=lerp(color,_FoamColor.rgb,authoredWhite*0.80);

                // Large moving paint islands give the pools the chunky turquoise / blue breakup of the reference.
                float islandA=fbm(poolUvA*float2(10.0,7.0)+3.2);
                float islandB=fbm(poolUvB*float2(17.0,11.0)+9.4);
                float lightIsland=smoothstep(0.60,0.72,islandA+(detail-0.5)*0.12)*pool;
                float darkIsland=smoothstep(0.62,0.75,islandB-(detail-0.5)*0.10)*pool;
                color=lerp(color,_ShallowColor.rgb,lightIsland*0.25);
                color=lerp(color,_DeepColor.rgb,darkIsland*0.21);

                // Broken foam islands: irregular clusters first, sparse short brush fragments second.
                float foamClusterA=smoothstep(0.59,0.70,fbm(poolUvA*float2(18.0,9.0)+5.1));
                float foamClusterB=smoothstep(0.62,0.73,fbm(poolUvB*float2(28.0,13.0)+13.6));
                float foamGate=smoothstep(0.53,0.68,fbm(float2(poolUvA.x*12.0+time*0.7,poolUvA.y*22.0)));
                float foamIslands=saturate((foamClusterA*0.76+foamClusterB*0.52)*foamGate)*pool;
                color=lerp(color,_FoamColor.rgb,foamIslands*0.46);

                float marksA=brushMark(poolUvA,15.0,43.0,1.4,0.86,0.050,0.15);
                float marksB=brushMark(poolUvB+float2(0.04,0.02),25.0,66.0,7.8,0.91,0.035,0.10);
                float marks= saturate(marksA*0.75+marksB*0.55)*pool;
                color=lerp(color,_FoamColor.rgb,marks*0.60);

                // Animated broken horizontal crest bands with noisy gaps, never full scanlines.
                float warp=(broadA-0.5)*0.030+(detail-0.5)*0.010;
                float wave=sin((poolUvA.y+warp)*70.0+sin(poolUvA.x*21.0+time*0.9)*0.95)*0.5+0.5;
                float bandCore=smoothstep(0.87,0.96,wave);
                float bandBreak=smoothstep(0.60,0.73,fbm(poolUvA*float2(22.0,8.0)+3.8));
                float lateralBreak=smoothstep(0.58,0.70,fbm(float2(poolUvA.x*44.0+time*0.9,poolUvA.y*6.0)));
                float brokenBands=bandCore*bandBreak*lateralBreak*pool;
                color=lerp(color,_FoamColor.rgb,brokenBands*0.38);

                // Waterfall sheets: crisp moving white ribs separated by darker blue troughs.
                float fallNoise=fbm(float2(fallUv.x*34.0+broadA*2.0,fallUv.y*9.0));
                float ribs=pow(saturate(sin(uv.x*92.0+fallNoise*18.0)*0.5+0.5),5.2);
                float thin=pow(saturate(sin(uv.x*176.0+detail*14.0)*0.5+0.5),8.0);
                float downPulseA=pow(saturate(sin((uv.y+time*0.86)*58.0+fallNoise*10.0)*0.5+0.5),4.4);
                float downPulseB=pow(saturate(sin((uv.y+time*1.18)*98.0+detail*8.0)*0.5+0.5),6.4);
                float fallWhite=smoothstep(0.34,0.78,saturate((ribs*0.78+thin*0.38)*(0.48+downPulseA*0.52)+downPulseB*0.28));
                float trough=smoothstep(0.28,0.76,(1.0-fallWhite)+fbm(fallUv*float2(13.0,6.0))*0.22);
                color=lerp(color,_DeepColor.rgb,fall*trough*0.62);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.96);

                // Broken bright lips and turbulent impacts rather than a continuous sticker outline.
                float lip=authoredLipMask(uv)*mask;
                float lipNoise=fbm(float2(uv.x*52.0+time*0.62,uv.y*13.0));
                float lipSegments=smoothstep(0.49,0.62,lipNoise+0.18*sin(uv.x*83.0+time*1.2));
                color=lerp(color,_FoamColor.rgb,lip*lipSegments*0.84);

                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*7.8)*fall;
                float bottom=saturate((rawMask-mDn)*6.6)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.2);
                float impactNoise=smoothstep(0.40,0.62,fbm(float2(fallUv.x*54.0,fallUv.y*23.0)+7.1));
                float impactFoam=saturate(topLip*1.05+bottom*1.18)*impactNoise;
                color=lerp(color,_FoamColor.rgb,impactFoam);

                float edgeBreak=smoothstep(0.58,0.73,fbm(poolUvB*float2(60.0,47.0)+4.2));
                float chipped=edgeAny*edgeBreak*pool*_EdgeFoam*0.18;
                color=lerp(color,_FoamColor.rgb,chipped);
                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
