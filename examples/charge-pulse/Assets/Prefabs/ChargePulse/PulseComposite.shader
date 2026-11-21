Shader "Qin Samples/Pulse Composite"
{
    Properties
    {
        _MainTex ("Body", 2D) = "white" {}
        _LineTex ("Line", 2D) = "white" {}
        _Sweep ("Arc sweep", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            #include "PulseComposite.hlsl"

            sampler2D _MainTex;
            sampler2D _LineTex;
            float _Sweep;

            float4 Fragment(v2f_img input) : SV_Target
            {
                float4 body = tex2D(_MainTex, input.uv);
                float4 filament = tex2D(_LineTex, input.uv);
                float2 offset = input.uv - 0.5;
                float turns = atan2(offset.y, offset.x) / 6.2831853 + 0.5;
                filament.a *= 1.0 - step(_Sweep, turns);
                float4 color;
                PulseComposite_float(body, filament, color);
                return color;
            }
            ENDCG
        }
    }
}
