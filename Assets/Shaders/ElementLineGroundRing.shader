Shader "ElementLine/GroundRing"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,0.7,0.3,1)
        _RingThickness ("Ring Thickness", Range(0.01,0.4)) = 0.12
        _Progress ("Progress", Range(0,1)) = 0
        _Softness ("Softness", Range(0.001,0.2)) = 0.05
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

            float4 _Tint;
            float _RingThickness;
            float _Progress;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = length(i.uv);
                float radius = saturate(_Progress * 0.95 + 0.05);
                float outer = smoothstep(radius + _Softness, radius, dist);
                float inner = smoothstep(radius - _RingThickness, radius - _RingThickness + _Softness, dist);
                float ring = saturate(outer * inner);
                float centerGlow = saturate((1.0 - dist) * (1.0 - _Progress)) * 0.35;
                return _Tint * saturate(ring + centerGlow);
            }
            ENDHLSL
        }
    }
}