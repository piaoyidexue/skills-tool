Shader "ElementLine/EnergyAbsorb"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.6, 0.35, 1, 1)
        _CoreGlow ("Core Glow Color", Color) = (1, 1, 1, 1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _Progress ("Absorb Progress", Range(0, 1)) = 0
        _VortexStrength ("Vortex Strength", Range(0, 3)) = 1.5
        _SoftEdge ("Soft Edge", Range(0.01, 0.2)) = 0.08
        _ParticleNoise ("Particle Noise", Range(0, 1)) = 0.6
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
            float4 _CoreGlow;
            float _Progress;
            float _VortexStrength;
            float _SoftEdge;
            float _ParticleNoise;

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
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = length(i.uv);

                // absorb: outer ring shrinks inward
                float absorbRadius = lerp(1.1, 0.15, _Progress);
                float outerRing = smoothstep(absorbRadius + _SoftEdge, absorbRadius, dist);
                float innerRing = smoothstep(absorbRadius * 0.35, absorbRadius * 0.55, dist);
                float coreRing = smoothstep(absorbRadius * 0.05, absorbRadius * 0.12, dist);

                // vortex/spiral feel
                float angle = atan2(i.uv.y, i.uv.x);
                float spiral = sin(angle * 3.0 + dist * 8.0 + _Time.y * 4.0) * 0.5 + 0.5;
                float vortex = spiral * _VortexStrength * (1.0 - _Progress);

                // noise particles pulled inward
                float2 noiseUv = i.uv * 0.5 + 0.5 + _Time.y * 0.2;
                float noise = tex2D(_MainTex, noiseUv).r;
                float particles = noise * _ParticleNoise * saturate((1.0 - dist) * 3.0);

                // color layers
                float4 outerLayer = _Tint * outerRing * (0.65 + vortex * 0.35);
                float4 innerLayer = _CoreGlow * innerRing * 0.8;
                float4 coreLayer = _CoreGlow * coreRing * (1.0 + _Progress * 1.5);
                float4 particleLayer = (_Tint + _CoreGlow) * 0.5 * particles * (0.3 + _Progress * 0.5);

                return saturate(outerLayer + innerLayer + coreLayer + particleLayer);
            }
            ENDHLSL
        }
    }
}
