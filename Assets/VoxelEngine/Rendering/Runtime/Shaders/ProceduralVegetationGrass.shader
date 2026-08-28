Shader "VoxelEngine/ProceduralVegetationGrass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.31, 0.62, 0.18, 1)
        _TipColor ("Tip Color", Color) = (0.56, 0.79, 0.27, 1)
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.22
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry+1"
        }
        Cull Off
        ZWrite On
        ZTest LEqual

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
            float _WindStrength;
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
                float2 blade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float height : TEXCOORD1;
                float tintNoise : TEXCOORD2;
                float densityNoise : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float AnimationTime()
            {
                float time = _UseValidationAnimationTime > 0.5 ? _ValidationAnimationTime : _Time.y;
                const float framesPerSecond = 6.0;
                return floor(time * framesPerSecond) / framesPerSecond;
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

            float3 ApplyCharacterDisplacement(float3 positionWS, float3 rootWS, float heightWeight)
            {
                float3 displaced = positionWS;
                int count = min(_GrassInteractorCount, 64);
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= count) break;
                    float3 interactor = _GrassInteractorPositions[i].xyz;
                    float radius = max(_GrassInteractorPositions[i].w, 0.20);
                    float2 away = rootWS.xz - interactor.xz;
                    float distanceToInteractor = length(away);
                    float influence = 1.0 - smoothstep(radius * 0.12, radius, distanceToInteractor);
                    float2 direction = distanceToInteractor > 0.001
                        ? away / distanceToInteractor
                        : float2(1.0, 0.0);
                    float bend = influence * heightWeight;
                    displaced.xz += direction * bend * radius * 0.52;
                    displaced.y -= bend * radius * 0.12;
                }
                return displaced;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 rootWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float heightWeight = saturate(input.uv.y);
                float heightBend = heightWeight * heightWeight;

                // Three decorrelated, low-frequency fields create coherent meadow regions rather
                // than independently randomized blades: presence/density, height, and tint.
                float densityNoise = WorldNoise(rootWS.xz * 0.115 + float2(11.7, -4.3));
                float heightNoise = WorldNoise(rootWS.xz * 0.091 + float2(-27.1, 18.9));
                float tintNoise = WorldNoise(rootWS.xz * 0.083 + float2(43.2, 29.4));
                float presence = smoothstep(0.27, 0.66, densityNoise);
                float regionalHeight = lerp(0.48, 1.24, heightNoise) * lerp(0.34, 1.0, presence);

                float3 positionOS = input.positionOS.xyz;
                positionOS.y *= regionalHeight;
                positionOS.xz *= lerp(0.68, 1.10, presence);
                float3 positionWS = TransformObjectToWorld(positionOS);

                float time = AnimationTime();
                float phase = input.blade.x * 6.2831853;
                float gustField = WorldNoise(rootWS.xz * 0.14 + float2(time * 0.13, -time * 0.09));
                float secondary = WorldNoise(rootWS.xz * 0.21 + float2(-time * 0.07, time * 0.11) + 31.7);
                float gust = (gustField - 0.5) * 1.35 + (secondary - 0.5) * 0.55 + sin(time * 1.7 + phase) * 0.18;
                float2 windDirection = normalize(float2(0.82 + input.blade.y * 0.16, 0.57 - input.blade.y * 0.12));
                positionWS.xz += windDirection * gust * _WindStrength * 0.22 * heightBend;

                positionWS = ApplyCharacterDisplacement(positionWS, rootWS, heightBend);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = heightWeight;
                output.tintNoise = tintNoise;
                output.densityNoise = densityNoise;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float ndl = abs(dot(normalWS, sunDir));
                float toon = 0.58 + smoothstep(0.18, 0.48, ndl) * 0.18 + smoothstep(0.62, 0.84, ndl) * 0.16;

                float tip = smoothstep(0.08, 0.94, input.height);
                float3 bladeColor = lerp(_BaseColor.rgb * 0.67, _TipColor.rgb * 1.06, tip);
                float tint = lerp(0.82, 1.14, input.tintNoise);
                float densityShade = lerp(0.90, 1.06, input.densityNoise);
                float sky = lerp(0.94, 1.08, saturate(normalWS.y * 0.5 + 0.5));
                float3 color = bladeColor * tint * densityShade * toon * sky;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
