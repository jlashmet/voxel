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

                float phase = dot(centreWS, float3(0.37, 0.23, 0.41)) + _Time.y * _FlutterSpeed;
                float flutter = sin(phase);
                float2 local = input.positionOS.xy;
                if (_Shape > 0.5 && _Shape < 4.5)
                    local.x *= 0.82 + abs(flutter) * 0.32;

                centreWS += cameraUp * sin(phase * 0.53) * 0.025 + cameraRight * cos(phase * 0.37) * 0.018;
                float3 positionWS = centreWS + cameraRight * local.x * sx + cameraUp * local.y * sy;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.flutter = flutter;
                return output;
            }

            float AmbientMask(float2 uv, float shape)
            {
                float2 p = uv * 2.0 - 1.0;

                // 0: luminous mote / firefly point.
                if (shape < 0.5)
                    return Ellipse(p, float2(0.50, 0.50));

                // 1: butterfly / moth: paired wings and a narrow body.
                if (shape < 1.5)
                {
                    float leftWing = Ellipse(p - float2(-0.38, 0.05), float2(0.48, 0.60));
                    float rightWing = Ellipse(p - float2(0.38, 0.05), float2(0.48, 0.60));
                    float body = Ellipse(p, float2(0.10, 0.62));
                    return max(max(leftWing, rightWing), body);
                }

                // 2: bee / compact flying insect.
                if (shape < 2.5)
                {
                    float body = Ellipse(p, float2(0.55, 0.28));
                    float wings = max(Ellipse(p - float2(-0.22, 0.28), float2(0.32, 0.27)),
                                      Ellipse(p - float2(0.22, 0.28), float2(0.32, 0.27)));
                    return max(body, wings * 0.88);
                }

                // 3: dragonfly / darting insect.
                if (shape < 3.5)
                {
                    float body = Ellipse(p, float2(0.11, 0.82));
                    float wings = max(Ellipse(p - float2(-0.42, 0.05), float2(0.52, 0.16)),
                                      Ellipse(p - float2(0.42, 0.05), float2(0.52, 0.16)));
                    return max(body, wings);
                }

                // 4: beetle/cricket body.
                if (shape < 4.5)
                    return max(Ellipse(p, float2(0.52, 0.67)), Ellipse(p - float2(0, 0.55), float2(0.30, 0.25)));

                // 5: frog / hopping ground life.
                if (shape < 5.5)
                {
                    float body = Ellipse(p - float2(0, -0.08), float2(0.68, 0.48));
                    float eyeA = Ellipse(p - float2(-0.34, 0.38), float2(0.20, 0.22));
                    float eyeB = Ellipse(p - float2(0.34, 0.38), float2(0.20, 0.22));
                    return max(body, max(eyeA, eyeB));
                }

                // 6: bird / bat wing silhouette.
                if (shape < 6.5)
                {
                    float left = Ellipse(p - float2(-0.38, 0.02), float2(0.62, 0.26));
                    float right = Ellipse(p - float2(0.38, 0.02), float2(0.62, 0.26));
                    float body = Ellipse(p, float2(0.18, 0.43));
                    return max(max(left, right), body);
                }

                // 7: spore mote.
                if (shape < 7.5)
                    return Ellipse(p, float2(0.32, 0.32));

                // 8: wisp / seed-light: tapered magical droplet.
                if (shape < 8.5)
                {
                    float bulb = Ellipse(p - float2(0, 0.10), float2(0.46, 0.55));
                    float tail = max(0.0, 0.18 - abs(p.x + p.y * 0.12)) * saturate(-p.y + 0.15) * 3.0;
                    return max(bulb, tail);
                }

                // 9: emberfly / energetic magic insect.
                float core = Ellipse(p, float2(0.24, 0.48));
                float wings = max(Ellipse(p - float2(-0.30, 0.08), float2(0.38, 0.22)),
                                  Ellipse(p - float2(0.30, 0.08), float2(0.38, 0.22)));
                return max(core, wings);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = saturate(AmbientMask(input.uv, _Shape));
                clip(mask - 0.035);

                float2 p = input.uv * 2.0 - 1.0;
                float centre = saturate(1.0 - length(p));
                float3 ambient = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, 0.48);
                float pattern = 0.5 + 0.5 * sin((input.uv.x + input.uv.y) * 18.0 + _Shape * 2.7);
                float3 albedo = lerp(_BaseColor.rgb, _SecondaryColor.rgb, pattern * 0.22);
                float3 lit = albedo * (ambient * 0.42 + 0.58);
                float pulse = 0.72 + 0.28 * sin(_Time.y * max(0.5, _FlutterSpeed * 0.42) + dot(input.positionWS, float3(0.27, 0.19, 0.31)));
                lit += _EmissionColor.rgb * _EmissionStrength * (0.45 + centre * 0.85) * pulse;
                float alpha = saturate(mask * 2.4) * _Opacity;
                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }
}
