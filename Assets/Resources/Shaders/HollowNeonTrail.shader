Shader "ZeroStep/HollowNeonTrail"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _EdgeWidth ("Outline Width", Range(0.02, 0.45)) = 0.18
        _EdgeSoftness ("Outline Softness", Range(0.005, 0.2)) = 0.04
        _GlowWidth ("Glow Width", Range(0.05, 1.0)) = 0.58
        _GlowAlpha ("Glow Alpha", Range(0, 1)) = 0.42
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 0.75
        _CenterAlpha ("Center Alpha", Range(0, 0.2)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TintColor;
            float _EdgeWidth;
            float _EdgeSoftness;
            float _GlowWidth;
            float _GlowAlpha;
            float _GlowIntensity;
            float _CenterAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _TintColor;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float across = abs(i.uv.y - 0.5) * 2.0;
                float edgeStart = saturate(1.0 - _EdgeWidth);
                float glowStart = saturate(1.0 - _GlowWidth);
                float edge = smoothstep(edgeStart - _EdgeSoftness, edgeStart, across);
                float glow = smoothstep(glowStart - (_EdgeSoftness * 2.0), glowStart, across);
                float hollowCenter = 1.0 - saturate(edge + glow);

                float alpha = max(edge, glow * _GlowAlpha) + hollowCenter * _CenterAlpha;
                alpha *= i.color.a;

                float brightness = edge + glow * _GlowIntensity;
                fixed3 rgb = i.color.rgb * max(brightness, _CenterAlpha);
                return fixed4(rgb, saturate(alpha));
            }
            ENDCG
        }
    }

    Fallback Off
}
