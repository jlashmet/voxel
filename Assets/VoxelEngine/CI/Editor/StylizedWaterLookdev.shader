Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.028,0.27,0.52,1)
        _MidColor ("Mid Color", Color) = (0.070,0.57,0.79,1)
        _ShallowColor ("Shallow Color", Color) = (0.34,0.84,0.95,1)
        _FoamColor ("Foam Color", Color) = (0.98,0.995,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.34
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
            float authoredFallMask(float2 uv){float f=0.0;f=max(f,boxMask(uv,float2(0.765,0.935),float2(0.075,0.060),0.014));f=max(f,boxMask(uv,float2(0.690,0.790),float2(0.075,0.075),0.016));f=max(f,boxMask(uv,float2(0.295,0.615),float2(0.070,0.075),0.016));f=max(f,boxMask(uv,float2(0.815,0.555),float2(0.030,0.040),0.012));return saturate(f);}
            Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=TRANSFORM_TEX(input.uv,_ReferenceTex);return o;}

            float horizontalStroke(float2 p,float scaleX,float scaleY,float seed)
            {
                float2 g=p*float2(scaleX,scaleY);
                float2 cell=floor(g);
                float2 f=frac(g);
                float rnd=hash21(cell+seed);
                float yCenter=0.24+0.52*hash21(cell+seed+7.3);
                float halfLen=0.22+0.25*hash21(cell+seed+13.1);
                float thickness=0.055+0.055*hash21(cell+seed+21.7);
                float xShape=1.0-smoothstep(halfLen,halfLen+0.10,abs(f.x-0.5));
                float yShape=1.0-smoothstep(thickness,thickness+0.035,abs(f.y-yCenter));
                return xShape*yShape*step(0.48,rnd);
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

                float2 poolUvA=uv+float2(time*0.095+sin(uv.y*11.0+time*1.7)*_FlowStrength,time*0.014);
                float2 poolUvB=uv+float2(-time*0.056+sin(uv.y*7.0-time*1.2)*_FlowStrength*0.7,-time*0.010);
                float2 fallUv=uv+float2(sin(uv.y*24.0+time*5.0)*_FlowStrength*0.8,time*0.64);

                float broadA=fbm(poolUvA*float2(6.0,7.4));
                float broadB=fbm(poolUvB*float2(10.0,12.0)+4.9);
                float detail=fbm(poolUvA*float2(27.0,29.0)+11.4);

                float depth=saturate(0.14+(1.0-uv.y)*0.41+broadA*0.43-broadB*0.13);
                half3 color=lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.20,0.62,depth));
                color=lerp(color,_DeepColor.rgb,smoothstep(0.59,0.92,depth));

                float palePatch=smoothstep(0.54,0.77,broadB+(detail-0.5)*0.24);
                float deepPatch=smoothstep(0.58,0.81,broadA-(detail-0.5)*0.20);
                color=lerp(color,_ShallowColor.rgb,palePatch*0.24*pool);
                color=lerp(color,_DeepColor.rgb,deepPatch*0.20*pool);

                // Animated anisotropic brush strokes: short, mostly-horizontal painted reflections.
                float strokesA=horizontalStroke(poolUvA,18.0,72.0,1.2);
                float strokesB=horizontalStroke(poolUvB+float2(0.03,0.01),27.0,108.0,9.7);
                float strokesC=horizontalStroke(poolUvA+float2(0.08,-0.02),12.0,48.0,17.1);
                float textureBreak=smoothstep(0.40,0.67,detail);
                float poolWhite=saturate((strokesA*0.78+strokesB*0.58+strokesC*0.42)*textureBreak*pool);
                color=lerp(color,_FoamColor.rgb,poolWhite*0.82);

                // Secondary cyan strokes keep the surface varied without producing cloud blobs.
                float cyanStroke=horizontalStroke(poolUvB+float2(0.11,0.04),15.0,57.0,25.2)*pool;
                color=lerp(color,_ShallowColor.rgb,cyanStroke*0.36);

                // Waterfall ribbons stay vertical while brightness packets move down the fall.
                float fallNoise=fbm(float2(fallUv.x*30.0+broadA*2.0,fallUv.y*8.0));
                float ribs=pow(saturate(sin(uv.x*91.0+fallNoise*16.0)*0.5+0.5),5.2);
                float thin=pow(saturate(sin(uv.x*166.0+detail*12.0)*0.5+0.5),8.5);
                float downPulseA=pow(saturate(sin((uv.y+time*0.76)*54.0+fallNoise*9.0)*0.5+0.5),4.0);
                float downPulseB=pow(saturate(sin((uv.y+time*1.08)*92.0+detail*7.0)*0.5+0.5),6.5);
                float fallWhite=saturate((ribs*0.65+thin*0.36)*(0.58+downPulseA*0.36)+downPulseB*0.24);
                color=lerp(color,_FoamColor.rgb,fall*fallWhite*0.78);
                color=lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*smoothstep(0.48,0.74,broadB)*0.26);

                float2 t=_ReferenceTex_TexelSize.xy*3.0;
                half mUp=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt=SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip=saturate((rawMask-mUp)*6.0)*fall;
                float bottom=saturate((rawMask-mDn)*5.4)*fall;
                float edgeAny=saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.1);
                float foamNoise=smoothstep(0.34,0.62,fbm(fallUv*43.0));
                color=lerp(color,_FoamColor.rgb,saturate((topLip+bottom*0.80)*foamNoise)*0.98);

                float chipped=edgeAny*smoothstep(0.36,0.64,fbm(poolUvB*43.0))*pool*_EdgeFoam*0.34;
                color=lerp(color,_FoamColor.rgb,chipped);

                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
