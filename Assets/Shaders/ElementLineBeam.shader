Shader "ElementLine/Beam"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.7,0.95,1,1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 2
        _Glow ("Glow", Range(0,4)) = 1.4
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
            float _ScrollSpeed;
            float _Glow;

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
                float beamMask = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                float noise = tex2D(_MainTex, uv).r;
                float alpha = beamMask * (0.45 + noise * 0.55);
                return _Tint * alpha * _Glow;
            }
            ENDHLSL
        }
    }
}