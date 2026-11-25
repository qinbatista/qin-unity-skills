Shader "Qin/Water/BowlProjection"
{
    Properties
    {
        [HideInInspector] _LiquidFilm ("Channel presentation", 2D) = "black" {}
        [HideInInspector] _FilmWeight ("Channel visibility", Range(0,1)) = 0
        [HideInInspector] _BowlBackdrop ("Dry camera image", 2D) = "black" {}
        [HideInInspector] _UseBowlBackdrop ("Use dry camera image", Float) = 0
        [HideInInspector] _BowlScreenRect ("Camera center and radii", Vector) = (0.5,0.5,0.5,0.5)
        _RimRise ("Rim height / resting water depth", Range(1,1.8)) = 1.18
        _OpticalStrength ("Refraction and reflections", Range(0,1.5)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Bowl water"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BowlVertex
            #pragma fragment BowlFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LiquidFilm);
            TEXTURE2D(_BowlBackdrop);
            TEXTURE2D_X(_CameraSortingLayerTexture);
            float4 _CameraSortingLayerTexture_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _BowlScreenRect, _BowlBackdrop_TexelSize;
                float _FilmWeight, _UseBowlBackdrop, _RimRise, _OpticalStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPosition : TEXCOORD1; };

            Varyings BowlVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs projected = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = projected.positionCS;
                output.screenPosition = projected.positionNDC;
                output.uv = input.uv;
                return output;
            }

            float4 BowlFragment(Varyings input) : SV_Target
            {
                float2 local = 2.0 * input.uv - 1.0;
                float radiusSq = dot(local, local);
                clip(1.0 - radiusSq);
                clip(_FilmWeight - 0.0001);

                // Each column is an original solved channel cell. Row 2 holds elevation,
                // bed and two dyes. Row 3 holds velocity, slope, foam and wet fraction.
                float column = clamp(input.uv.x, 0.5 / 128.0, 1.0 - 0.5 / 128.0);
                float4 section = SAMPLE_TEXTURE2D_LOD(_LiquidFilm, sampler_LinearClamp, float2(column, 0.625), 0);
                float4 flow = SAMPLE_TEXTURE2D_LOD(_LiquidFilm, sampler_LinearClamp, float2(column, 0.875), 0);
                float restDepth = max(0.02, -section.y);
                float curvedBed = section.y + restDepth * max(_RimRise, 1.0) * radiusSq;
                float waterDepth = section.x - curvedBed;
                float transition = max(fwidth(waterDepth), 0.0015);
                float wet = smoothstep(0.0, transition, waterDepth) * saturate(flow.w);
                clip(wet - 0.0001);

                float depth = saturate(waterDepth / restDepth);
                float speed = saturate(abs(flow.x) * 0.85);
                float slope = clamp(flow.y, -1.5, 1.5);
                float2 outward = local * rsqrt(max(radiusSq, 0.0001));
                float meniscus = exp(-max(waterDepth, 0.0) / max(0.012 * restDepth, 1.4 * transition));
                float2 light = float2(-0.61, 0.79);
                float grazing = 0.2 + 0.8 * saturate(dot(outward, light));

                // The solved longitudinal slope bends the scene. The curved contact
                // line adds a radial normal only along the shallow lip.
                float tracer = section.z - section.w;
                float2 bend = float2(-0.025 * slope, 0.004 * tracer * speed)
                            + outward * (0.007 * meniscus + 0.002 * depth);
                float2 tap = local + bend * _OpticalStrength;
                // Leave one bilinear footprint of clearance on the captured hardware.
                float2 screenPerTexel = _ScaledScreenParams.xy / max(_CameraSortingLayerTexture_TexelSize.zw, 1.0);
                float inset = _UseBowlBackdrop > 0.5
                    ? length(abs(_BowlBackdrop_TexelSize.xy) / max(_BowlScreenRect.zw, 0.00001))
                    : length(fwidth(local)) * max(screenPerTexel.x, screenPerTexel.y);
                // A source pixel wider than the aperture cannot provide an interior sample.
                clip(0.99 - inset);
                tap *= min(1.0, (1.0 - inset) * rsqrt(max(dot(tap, tap), 0.0001)));

                float3 plate;
                if (_UseBowlBackdrop > 0.5)
                {
                    float2 uv = _BowlScreenRect.xy + tap * _BowlScreenRect.zw;
                    plate = SAMPLE_TEXTURE2D_LOD(_BowlBackdrop, sampler_LinearClamp, saturate(uv), 0).rgb;
                }
                else
                {
                    // Recover the screen-space offset from the projected quad, including
                    // RT orientation. This also works when a camera rolls relative to it.
                    float2 screen = input.screenPosition.xy / input.screenPosition.w;
                    float2 dx = ddx(local), dy = ddy(local);
                    float det = dx.x * dy.y - dx.y * dy.x;
                    float2 delta = tap - local;
                    float2 steps = float2(delta.x * dy.y - delta.y * dy.x, dx.x * delta.y - dx.y * delta.x)
                                 / (abs(det) > 1e-12 ? det : 1.0);
                    float2 uv = saturate(screen + steps.x * ddx(screen) + steps.y * ddy(screen));
                    if (min(_CameraSortingLayerTexture_TexelSize.z, _CameraSortingLayerTexture_TexelSize.w) < 2)
                        discard;
                    plate = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture, sampler_LinearClamp, uv).rgb;
                }

                float3 transmission = plate * lerp(float3(0.99, 1.0, 1.0), float3(0.80, 0.94, 0.975), depth);
                transmission += depth * float3(0.004, 0.014, 0.018);
                // Stationary sky reflected from a slope-dependent curved surface normal.
                float shoulder = local.y + 0.36 * local.x * local.x - 0.42 * slope - 0.13 * tracer;
                float glint = exp(-abs(shoulder - 0.37) / 0.043) * (0.035 + 0.15 * speed);
                float rim = meniscus * grazing * (0.64 + 0.20 * saturate(flow.z));
                float3 color = transmission + _OpticalStrength * (rim + glint) * float3(0.21, 0.29, 0.28);
                return float4(color, saturate(_FilmWeight) * wet * (0.74 + 0.19 * depth));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
