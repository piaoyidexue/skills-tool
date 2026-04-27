Shader "ElementLine/ArcBeam"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.55, 0.82, 1, 1)
        _Accent ("Accent", Color) = (1, 0.95, 0.35, 1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 3.5
        _Glow ("Glow", Range(0, 6)) = 2.2
        _SegmentCount ("Segment Count", Range(2, 12)) = 5
        _Jitter ("Jitter", Range(0, 0.3)) = 0.12
        _PulseFrequency ("Pulse Frequency", Float) = 8
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
        }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _Accent;
            float _ScrollSpeed;
            float _Glow;
            float _SegmentCount;
            float _Jitter;
            float _PulseFrequency;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x += _Time.y * _ScrollSpeed;

                // segment-based jitter for chain lightning feel
                float segment = frac(i.uv.x * _SegmentCount);
                float jitter = sin(segment * 6.2832 + _Time.y * _PulseFrequency) * _Jitter;
                float beamMask = saturate(1.0 - abs((i.uv.y + jitter) * 2.0 - 1.0));

                float noise = tex2D(_MainTex, uv).r;
                float segmentEdge = abs(segment - 0.5) * 2.0; // 0 at segment center, 1 at edges
                float edgeGlow = smoothstep(0.85, 0.55, segmentEdge);

                float alpha = beamMask * (0.35 + noise * 0.4 + edgeGlow * 0.25);

                // blend between Tint and Accent based on position
                float4 color = lerp(_Tint, _Accent, saturate(i.uv.x * 0.7 + noise * 0.3));

                return color * alpha * _Glow;
            }
            ENDHLSL
        }
    }
}
