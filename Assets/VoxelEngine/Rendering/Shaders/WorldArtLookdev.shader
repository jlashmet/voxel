Shader "VoxelEngine/WorldArtLookdev"
{
    Properties
    {
        _MainTex ("Surface Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _TextureScale ("World Texture Scale", Float) = 0.45
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _TopLight ("Upward Surface Lift", Range(0,0.4)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Tint;
        float _TextureScale;
        half _Smoothness;
        half _TopLight;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(normalize(IN.worldNormal));
            // Sharper weights keep broad voxel planes graphic instead of turning texture
            // transitions into muddy diagonal bands.
            float3 w = n * n * n * n;
            w /= max(0.0001, w.x + w.y + w.z);

            float s = _TextureScale;
            fixed4 xSample = tex2D(_MainTex, IN.worldPos.zy * s);
            fixed4 ySample = tex2D(_MainTex, IN.worldPos.xz * s);
            fixed4 zSample = tex2D(_MainTex, IN.worldPos.xy * s);
            fixed3 albedo = xSample.rgb * w.x + ySample.rgb * w.y + zSample.rgb * w.z;

            // A tiny upward-facing lift is intentionally painterly. It helps the same material
            // separate horizontal caps from vertical faces without baking unique textures.
            float top = saturate(IN.worldNormal.y);
            albedo *= lerp(1.0 - _TopLight * 0.35, 1.0 + _TopLight, top);

            o.Albedo = albedo * _Tint.rgb;
            o.Metallic = 0;
            o.Smoothness = _Smoothness;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
