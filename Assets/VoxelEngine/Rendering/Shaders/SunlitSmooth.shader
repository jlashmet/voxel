Shader "VoxelEngine/SunlitSmooth"
{
    Properties
    {
        _MainTex ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _SecondaryColor ("Secondary Color", Color) = (1,1,1,1)
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Smoothness ("Smoothness", Range(0,1)) = 0.1
        _TextureScale ("World Texture Scale", Float) = 0.35
        _TextureStrength ("Texture Strength", Range(0,1)) = 0.55
        _DetailScale ("Painterly Detail Scale", Float) = 0.18
        _DetailStrength ("Painterly Detail", Range(0,1)) = 0.12
        _TopStrength ("Top Tint Strength", Range(0,1)) = 0.0
        _RimStrength ("Soft Rim", Range(0,0.25)) = 0.055
        _SurfaceKind ("Surface Kind", Float) = 0
        _StoneReliefStrength ("Stone Face Relief", Range(0,1)) = 0
        _StoneJointRelief ("Stone Joint Relief", Range(0,1)) = 0
        _StoneBlockVariation ("Stone Block Variation", Range(0,1)) = 0
        _StoneWeathering ("Stone Edge Weathering", Range(0,1)) = 0
        _ArchSeams ("Arch Masonry Seams", Range(0,1)) = 0
        _ArchJointColor ("Arch Joint Color", Color) = (0.28,0.25,0.20,1)
        _ArchCenterSpring ("Arch Center/Spring/Front", Vector) = (0,0,0,0)
        _ArchRadii ("Arch Inner/Outer/StoneCount/JointWidth", Vector) = (1.6,2.1,15,0.025)
        _ArchPier ("Arch PierOffset/Width/Course/ShaftY", Vector) = (2.2,1.1,0.5,0)
        _ArchVertical ("Arch BaseY/SpringY/BackingFrontZ/Depth", Vector) = (0,3.5,0,1)
        [HideInInspector] _ZWrite ("Z Write", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define ARCH_PI 3.14159265359

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _SecondaryColor;
                float4 _TopColor;
                float4 _EmissionColor;
                float4 _ArchJointColor;
                float4 _ArchCenterSpring;
                float4 _ArchRadii;
                float4 _ArchPier;
                float4 _ArchVertical;
                float _Smoothness;
                float _TextureScale;
                float _TextureStrength;
                float _DetailScale;
                float _DetailStrength;
                float _TopStrength;
                float _RimStrength;
                float _SurfaceKind;
                float _StoneReliefStrength;
                float _StoneJointRelief;
                float _StoneBlockVariation;
                float _StoneWeathering;
                float _ArchSeams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            float PainterNoise(float3 worldPos)
            {
                float3 p0 = floor(worldPos * max(_DetailScale, 0.001));
                float3 p1 = floor(worldPos * max(_DetailScale * 2.17, 0.001));
                return (Hash31(p0) - 0.5) * 0.72 + (Hash31(p1 + 17.0) - 0.5) * 0.28;
            }

            float StoneReliefNoise(float3 worldPos)
            {
                float broad = ValueNoise(worldPos * 1.35 + float3(7.1, 19.3, 3.7));
                float fine = ValueNoise(worldPos * 4.60 + float3(13.4, 2.7, 23.1));
                return ((broad - 0.5) * 0.68 + (fine - 0.5) * 0.32) * 2.0;
            }

            half3 TriplanarTexture(float3 worldPos, half3 normalWS)
            {
                half3 w = pow(abs(normalWS), 4.0h);
                w /= max(w.x + w.y + w.z, 0.0001h);
                float s = max(_TextureScale, 0.001);
                half3 tx = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.zy * s).rgb;
                half3 ty = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.xz * s).rgb;
                half3 tz = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.xy * s).rgb;
                return tx * w.x + ty * w.y + tz * w.z;
            }

            float DistanceToInteger(float value)
            {
                return abs(frac(value + 0.5) - 0.5);
            }

            half ArchMasonry(float3 worldPos, half3 normalWS, out half keystone)
            {
                keystone = 0.0h;
                if (_ArchSeams < 0.001) return 0.0h;

                half frontness = smoothstep(0.35h, 0.82h, -normalWS.z);
                if (frontness <= 0.001h) return 0.0h;

                float2 q = worldPos.xy - _ArchCenterSpring.xy;
                float r = length(q);
                float innerRadius = _ArchRadii.x;
                float outerRadius = _ArchRadii.y;
                float stoneCount = max(5.0, _ArchRadii.z);
                float jointWidth = max(0.008, _ArchRadii.w);

                half ringBand = step(-0.01, q.y)
                    * smoothstep(innerRadius - 0.04, innerRadius + 0.03, r)
                    * (1.0h - smoothstep(outerRadius - 0.03, outerRadius + 0.05, r));
                half ringFront = 1.0h - smoothstep(0.16h, 0.30h,
                    abs(worldPos.z - _ArchCenterSpring.z));

                float angle = atan2(max(q.y, 0.0), q.x);
                float scaledAngle = angle * stoneCount / ARCH_PI;
                float boundaryDistance = DistanceToInteger(scaledAngle)
                    * (ARCH_PI / stoneCount) * max(r, 0.05);
                float radialWeather = lerp(1.0, 0.80 + 0.46 * ValueNoise(
                    worldPos * 8.0 + float3(3.1, 17.2, 5.4)), saturate(_StoneWeathering));
                float radialWidth = jointWidth * radialWeather;
                half radialSeam = (1.0h - smoothstep(radialWidth, radialWidth * 1.85,
                    boundaryDistance)) * ringBand * ringFront;

                float centralDistance = abs(scaledAngle - stoneCount * 0.5);
                keystone = (1.0h - smoothstep(0.34h, 0.48h, centralDistance))
                    * ringBand * ringFront * frontness;

                float cx = _ArchCenterSpring.x;
                float pierOffset = _ArchPier.x;
                float pierWidth = _ArchPier.y;
                float courseHeight = max(0.10, _ArchPier.z);
                float shaftY = _ArchPier.w;
                float springY = _ArchVertical.y;
                float nearestPierCenter = worldPos.x < cx ? cx - pierOffset : cx + pierOffset;
                float pierDx = abs(worldPos.x - nearestPierCenter);
                half pierBand = (1.0h - smoothstep(pierWidth * 0.48, pierWidth * 0.54, pierDx))
                    * step(shaftY, worldPos.y)
                    * (1.0h - step(springY - 0.22, worldPos.y));
                half pierFront = 1.0h - smoothstep(0.17h, 0.31h,
                    abs(worldPos.z - _ArchCenterSpring.z));

                float courseCoord = (worldPos.y - shaftY) / courseHeight;
                float courseDistance = DistanceToInteger(courseCoord) * courseHeight;
                float bedWeather = lerp(1.0, 0.82 + 0.40 * ValueNoise(
                    worldPos * 7.0 + float3(21.4, 6.7, 9.1)), saturate(_StoneWeathering));
                half bedJoint = 1.0h - smoothstep(jointWidth * 0.72 * bedWeather,
                    jointWidth * 1.45 * bedWeather, courseDistance);

                float row = floor(courseCoord);
                float rowParity = fmod(abs(row), 2.0);
                float headShift = rowParity < 1.0 ? -0.10 : 0.10;
                float headDistance = abs(worldPos.x - (nearestPierCenter + headShift));
                float headWeather = lerp(1.0, 0.84 + 0.38 * ValueNoise(
                    worldPos * 8.5 + float3(4.6, 12.8, 25.2)), saturate(_StoneWeathering));
                half headJoint = (1.0h - smoothstep(jointWidth * 0.70 * headWeather,
                    jointWidth * 1.40 * headWeather, headDistance))
                    * smoothstep(jointWidth * 2.0, jointWidth * 3.0, courseDistance);
                half pierSeam = max(bedJoint, headJoint) * pierBand * pierFront;

                float backingFrontZ = _ArchVertical.z;
                half backingFront = 1.0h - smoothstep(0.13h, 0.28h,
                    abs(worldPos.z - backingFrontZ));
                half outsideRing = step(outerRadius + 0.05, r) * step(-0.01, q.y);
                float backingCourseDistance = DistanceToInteger(q.y / courseHeight) * courseHeight;
                half backingSeam = (1.0h - smoothstep(jointWidth * 0.55,
                    jointWidth * 1.20, backingCourseDistance))
                    * backingFront * outsideRing * 0.42h;

                return saturate(max(radialSeam, max(pierSeam * 0.86h, backingSeam))
                    * frontness * _ArchSeams);
            }

            half ArchStoneVariation(float3 worldPos, half3 normalWS)
            {
                if (_ArchSeams < 0.001) return 0.0h;

                half frontness = smoothstep(0.35h, 0.82h, -normalWS.z);
                if (frontness <= 0.001h) return 0.0h;

                float2 q = worldPos.xy - _ArchCenterSpring.xy;
                float r = length(q);
                float innerRadius = _ArchRadii.x;
                float outerRadius = _ArchRadii.y;
                float stoneCount = max(5.0, _ArchRadii.z);
                half ringBand = step(-0.01, q.y)
                    * smoothstep(innerRadius - 0.02, innerRadius + 0.05, r)
                    * (1.0h - smoothstep(outerRadius - 0.05, outerRadius + 0.02, r));
                if (ringBand > 0.01h)
                {
                    float angle = atan2(max(q.y, 0.0), q.x);
                    float stoneIndex = floor(angle * stoneCount / ARCH_PI);
                    return (half)(Hash31(float3(stoneIndex, 11.0, 37.0)) * 2.0 - 1.0)
                        * ringBand * frontness;
                }

                float cx = _ArchCenterSpring.x;
                float pierOffset = _ArchPier.x;
                float pierWidth = _ArchPier.y;
                float courseHeight = max(0.10, _ArchPier.z);
                float shaftY = _ArchPier.w;
                float springY = _ArchVertical.y;
                float nearestPierCenter = worldPos.x < cx ? cx - pierOffset : cx + pierOffset;
                float pierDx = abs(worldPos.x - nearestPierCenter);
                half pierBand = (1.0h - smoothstep(pierWidth * 0.48, pierWidth * 0.54, pierDx))
                    * step(shaftY, worldPos.y)
                    * (1.0h - step(springY - 0.22, worldPos.y));
                if (pierBand > 0.01h)
                {
                    float row = floor((worldPos.y - shaftY) / courseHeight);
                    float side = step(nearestPierCenter, worldPos.x);
                    float pierSide = worldPos.x < cx ? 0.0 : 1.0;
                    return (half)(Hash31(float3(row, side + pierSide * 2.0, 53.0)) * 2.0 - 1.0)
                        * pierBand * frontness;
                }

                return 0.0h;
            }

            half3 PerturbStoneNormal(float3 worldPos, half3 normalWS, float height)
            {
                float3 dpdx = ddx(worldPos);
                float3 dpdy = ddy(worldPos);
                float dhdx = ddx(height);
                float dhdy = ddy(height);
                float3 r1 = cross(dpdy, normalWS);
                float3 r2 = cross(normalWS, dpdx);
                float det = dot(dpdx, r1);
                if (abs(det) < 1e-6) return normalWS;
                float3 gradient = sign(det) * (dhdx * r1 + dhdy * r2);
                return (half3)normalize(abs(det) * normalWS - gradient);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 geometricNormal = normalize(input.normalWS);
                half3 viewDir = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half3 tri = TriplanarTexture(input.positionWS, geometricNormal);
                half lum = dot(tri, half3(0.299h, 0.587h, 0.114h));

                half keystone;
                half masonry = ArchMasonry(input.positionWS, geometricNormal, keystone);
                half blockVariation = ArchStoneVariation(input.positionWS, geometricNormal);
                float reliefNoise = StoneReliefNoise(input.positionWS);
                float reliefHeight = reliefNoise * (0.018 * saturate(_StoneReliefStrength))
                    - masonry * (0.030 * saturate(_StoneJointRelief));
                half3 normalWS = geometricNormal;
                if (_StoneReliefStrength > 0.001 || _StoneJointRelief > 0.001)
                    normalWS = PerturbStoneNormal(input.positionWS, geometricNormal, reliefHeight);

                half textureValue = lerp(1.0h, lerp(0.72h, 1.30h, lum), saturate(_TextureStrength));
                half noise = PainterNoise(input.positionWS) * _DetailStrength;
                half3 baseRgb = _BaseColor.rgb * max(0.35h, textureValue + noise);
                baseRgb *= 1.0h + blockVariation * (half)_StoneBlockVariation * 0.10h;
                half weatherMottle = (half)(ValueNoise(input.positionWS * 2.15 + float3(17.0, 3.0, 9.0)) - 0.5);
                baseRgb *= 1.0h + weatherMottle * (half)_StoneWeathering * 0.055h;

                half top = smoothstep(0.28h, 0.92h, geometricNormal.y) * saturate(_TopStrength);
                baseRgb = lerp(baseRgb, _TopColor.rgb * max(0.80h, textureValue), top);

                half steep = smoothstep(0.72h, 0.18h, geometricNormal.y);
                if (_SurfaceKind > 0.5h && _SurfaceKind < 3.5h)
                    baseRgb = lerp(baseRgb, _SecondaryColor.rgb * max(0.78h, textureValue), steep * 0.34h);

                baseRgb = lerp(baseRgb, _ArchJointColor.rgb * max(0.72h, textureValue), masonry * 0.62h);
                baseRgb = lerp(baseRgb, _TopColor.rgb * max(0.92h, textureValue), keystone * 0.060h);

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half shade = 0.64h + 0.34h * ndl * lerp(0.58h, 1.0h, shadow);

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), 2.5h);
                half rim = fresnel * _RimStrength;
                half3 colour = baseRgb * shade + rim * lerp(baseRgb, half3(1,1,1), 0.35h) + _EmissionColor.rgb;

                if (_SurfaceKind > 3.5h && _SurfaceKind < 4.5h)
                {
                    half ripple = sin(input.positionWS.x * 2.6h + input.positionWS.z * 1.9h) * 0.5h +
                                  sin(input.positionWS.x * 5.1h - input.positionWS.z * 3.4h) * 0.25h;
                    half sparkle = saturate(ripple * 0.20h + 0.16h);
                    colour = baseRgb * (0.92h + sparkle) + _EmissionColor.rgb + fresnel * half3(0.22h,0.42h,0.50h);
                }

                if (_SurfaceKind > 4.5h && _SurfaceKind < 5.5h)
                {
                    half streak = 0.5h + 0.5h * sin(input.positionWS.x * 10.0h + input.positionWS.y * 1.4h);
                    half fine = 0.5h + 0.5h * sin(input.positionWS.x * 23.0h - input.positionWS.y * 2.2h);
                    half flow = saturate(streak * 0.62h + fine * 0.38h);
                    colour = lerp(baseRgb * 0.86h, half3(0.91h,0.985h,1.0h), flow * 0.48h) + _EmissionColor.rgb;
                }

                if (_SurfaceKind > 5.5h && _SurfaceKind < 6.5h)
                    colour = lerp(baseRgb, half3(1.0h,1.0h,0.98h), 0.58h) + _EmissionColor.rgb;

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
