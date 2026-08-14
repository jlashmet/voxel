Shader "Hidden/VoxelEngine/StylizedWaterLookdev"
{
    Properties
    {
        _ReferenceTex ("Water Silhouette", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.04,0.34,0.58,1)
        _MidColor ("Mid Color", Color) = (0.08,0.60,0.80,1)
        _ShallowColor ("Shallow Color", Color) = (0.30,0.82,0.93,1)
        _FoamColor ("Foam Color", Color) = (0.94,0.985,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.20
        _FlowStrength ("Flow Strength", Range(0,0.02)) = 0.006
        _Shimmer ("Shimmer", Range(0,1)) = 0.24
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
                [unroll] for (int k=0;k<5;k++)
                {
                    v += noise(p)*a;
                    p = p*2.03 + float2(13.7,8.9);
                    a *= 0.5;
                }
                return v;
            }

            float boxMask(float2 uv, float2 center, float2 halfSize, float feather)
            {
                float2 d = abs(uv-center)-halfSize;
                float outside = max(d.x,d.y);
                return 1.0-smoothstep(-feather, feather, outside);
            }

            float authoredFallMask(float2 uv)
            {
                float f = 0.0;
                f = max(f, boxMask(uv,float2(0.735,0.925),float2(0.105,0.075),0.020));
                f = max(f, boxMask(uv,float2(0.635,0.765),float2(0.115,0.110),0.025));
                f = max(f, boxMask(uv,float2(0.805,0.655),float2(0.105,0.085),0.022));
                f = max(f, boxMask(uv,float2(0.285,0.575),float2(0.095,0.105),0.025));
                f = max(f, boxMask(uv,float2(0.755,0.545),float2(0.075,0.060),0.020));
                return saturate(f);
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
                half mask = smoothstep(0.10h,0.46h,rawMask);
                clip(mask-0.07h);

                float time = _Time.y*_FlowSpeed;
                float fall = authoredFallMask(uv)*mask;

                float broadA = fbm(uv*float2(6.5,8.0)+float2(time*0.10,-time*0.14));
                float broadB = fbm(uv*float2(11.0,13.0)+float2(-time*0.08,time*0.11)+4.9);
                float detail = fbm(uv*float2(29.0,31.0)+float2(time*0.22,-time*0.28)+11.4);

                // Large painterly turquoise/deep-blue patches like hand-painted water, not tiled caustics.
                float depth = saturate(0.16 + (1.0-uv.y)*0.36 + broadA*0.42 - broadB*0.12);
                half3 color = lerp(_ShallowColor.rgb,_MidColor.rgb,smoothstep(0.24,0.66,depth));
                color = lerp(color,_DeepColor.rgb,smoothstep(0.62,0.94,depth));

                float lightPatch = smoothstep(0.55,0.78,broadB + (detail-0.5)*0.22);
                float darkPatch = smoothstep(0.58,0.82,broadA - (detail-0.5)*0.18);
                color = lerp(color,_ShallowColor.rgb,lightPatch*0.24*(1.0-fall));
                color = lerp(color,_DeepColor.rgb,darkPatch*0.18*(1.0-fall));

                // Pools: broken horizontal foam strokes, two scales, sparse and painterly.
                float waveA = sin(uv.y*112.0 + broadA*13.0 + uv.x*9.0 + time*1.4);
                waveA = pow(saturate(waveA*0.5+0.5),22.0);
                float waveB = sin(uv.y*58.0 - uv.x*11.0 + broadB*10.0 - time*0.9);
                waveB = pow(saturate(waveB*0.5+0.5),26.0);
                float breakA = smoothstep(0.53,0.72,detail);
                float poolFoam = saturate((waveA*0.75 + waveB*0.40)*breakA*(1.0-fall));
                color = lerp(color,_FoamColor.rgb,poolFoam*0.42);

                // Waterfalls: vertical streaking with broad white ribs, blue troughs, and turbulent breakup.
                float fallNoise = fbm(float2(uv.x*31.0 + broadA*2.0, uv.y*8.0 - time*2.6));
                float ribPhase = uv.x*92.0 + fallNoise*15.0 - time*8.0;
                float ribs = pow(saturate(sin(ribPhase)*0.5+0.5),6.0);
                float thinRibs = pow(saturate(sin(uv.x*165.0 + detail*12.0 - time*10.5)*0.5+0.5),10.0);
                float fallWhite = saturate(ribs*0.58 + thinRibs*0.30);
                color = lerp(color,_FoamColor.rgb,fall*fallWhite*0.62);
                color = lerp(color,_DeepColor.rgb,fall*(1.0-fallWhite)*smoothstep(0.52,0.78,broadB)*0.22);

                // Strong white lips at waterfall starts and frothy impact bands at bottoms.
                float2 t = _ReferenceTex_TexelSize.xy*3.0;
                half mUp = SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(0,t.y)).r;
                half mDn = SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(0,t.y)).r;
                half mLf = SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv-float2(t.x,0)).r;
                half mRt = SAMPLE_TEXTURE2D(_ReferenceTex,sampler_ReferenceTex,uv+float2(t.x,0)).r;
                float topLip = saturate((rawMask-mUp)*5.5)*fall;
                float bottomImpact = saturate((rawMask-mDn)*5.0)*fall;
                float edgeAny = saturate((rawMask-min(min(mUp,mDn),min(mLf,mRt)))*4.0);
                float foamNoise = smoothstep(0.38,0.66,fbm(uv*41.0+float2(time*0.4,-time*0.7)));
                float impact = saturate((topLip*0.95 + bottomImpact*0.72)*foamNoise);
                color = lerp(color,_FoamColor.rgb,impact*0.90);

                // Chipped shoreline sparkle, intentionally weaker than waterfall foam.
                float chipped = edgeAny*foamNoise*(1.0-fall)*_EdgeFoam*0.42;
                color = lerp(color,_FoamColor.rgb,chipped);

                // Tiny bright flecks inside pools.
                float fleck = pow(saturate(sin((uv.x*1.5-uv.y)*63.0 + detail*9.0 + time)*0.5+0.5),34.0);
                fleck *= smoothstep(0.62,0.80,broadB)*(1.0-fall)*_Shimmer;
                color = lerp(color,_FoamColor.rgb,fleck*0.55);

                return half4(saturate(color),saturate(mask*_Alpha));
            }
            ENDHLSL
        }
    }
}
