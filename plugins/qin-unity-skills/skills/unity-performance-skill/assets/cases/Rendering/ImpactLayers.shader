Shader "Hidden/Qin/PerformanceCases/ImpactLayers"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        float4 _ImpactCenter;
        struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings Vert(uint vertexID : SV_VertexID)
        {
            Varyings output;
            output.position = GetFullScreenTriangleVertexPosition(vertexID);
            output.uv = GetFullScreenTriangleTexCoord(vertexID);
            return output;
        }

        float Core(float radius) { return saturate(1 - 5 * radius) * 0.15; }
        float Ring(float radius) { return saturate(1 - 20 * abs(radius - 0.19)) * 0.3; }
        float4 CoreOnly(Varyings input) : SV_Target { return float4(Core(distance(input.uv, _ImpactCenter.xy)), 0, 0, 0); }
        float4 RingOnly(Varyings input) : SV_Target { return float4(0, Ring(distance(input.uv, _ImpactCenter.xy)), 0, 0); }
        float4 Combined(Varyings input) : SV_Target
        {
            float radius = distance(input.uv, _ImpactCenter.xy);
            return float4(Core(radius), Ring(radius), 0, 0);
        }
        ENDHLSL

        Pass
        {
            Name "CoreOnly"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CoreOnly
            ENDHLSL
        }
        Pass
        {
            Name "RingOnly"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment RingOnly
            ENDHLSL
        }
        Pass
        {
            Name "Combined"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Combined
            ENDHLSL
        }
    }
}
