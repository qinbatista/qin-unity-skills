#ifndef QIN_FLUID_SURFACE_INCLUDED
#define QIN_FLUID_SURFACE_INCLUDED

#include "WaterStudyOptics.hlsl"

float4 FluidField(UnityTexture2D field, float2 uv)
{
    return SAMPLE_TEXTURE2D(field.tex, field.samplerstate, saturate(uv));
}

void SideWaterline(float2 uv, UnityTexture2D film, out float pigment, out float rim,
    out float opacity, out float2 bend, out float crest)
{
    float dx = max(film.texelSize.x, .0001);
    float2 p = float2(uv.x, .5);
    float2 value = FluidField(film, p).rg;
    float2 left = FluidField(film, p - float2(dx, 0.)).rg;
    float2 right = FluidField(film, p + float2(dx, 0.)).rg;
    float slope = (right.x - left.x) / max(12. * dx, .001);
    float energy = abs(value.x) + .036 * abs(value.y) + .024 * abs(slope);
    float wet = smoothstep(.0006, .023, energy);
    float end = 1. - smoothstep(.79, .995, abs(2. * uv.x - 1.));
    // This sprite is a side section. Its top is the displaced physical waterline.
    float localY = (uv.y - .5) * 1.024;
    float top = .040 + clamp(value.x * 2.35, -.13, .165) * end;
    float below = top - localY;
    float body = smoothstep(-.014, .022, below) * (1. - smoothstep(.040, .190, below));
    float stroke = smoothstep(-.015, .009, below) * (1. - smoothstep(.010, .050, below));
    crest = saturate(.46 * wet + 1.65 * abs(slope));
    pigment = saturate(.23 + 2.0 * below);
    rim = stroke * (.50 + .31 * crest);
    opacity = end * wet * (body * .28 + rim * .62);
    bend = float2(-slope * 1.25, value.y * .14) * body;
}

void FluidInterface_float(float4 UV, UnityTexture2D Film, float NearSide, out float Depth,
    out float Reflectance, out float WetAlpha, out float2 Slope, out float Crest)
{
    float2 uv = UV.xy;
    if (NearSide < .5)
    {
        SideWaterline(uv, Film, Depth, Reflectance, WetAlpha, Slope, Crest);
        return;
    }

    float2 stepUV = Film.texelSize.xy;
    float4 center = FluidField(Film, uv);
    float4 left = FluidField(Film, uv - float2(stepUV.x, 0.));
    float4 right = FluidField(Film, uv + float2(stepUV.x, 0.));
    float4 below = FluidField(Film, uv - float2(0., stepUV.y));
    float4 above = FluidField(Film, uv + float2(0., stepUV.y));
    float thickness = (4. * center.r + left.r + right.r + below.r + above.r) * .125;
    float coverage = smoothstep(.091, .50, thickness);
    float2 gradient = float2(right.r - left.r, above.r - below.r) / (1. + .55 * thickness);
    float2 tracer = float2(right.a - left.a, above.a - below.a);
    float speed = saturate(center.b);
    // Lay a cyan stroke along the moving contour and a few interior strokes along the flow.
    float edgeBand = smoothstep(.102, .29, thickness) * (1. - smoothstep(.43, 1.25, thickness));
    float facing = smoothstep(-.19, .38, dot(-gradient, float2(-.36, .93)));
    float contour = edgeBand * (.49 + .42 * facing);
    float interior = smoothstep(.33, .71, thickness);
    float stroke = (1. - smoothstep(.115, .40, abs(center.a - .61))) * interior;
    float movingFoam = stroke * (.19 + .40 * speed) * smoothstep(.046, .45, length(tracer));
    Depth = saturate(.16 + .62 * smoothstep(.15, 2.30, thickness) - interior * .105 * center.a);
    Reflectance = saturate(.82 * contour + movingFoam);
    WetAlpha = coverage * (.16 + .15 * Depth + .32 * Reflectance);
    Slope = -(gradient * .86 + tracer * (.09 + .055 * speed)) * coverage;
    Crest = saturate(contour + movingFoam + .16 * speed);
}

void FluidBackground_float(float4 ScreenUV, float2 Slope, float Crest, float4 Optics,
    float3 WaterColor, float WetAlpha, out float3 Color, out float Alpha)
{
    Color = WaterColor;
    Alpha = WetAlpha;
    ApplyWaterStudyOptics(ScreenUV.xy, Slope, Crest, Optics, Color, Alpha);
}
#endif
