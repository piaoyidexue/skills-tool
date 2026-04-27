Shader "ElementLine/PrismBeam"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.75, 0.88, 1, 1)
        _RefractColor ("Refract Color", Color) = (0.4, 1, 0.95, 1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 1.8
        _Glow ("Glow", Range(0, 5)) = 1.6
        _FacetCount ("Facet Count", Range(2, 10)) = 4
        _FacetSharpness ("Facet Sharpness", Range(0.1, 2)) = 0.6
        _RefractStrength ("Refract Strength", Range(0, 0.5)) = 0.15
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
            float4 _RefractColor;
            float _ScrollSpeed;
            float _Glow;
            float _FacetCount;
            float _FacetSharpness;
            float _RefractStrength;

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

                // faceted prism structure
                float facet = frac(i.uv.x * _FacetCount);
                float facetCenter = abs(facet - 0.5) * 2.0;
                float facetEdge = smoothstep(1.0, 0.3, facetCenter);

                // refractive offset per facet
                float refractOffset = sin(facet * 6.2832) * _RefractStrength;
                float beamY = i.uv.y + refractOffset;

                // main beam with facet separation
                float coreMask = saturate(1.0 - abs(beamY * 2.0 - 1.0) * _FacetSharpness);
                float edgeMask = saturate(1.0 - abs(beamY * 2.0 - 1.0) * (_FacetSharpness * 2.5));

                float noise = tex2D(_MainTex, uv).r;
                float noise2 = tex2D(_MainTex, uv + 0.35).r;

                // prism-like color split
                float4 baseLayer = _Tint * coreMask * (0.5 + noise * 0.35);
                float4 refractLayer = _RefractColor * edgeMask * (0.2 + noise2 * 0.3) * facetEdge;
                float4 glow = (_Tint + _RefractColor) * 0.5 * coreMask * noise * 0.25;

                return (baseLayer + refractLayer + glow) * _Glow;
            }
            ENDHLSL
        }
    }
}
