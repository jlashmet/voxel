Shader "VoxelEngine/ProceduralVegetationFoliage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.20, 0.45, 0.16, 1)
        _TipColor ("Tip Color", Color) = (0.42, 0.70, 0.24, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 0
        _Shape ("Shape", Range(0, 4)) = 0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.22
        _Cutoff ("Cutoff", Range(0, 1)) = 0.42
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite On
        ZTest LEqual
        AlphaToMask On

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseColor;
            float4 _TipColor;
            float4 _EmissionColor;
            float _EmissionStrength;
            float _Shape;
            float _WindStrength;
            float _Cutoff;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            float _ValidationAnimationTime;
            float _UseValidationAnimationTime;
            float4 _GrassInteractorPositions[64];
            int _GrassInteractorCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float instanceVariation : TEXCOORD3;
                float windNoise : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float AnimationTime()
            {
                return _UseValidationAnimationTime > 0.5 ? _ValidationAnimationTime : _Time.y;
            }

            float QuantizedAnimationTime()
            {
                const float framesPerSecond = 8.0;
                return floor(AnimationTime() * framesPerSecond) / framesPerSecond;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float WorldNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float SecondaryWindNoise(float2 worldXZ, float time)
            {
                float broad = WorldNoise(worldXZ * 0.075 + float2(time * 0.18, -time * 0.11));
                float offset = WorldNoise(worldXZ * 0.13 + float2(19.7, -31.3) + float2(-time * 0.08, time * 0.14));
                return broad * 0.62 + offset * 0.38;
            }

            float InstanceVariation(float3 instanceOriginWS)
            {
                return Hash21(instanceOriginWS.xz * 0.173 + instanceOriginWS.y * float2(0.071, 0.113));
            }

            float2 ViewSway(float3 positionWS, float instanceVariation, float time)
            {
                float3 toCamera = normalize(GetCameraPositionWS() - positionWS);
                float2 viewDirection = normalize(toCamera.xz + float2(0.0001, 0.0001));
                float pulse = sin(time * 2.15 + instanceVariation * 6.2831853) * 0.5 + 0.5;
                return viewDirection * ((pulse - 0.5) * 0.035);
            }

            float3 ApplyCharacterDisplacement(float3 positionWS, float bend)
            {
                float3 displaced = positionWS;
                int count = min(_GrassInteractorCount, 64);
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= count) break;
                    float3 interactor = _GrassInteractorPositions[i].xyz;
                    float radius = max(_GrassInteractorPositions[i].w, 0.35);
                    float2 away = displaced.xz - interactor.xz;
                    float distanceToInteractor = length(away);
                    float influence = 1.0 - smoothstep(radius * 0.25, radius, distanceToInteractor);
                    float2 direction = distanceToInteractor > 0.001 ? away / distanceToInteractor : float2(1.0, 0.0);
                    displaced.xz += direction * influence * radius * 0.28 * bend;
                    displaced.y -= influence * radius * 0.08 * bend;
                }
                return displaced;
            }

            float HybridToonLight(float ndl)
            {
                float lowBand = smoothstep(0.25, 0.42, ndl);
                float highBand = smoothstep(0.62, 0.80, ndl);
                return 0.42 + lowBand * 0.24 + highBand * 0.28;
            }

            float2 BentMaskUv(float2 uv, float windNoise, float instanceVariation)
            {
                float height = saturate(uv.y);
                float perspectiveScale = lerp(1.08, 0.88, height);
                float windLean = (windNoise - 0.5) * 0.11 * height * height;
                float instanceLean = (instanceVariation - 0.5) * 0.06 * height;
                float2 p = uv - float2(0.5, 0.0);
                p.x = p.x * perspectiveScale + windLean + instanceLean;
                return p + float2(0.5, 0.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 instanceOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float variation = InstanceVariation(instanceOriginWS);
                float time = QuantizedAnimationTime() + variation * 0.43;

                float3 localPosition = input.positionOS.xyz;
                float widthScale = lerp(0.84, 1.14, variation);
                float heightScale = lerp(0.88, 1.12, frac(variation * 7.13));
                localPosition.x *= widthScale;
                localPosition.y *= heightScale;

                float3 positionWS = TransformObjectToWorld(localPosition);
                float bend = saturate(input.uv.y);
                float windNoise = SecondaryWindNoise(positionWS.xz, time);
                float phase = variation * 6.2831853 + time * 1.55;
                float gust = (windNoise - 0.5) * 1.55 + sin(phase) * 0.18;
                float secondary = WorldNoise(positionWS.xz * 0.19 + float2(-time * 0.09, time * 0.06)) - 0.5;
                float bendAmount = _WindStrength * bend * bend;
                positionWS.x += (gust * 0.15 + secondary * 0.08) * bendAmount;
                positionWS.z += (secondary * 0.13 - gust * 0.06) * bendAmount;

                float2 viewSway = ViewSway(positionWS, variation, time);
                positionWS.xz += viewSway * bend * _WindStrength;
                positionWS = ApplyCharacterDisplacement(positionWS, bend);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                output.instanceVariation = variation;
                output.windNoise = windNoise;
                return output;
            }

            float Ellipse(float2 p, float2 radius)
            {
                float2 q = p / max(radius, float2(0.001, 0.001));
                return 1.0 - dot(q, q);
            }

            float PlantMask(float2 uv, float shape)
            {
                float2 p = uv * 2.0 - 1.0;

                if (shape < 0.5)
                {
                    float width = lerp(0.58, 0.055, saturate(uv.y));
                    float side = 1.0 - abs(p.x) / max(width, 0.02);
                    float tip = 1.05 - abs(p.y - 0.02);
                    return min(side, tip);
                }

                if (shape < 1.5)
                {
                    float taper = lerp(0.70, 0.24, saturate(uv.y));
                    return 1.0 - (p.x * p.x / max(taper * taper, 0.02) + p.y * p.y * 0.92);
                }

                if (shape < 2.5)
                {
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    float petals = 0.57 + 0.24 * cos(a * 6.0);
                    float stem = min(0.11 - abs(p.x), 0.35 - abs(p.y + 0.66));
                    return max(petals - r, stem);
                }

                if (shape < 3.5)
                {
                    float cap = Ellipse(p - float2(0.0, 0.36), float2(0.86, 0.42));
                    float stem = min(0.22 - abs(p.x), 0.68 - abs(p.y + 0.36));
                    return max(cap, stem);
                }

                float a = atan2(p.y, p.x);
                float r = length(p);
                float outline = 0.76 + 0.08 * sin(a * 5.0) + 0.06 * sin(a * 9.0 + 1.2);
                return outline - r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 maskUv = BentMaskUv(input.uv, input.windNoise, input.instanceVariation);
                float mask = PlantMask(maskUv, _Shape);
                clip(mask - lerp(-0.04, 0.16, _Cutoff));

                float3 n = normalize(input.normalWS);
                float3 sun = normalize(_SunDirection.xyz);
                float ndl = abs(dot(n, sun));
                float skyT = saturate(abs(n.y) * 0.70 + 0.18);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, skyT);

                float height = saturate(input.uv.y);
                float colourPatch = WorldNoise(input.positionWS.xz * 0.095 + float2(7.3, -11.9));
                float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, height * 0.72);
                albedo *= lerp(0.88, 1.13, colourPatch);
                albedo *= lerp(0.91, 1.09, input.instanceVariation);
                albedo *= lerp(float3(1,1,1), input.color.rgb, 0.30);

                // Flowers need to read as blossoms rather than tiny foliage-colored stars. Preserve
                // the material's authored petal hue, lift the outer petals toward a soft cream, and
                // add a compact warm centre. This only affects the six-petal flower shape.
                if (_Shape > 1.5 && _Shape < 2.5)
                {
                    float2 flowerP = input.uv * 2.0 - 1.0;
                    float flowerR = length(flowerP);
                    float flowerHead = 1.0 - smoothstep(0.58, 0.82, flowerR);
                    float palePetal = smoothstep(0.18, 0.58, flowerR) * flowerHead;
                    float warmCenter = 1.0 - smoothstep(0.09, 0.23, flowerR);
                    albedo = lerp(albedo * 1.12, float3(1.0, 0.94, 0.86), palePetal * 0.30);
                    albedo = lerp(albedo, float3(1.0, 0.76, 0.20), warmCenter * 0.92);
                }

                float toon = HybridToonLight(ndl);
                float3 lit = albedo * (ambient * 0.38 + toon);
                float pulse = 0.90 + 0.10 * sin(QuantizedAnimationTime() * 1.6 + input.instanceVariation * 6.2831853);
                lit += _EmissionColor.rgb * _EmissionStrength * pulse;
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
