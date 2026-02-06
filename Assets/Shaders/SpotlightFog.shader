Shader "Custom/SpotlightFog"
{
    // Fog of War: 검은색 레이어 + 스포트라이트 위치만 투명. Softness로 경계 은은하게.
    Properties
    {
        _FogColor ("Fog Color", Color) = (0, 0, 0, 1)
        _Radius ("Spotlight Radius", Float) = 2.5
        _Softness ("Edge Softness", Range(0.01, 2)) = 0.4
        _Center ("Spotlight Center (world xy, z=use 0=don't)", Vector) = (0, 0, 0, 0)
        _RevealedCount ("Revealed Positions Count", Int) = 0
        _PulseCenter ("Pulse Center (world xy, z=active)", Vector) = (0, 0, 0, 0)
        _PulseRadius ("Pulse Wave Radius", Float) = -1
        _PulseWidth ("Pulse Ring Width (visible ~0.2s)", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_REVEALED 64

            float4 _FogColor;
            float _Radius;
            float _Softness;
            float4 _Center;
            int _RevealedCount;
            float4 _RevealedPositions[MAX_REVEALED];
            float4 _PulseCenter;
            float _PulseRadius;
            float _PulseWidth;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldVertex = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldVertex);
                o.worldPos = worldVertex.xy;
                return o;
            }

            float circleVisibility(float2 worldPos, float2 center, float radius, float softness)
            {
                float d = distance(worldPos, center);
                float inner = radius - softness;
                float outer = radius + softness;
                return 1.0 - smoothstep(inner, outer, d);
            }

            // 게임오버 Radar Pulse: 파동이 닿는 링만 0.2초 정도 보였다가 다시 어두워짐
            float pulseRingVisibility(float2 worldPos, float2 center, float pulseRadius, float ringWidth)
            {
                float d = distance(worldPos, center);
                float inner = pulseRadius - ringWidth;
                float outer = pulseRadius + ringWidth;
                return smoothstep(inner - 0.1, inner, d) * (1.0 - smoothstep(outer, outer + 0.1, d));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 worldPos = i.worldPos;
                float visibility = 0.0;

                if (_Center.z > 0.5)
                    visibility = max(visibility, circleVisibility(worldPos, _Center.xy, _Radius, _Softness));

                for (int k = 0; k < _RevealedCount && k < MAX_REVEALED; k++)
                    visibility = max(visibility, circleVisibility(worldPos, _RevealedPositions[k].xy, _Radius, _Softness));

                if (_PulseCenter.z > 0.5 && _PulseRadius >= 0.0)
                    visibility = max(visibility, pulseRingVisibility(worldPos, _PulseCenter.xy, _PulseRadius, _PulseWidth));

                float alpha = 1.0 - visibility;
                return fixed4(_FogColor.rgb, _FogColor.a * alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
