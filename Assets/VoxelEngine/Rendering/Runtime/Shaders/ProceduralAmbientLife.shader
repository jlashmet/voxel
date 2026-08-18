Shader "VoxelEngine/ProceduralAmbientLife"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.85, 0.78, 0.35, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.22, 0.16, 0.08, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 12)) = 0
        _Shape ("Shape", Range(0, 9)) = 0
        _FlutterSpeed ("Flutter Speed", Range(0, 16)) = 5
        _Opacity ("Opacity", Range(0, 1)) = 1
        _AnimationTime ("Animation Time", Float) = 0
        _SunDirection ("Sun Direction", Vector) = (-0.48, 0.76, -0.44, 0)
        _SkyHorizon ("Sky Horizon", Color) = (0.66, 0.75, 0.85, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.45, 0.76, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

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
            float4 _SecondaryColor;
            float4 _EmissionColor;
            float _EmissionStrength;
            float _Shape;
            float _FlutterSpeed;
            float _Opacity;
            float _AnimationTime;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float flutter : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Ellipse(float2 p, float2 radius)
            {
                float2 q = p / max(radius, float2(0.001, 0.001));
                return saturate(1.0 - dot(q, q));
            }

            float2 ArticulateLocal(float2 local, float shape, float flutter, float phase)
            {
                float flap = abs(flutter);

                // Wing opening is handled in the fragment silhouette so the body remains stable.
                // Keep only small whole-body compression here.
                if (shape > 0.5 && shape < 1.5)
                {
                    local.y *= 1.02 - flap * 0.035;
                }
                else if (shape > 1.5 && shape < 2.5)
                {
                    local.y *= 1.01 - flap * 0.02;
                }
                else if (shape > 2.5 && shape < 3.5)
                {
                    local.y *= 1.0 - flap * 0.012;
                }
                // Ground insect: deliberately stable silhouette; tiny gait compression only.
                else if (shape > 3.5 && shape < 4.5)
                {
                    float gait = sin(phase * 0.58);
                    local.x *= 1.0 + gait * 0.018;
                    local.y *= 1.0 - gait * 0.012;
                }
                // Frog: subtle body compression, while actual hop translation remains CPU-driven.
                else if (shape > 4.5 && shape < 5.5)
                {
                    float squat = 0.5 + 0.5 * sin(phase * 0.42);
                    local.x *= 1.0 + squat * 0.045;
                    local.y *= 1.0 - squat * 0.055;
                }
                else if (shape > 5.5 && shape < 6.5)
                {
                    local.y *= 1.04 - flap * 0.08;
                }
                // Spores gently breathe rather than flap.
                else if (shape > 6.5 && shape < 7.5)
                {
                    float breathe = 0.96 + 0.06 * sin(phase * 0.31);
                    local *= breathe;
                }
                // Wisps stretch vertically and contract horizontally.
                else if (shape > 7.5 && shape < 8.5)
                {
                    float wisp = sin(phase * 0.39);
                    local.x *= 1.0 - wisp * 0.055;
                    local.y *= 1.0 + wisp * 0.085;
                }
                else if (shape > 8.5)
                {
                    local.y *= 1.02 - flap * 0.04;
                }

                return local;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 centreWS = TransformObjectToWorld(float3(0, 0, 0));
                float sx = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float sy = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float3 cameraRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                float3 cameraUp = normalize(UNITY_MATRIX_I_V[1].xyz);

                float phase = dot(centreWS, float3(0.37, 0.23, 0.41)) + _AnimationTime * _FlutterSpeed;
                float flutter = sin(phase);
                float2 local = ArticulateLocal(input.positionOS.xy, _Shape, flutter, phase);

                centreWS += cameraUp * sin(phase * 0.53) * 0.025
                          + cameraRight * cos(phase * 0.37) * 0.018;
                float3 positionWS = centreWS
                                  + cameraRight * local.x * sx
                                  + cameraUp * local.y * sy;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.flutter = flutter;
                return output;
            }

            float AmbientMask(float2 uv, float shape, float flutter)
            {
                float2 p = uv * 2.0 - 1.0;
                float flap = abs(flutter);

                if (shape < 0.5)
                    return Ellipse(p, float2(0.48, 0.48));

                if (shape < 1.5)
                {
                    float wingOffset = lerp(0.23, 0.40, flap);
                    float wingRadiusX = lerp(0.29, 0.46, flap);
                    float wingRadiusY = lerp(0.48, 0.58, flap);
                    float leftWing = Ellipse(p - float2(-wingOffset, 0.05), float2(wingRadiusX, wingRadiusY));
                    float rightWing = Ellipse(p - float2(wingOffset, 0.05), float2(wingRadiusX, wingRadiusY));
                    float body = Ellipse(p, float2(0.09, 0.64));
                    return max(max(leftWing, rightWing), body);
                }

                if (shape < 2.5)
                {
                    float body = Ellipse(p, float2(0.45, 0.27));
                    float wingOffset = lerp(0.16, 0.31, flap);
                    float wingY = lerp(0.18, 0.29, flap);
                    float wingRadiusX = lerp(0.18, 0.35, flap);
                    float wingRadiusY = lerp(0.15, 0.25, flap);
                    float wings = max(
                        Ellipse(p - float2(-wingOffset, wingY), float2(wingRadiusX, wingRadiusY)),
                        Ellipse(p - float2(wingOffset, wingY), float2(wingRadiusX, wingRadiusY)));
                    return max(body, wings * 0.92);
                }

                if (shape < 3.5)
                {
                    float body = Ellipse(p, float2(0.10, 0.84));
                    float wingOffset = lerp(0.28, 0.45, flap);
                    float wingRadiusX = lerp(0.30, 0.50, flap);
                    float wingRadiusY = lerp(0.10, 0.16, flap);
                    float wings = max(
                        Ellipse(p - float2(-wingOffset, 0.04), float2(wingRadiusX, wingRadiusY)),
                        Ellipse(p - float2(wingOffset, 0.04), float2(wingRadiusX, wingRadiusY)));
                    return max(body, wings);
                }

                if (shape < 4.5)
                {
                    float abdomen = Ellipse(p - float2(0, -0.08), float2(0.48, 0.60));
                    float head = Ellipse(p - float2(0, 0.55), float2(0.28, 0.24));
                    return max(abdomen, head);
                }

                if (shape < 5.5)
                {
                    float body = Ellipse(p - float2(0, -0.09), float2(0.68, 0.46));
                    float eyeA = Ellipse(p - float2(-0.34, 0.37), float2(0.19, 0.21));
                    float eyeB = Ellipse(p - float2(0.34, 0.37), float2(0.19, 0.21));
                    return max(body, max(eyeA, eyeB));
                }

                if (shape < 6.5)
                {
                    float wingOffset = lerp(0.23, 0.41, flap);
                    float wingRadiusX = lerp(0.34, 0.58, flap);
                    float wingRadiusY = lerp(0.17, 0.25, flap);
                    float left = Ellipse(p - float2(-wingOffset, 0.02), float2(wingRadiusX, wingRadiusY));
                    float right = Ellipse(p - float2(wingOffset, 0.02), float2(wingRadiusX, wingRadiusY));
                    float body = Ellipse(p, float2(0.16, 0.42));
                    return max(max(left, right), body);
                }

                if (shape < 7.5)
                    return Ellipse(p, float2(0.30, 0.30));

                if (shape < 8.5)
                {
                    float bulb = Ellipse(p - float2(0, 0.11), float2(0.43, 0.52));
                    float tail = max(0.0, 0.16 - abs(p.x + p.y * 0.12))
                               * saturate(-p.y + 0.18) * 3.2;
                    return max(bulb, tail);
                }

                float core = Ellipse(p, float2(0.22, 0.48));
                float wingOffset = lerp(0.19, 0.33, flap);
                float wingRadiusX = lerp(0.20, 0.36, flap);
                float wingRadiusY = lerp(0.13, 0.21, flap);
                float wings = max(
                    Ellipse(p - float2(-wingOffset, 0.08), float2(wingRadiusX, wingRadiusY)),
                    Ellipse(p - float2(wingOffset, 0.08), float2(wingRadiusX, wingRadiusY)));
                return max(core, wings);
            }

            float AmbientDetail(float2 uv, float shape, float flutter)
            {
                float2 p = uv * 2.0 - 1.0;
                float flap = abs(flutter);

                if (shape < 0.5)
                    return Ellipse(p, float2(0.16, 0.16)) * 0.65;

                if (shape < 1.5)
                {
                    float wingOffset = lerp(0.23, 0.40, flap);
                    float body = Ellipse(p, float2(0.095, 0.62));
                    float leftSpot = Ellipse(p - float2(-wingOffset, 0.06), float2(0.14, 0.20));
                    float rightSpot = Ellipse(p - float2(wingOffset, 0.06), float2(0.14, 0.20));
                    float lowerOffset = lerp(0.17, 0.29, flap);
                    float lowerSpot = max(
                        Ellipse(p - float2(-lowerOffset, -0.31), float2(0.10, 0.12)),
                        Ellipse(p - float2(lowerOffset, -0.31), float2(0.10, 0.12)));
                    return saturate(body + max(leftSpot, rightSpot) * 0.75 + lowerSpot * 0.55);
                }

                if (shape < 2.5)
                {
                    float body = Ellipse(p, float2(0.44, 0.26));
                    float stripeWave = 0.5 + 0.5 * sin(p.x * 20.0 + flutter * 0.8);
                    float stripes = body * smoothstep(0.56, 0.88, stripeWave);
                    float head = Ellipse(p - float2(0.41, 0), float2(0.14, 0.19));
                    return saturate(stripes * 0.85 + head * 0.75);
                }

                if (shape < 3.5)
                {
                    float wingOffset = lerp(0.28, 0.45, flap);
                    float spine = Ellipse(p, float2(0.075, 0.80));
                    float wingBand = max(
                        Ellipse(p - float2(-wingOffset, 0.04), float2(0.25, 0.055)),
                        Ellipse(p - float2(wingOffset, 0.04), float2(0.25, 0.055)));
                    return saturate(spine + wingBand * 0.65);
                }

                if (shape < 4.5)
                {
                    float seam = saturate(1.0 - abs(p.x) / 0.075)
                               * Ellipse(p - float2(0, -0.08), float2(0.48, 0.60));
                    float head = Ellipse(p - float2(0, 0.55), float2(0.19, 0.16));
                    return saturate(seam * 0.75 + head * 0.75);
                }

                if (shape < 5.5)
                {
                    float eyeA = Ellipse(p - float2(-0.34, 0.38), float2(0.11, 0.11));
                    float eyeB = Ellipse(p - float2(0.34, 0.38), float2(0.11, 0.11));
                    float back = Ellipse(p - float2(0, -0.04), float2(0.36, 0.27));
                    return saturate(max(eyeA, eyeB) + back * 0.35);
                }

                if (shape < 6.5)
                {
                    float body = Ellipse(p, float2(0.15, 0.40));
                    float wingFold = saturate(abs(p.y + 0.02) * 6.0 - 0.35)
                                   * saturate(1.0 - abs(p.x) * 0.70);
                    return saturate(body * 0.90 + wingFold * 0.40);
                }

                if (shape < 7.5)
                    return Ellipse(p, float2(0.11, 0.11)) * 0.45;

                if (shape < 8.5)
                {
                    float core = Ellipse(p - float2(0, 0.14), float2(0.19, 0.24));
                    float tailCore = max(0.0, 0.07 - abs(p.x + p.y * 0.12))
                                   * saturate(-p.y + 0.10) * 6.0;
                    return saturate(core * 0.65 + tailCore);
                }

                float core = Ellipse(p, float2(0.15, 0.39));
                float wingOffset = lerp(0.19, 0.33, flap);
                float wingMarks = max(
                    Ellipse(p - float2(-wingOffset, 0.08), float2(0.11, 0.08)),
                    Ellipse(p - float2(wingOffset, 0.08), float2(0.11, 0.08)));
                return saturate(core * 0.80 + wingMarks * 0.65);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = saturate(AmbientMask(input.uv, _Shape, input.flutter));
                clip(mask - 0.075);

                float detail = AmbientDetail(input.uv, _Shape, input.flutter);
                float pattern = 0.5 + 0.5 * sin((input.uv.x + input.uv.y) * 18.0 + _Shape * 2.7);
                float detailMix = saturate(detail * 0.82 + pattern * 0.08);
                float3 albedo = lerp(_BaseColor.rgb, _SecondaryColor.rgb, detailMix);
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, 0.48);
                float3 lit = albedo * (ambient * 0.42 + 0.58);

                // Luminous agents should visibly breathe instead of remaining clipped near white.
                // Squaring the normalized wave gives a long readable dim phase while retaining a
                // brief bright peak. The non-zero floor keeps Fireflies and magical motes present.
                float pulseWave = 0.5 + 0.5 * sin(
                    _AnimationTime * max(0.5, _FlutterSpeed * 0.42)
                    + dot(input.positionWS, float3(0.27, 0.19, 0.31)));
                float pulse = lerp(0.12, 1.0, pulseWave * pulseWave);
                float emissionCore = smoothstep(0.08, 0.58, mask);
                lit += _EmissionColor.rgb * _EmissionStrength
                     * (0.20 + emissionCore * 0.58) * pulse;

                float alpha = smoothstep(0.075, 0.22, mask) * _Opacity;
                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }
}
