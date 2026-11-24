Shader "Qin/FlowSky2D"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Case ("Case", Range(0, 23)) = 0
        _Phase ("Normalized phase", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            #include "FlowSky2D.cginc"
            float _Case, _Phase, _Opacity;
            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varying Vertex(VertexInput v)
            {
                Varying output;
                output.vertex = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                return output;
            }

            float4 Fragment(Varying input) : SV_Target
            {
                float3 color;
                float alpha;
                FlowSky2D_float(input.uv, _Case, _Phase, color, alpha);
                alpha *= saturate(_Opacity);
                return float4(color * alpha, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
