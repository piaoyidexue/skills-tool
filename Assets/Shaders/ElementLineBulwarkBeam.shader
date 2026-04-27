Shader "ElementLine/BulwarkBeam"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.9, 0.78, 0.35, 1)
        _CoreColor ("Core Color", Color) = (1, 0.95, 0.7, 1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 1.2
        _Glow ("Glow", Range(0, 4)) = 1.8
        _WallThickness ("Wall Thickness", Range(0.3, 1.5)) = 0.85
        _CoreWidth ("Core Width", Range(0.05, 0.4)) = 0.15
        _RippleFrequency ("Ripple Frequency", Float) = 3
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
            float4 _CoreColor;
            float _ScrollSpeed;
            float _Glow;
            float _WallThickness;
            float _CoreWidth;
            float _RippleFrequency;

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

                // wide wall body
                float distFromCenter = abs(i.uv.y * 2.0 - 1.0);
                float wallOuter = smoothstep(_WallThickness, _WallThickness * 0.7, distFromCenter);
                float wallInner = smoothstep(_CoreWidth, _CoreWidth * 0.5, distFromCenter);

                // ripple effect along beam length
                float ripple = sin(i.uv.x * 12.56 + _Time.y * _RippleFrequency) * 0.15 + 0.85;

                float noise = tex2D(_MainTex, uv).r;
                float noise2 = tex2D(_MainTex, uv + float2(0, 0.25)).r;

                // outer wall (thick, stable)
                float outerAlpha = wallOuter * (0.55 + noise * 0.3 + ripple * 0.15);
                float4 outerColor = _Tint * outerAlpha;

                // inner core (thin, bright)
                float innerAlpha = wallInner * (0.7 + noise2 * 0.3);
                float4 innerColor = _CoreColor * innerAlpha;

                return (outerColor + innerColor) * _Glow;
            }
            ENDHLSL
        }
    }
}
