Shader "Qin Samples/Pulse Glow"
{
    Properties
    {
        _Tint ("Linear tint and opacity", Color) = (0.2, 1, 0.85, 1)
        _RingWeight ("Ring coverage", Range(0, 1)) = 1
        _CoreWeight ("Core coverage", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Input
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Tint;
            float _RingWeight;
            float _CoreWeight;

            Varyings Vert(Input input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float edge = max(fwidth(radius), 0.003);
                float ring = 1.0 - smoothstep(0.035, 0.075 + edge, abs(radius - 0.77));
                float core = 1.0 - smoothstep(0.0, 1.0, radius);
                return half4(_Tint.rgb, _Tint.a * saturate(ring * _RingWeight + core * _CoreWeight));
            }
            ENDCG
        }
    }
}
