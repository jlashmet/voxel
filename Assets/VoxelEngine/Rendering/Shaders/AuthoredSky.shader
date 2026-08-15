Shader "Hidden/VoxelEngine/AuthoredSky"
{
    Properties
    {
        _SkyTexture("Sky Panorama", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" }
        Pass
        {
            Name "AuthoredVoxelSky"
            Cull Off
            ZWrite Off
            // This pass runs after URP's normal skybox. Draw only where opaque geometry left the
            // depth buffer at its exact far-clear value, replacing the default sky without ever
            // painting over terrain, castle meshes, or ordinary Unity opaque objects.
            ZTest Equal

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyTexture);
            SAMPLER(sampler_SkyTexture);

            float4x4 _InvViewProj;
            float4 _CameraPosition;
            float4 _SunDirection;
            float4 _SkyHorizon;
            float4 _SkyZenith;
            // x = deck scale, y = coverage threshold, z = drift speed, w = opacity
            float4 _CloudParams;
            float4 _CloudColour;
            float4 _CloudShadow;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                Varyings output;
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_REVERSED_Z
                output.positionCS.z = 0.0;
#else
                output.positionCS.z = output.positionCS.w;
#endif
                output.uv = uv;
                return output;
            }

            float3 GradientSky(float3 direction)
            {
                // Horizon wash must be confined to a narrow band just above the skyline.
                // An exponent of 2.2 left the horizon colour still contributing 66% at ten
                // degrees up and 40% at twenty, so everything except the zenith read grey —
                // worse than the linear ramp it replaced. A high exponent collapses the wash
                // into the few degrees where it actually belongs.
                float t = saturate(direction.y);
                float blend = pow(1.0 - t, 9.0);
                return lerp(_SkyZenith.rgb, _SkyHorizon.rgb, blend);
            }

            // -- clouds ---------------------------------------------------------------
            // Value-noise fBm on a plane at cloud altitude. Cheap, and the sky pass is a
            // single full-screen quad that only shades background pixels, so this costs
            // nothing where terrain covers the view.
            float CloudHash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float CloudNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = CloudHash(i);
                float b = CloudHash(i + float2(1.0, 0.0));
                float c = CloudHash(i + float2(0.0, 1.0));
                float d = CloudHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float CloudFbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    sum += CloudNoise(p) * amplitude;
                    p = p * 2.02 + 17.3;
                    amplitude *= 0.5;
                }
                return sum;
            }

            float3 Clouds(float3 direction, float3 sky)
            {
                // Below the horizon there is no cloud plane to intersect.
                if (direction.y <= 0.02) return sky;

                float drift = _Time.y * _CloudParams.z;
                // Project the view ray onto the cloud deck. The 1/y term is what gives the
                // deck its perspective: cells stretch toward the horizon instead of tiling
                // uniformly across the dome.
                float2 uv = direction.xz / direction.y * _CloudParams.x + float2(drift, drift * 0.6);

                // Domain warp, so the shapes billow instead of reading as noise.
                float2 warp = float2(CloudFbm(uv * 0.5 + 11.7), CloudFbm(uv * 0.5 + 41.3));
                float density = CloudFbm(uv + warp * 1.8);

                float coverage = _CloudParams.y;
                float mask = smoothstep(coverage, coverage + 0.28, density);
                // Fade the deck out at the horizon so it never forms a hard band.
                mask *= smoothstep(0.02, 0.30, direction.y);

                float sunAmount = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float3 lit = lerp(_CloudColour.rgb, float3(1.0, 0.96, 0.90),
                                  pow(sunAmount, 6.0) * 0.55);
                // Thicker cloud shades toward its own underside rather than going grey.
                float3 shaded = lerp(_CloudShadow.rgb, lit, saturate(density * 1.4));
                return lerp(sky, shaded, mask * _CloudParams.w);
            }

            float3 AuthoredSky(float3 direction)
            {
                float2 skyUv = float2(atan2(direction.x, direction.z) * 0.159154943 + 0.5,
                                      asin(clamp(direction.y, -1.0, 1.0)) * 0.318309886 + 0.5);
                float3 painted = SAMPLE_TEXTURE2D_LOD(_SkyTexture, sampler_SkyTexture, skyUv, 0).rgb;
                float luminance = dot(painted, float3(0.2126, 0.7152, 0.0722));
                painted = lerp(luminance.xxx, painted, 0.46);
                // The gradient now leads. The panorama had been supplying most of the colour
                // and it is a desaturated plate, which is what kept the sky looking washed out.
                float3 sky = lerp(painted, GradientSky(direction), 0.82);

                float sunDot = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float broadHalo = pow(sunDot, 18.0);
                float innerHalo = pow(sunDot, 96.0);
                float disc = pow(sunDot, 900.0);
                sky += float3(1.0, 0.55, 0.23) * broadHalo * 0.12;
                sky += float3(1.0, 0.72, 0.42) * innerHalo * 0.24;
                sky += float3(1.0, 0.92, 0.72) * disc * 1.25;
                // Clouds last: they occlude the sun halo rather than being washed out by it.
                sky = Clouds(direction, sky);
                return sky;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // Match the old raymarch direction reconstruction. Clip z=0.5 is only used to
                // obtain a point along the camera ray; it is independent of this pass's far depth.
                float2 ndc = input.uv * 2.0 - 1.0;
                float4 h = mul(_InvViewProj, float4(ndc, 0.5, 1.0));
                float3 target = h.xyz / h.w;
                float3 direction = normalize(target - _CameraPosition.xyz);
                return float4(AuthoredSky(direction), 1.0);
            }
            ENDHLSL
        }
    }
}
