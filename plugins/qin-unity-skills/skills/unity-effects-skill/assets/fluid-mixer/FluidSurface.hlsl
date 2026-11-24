#ifndef QIN_FLUID_SURFACE_INCLUDED
#define QIN_FLUID_SURFACE_INCLUDED

#include "WaterStudyOptics.hlsl"

float _QinFluidNeutralSurface;

float4 FluidField(UnityTexture2D field, float2 uv)
{
    return SAMPLE_TEXTURE2D(field.tex, field.samplerstate, saturate(uv));
}

// Across a free surface the missing depth is air, not a sample at zero depth.
float FluidNeighborDepth(float4 neighbor, float centerDepth)
{
    return lerp(centerDepth, neighbor.g, smoothstep(.12, .92, neighbor.r));
}

void FluidInterface_float(float4 UV, UnityTexture2D Film, float NearSide, out float Depth,
    out float Reflectance, out float WetAlpha, out float2 Slope, out float Crest)
{
    float2 uv = UV.xy;
    float2 stepUV = max(Film.texelSize.xy, .0001);
    float4 center = FluidField(Film, uv);
    float4 left = FluidField(Film, uv - float2(stepUV.x, 0.));
    float4 right = FluidField(Film, uv + float2(stepUV.x, 0.));
    float4 below = FluidField(Film, uv - float2(0., stepUV.y));
    float4 above = FluidField(Film, uv + float2(0., stepUV.y));

    float2 slope;
    float crest;
    float coverage;
    float absorption;
    float swell;
    float edgeLight;
    float fineHighlight;
    float trough;
    float enabled = 1. - saturate(_QinFluidNeutralSurface);
    if (NearSide < .5)
    {
        // A height field in the horizontal XZ plane. Z is foreshortened only by the renderer.
        float2 meters = stepUV * float2(6., 3.2);
        float2 gradient = float2(right.r - left.r, above.r - below.r) / (2. * meters);
        float laplacian = center.r - .25 * (left.r + right.r + below.r + above.r);
        float gradientLength = length(gradient);
        float energy = abs(center.r) + .083 * abs(center.g) + .14 * gradientLength;
        // The simulation grid is rectangular; its viewing aperture is a soft, irregular shore.
        float2 q = 2. * uv - 1.;
        float r = length(q * float2(.985, 1.0)) +
            .026 * (q.x * q.y + 3. * q.x * q.x * q.y - q.y * q.y);
        float shore = 1. - smoothstep(.82, .987, r);
        coverage = shore * smoothstep(.00045, .009, energy);
        slope = gradient * float2(5.5, 8.1);
        float facing = dot(slope, float2(-.43, .90));
        float turnover = smoothstep(.018, .235, abs(center.g) + .65 * gradientLength);
        float convex = smoothstep(.00010, .0031, laplacian);
        crest = saturate(.48 * turnover + .65 * convex + .29 * smoothstep(.025, .26, gradientLength));
        // A rising face reflects the sky. The returning trough refracts the darker bank.
        edgeLight = turnover * (.24 + .57 * smoothstep(-.11, .56, facing)) + .18 * convex;
        fineHighlight = convex * (.39 + .61 * saturate(.58 + .53 * facing));
        trough = smoothstep(.008, .105, -center.g) * (.4 + .60 * smoothstep(.0, .32, -facing));
        swell = smoothstep(.003, .095, energy);
        absorption = saturate(.15 + .30 * swell + .48 * trough);
    }
    else
    {
        // Thickness and front depth reconstruct a continuous sheet from overlapping water parcels.
        float thickness = (3. * center.r + left.r + right.r + below.r + above.r) / 7.;
        coverage = smoothstep(.100, .59, thickness);
        // One parcel supports a broad reconstruction kernel. Taper its dilute fringe
        // more in the air; overlapping sheets and the low rotating rim retain their mass.
        float projectedHeight = uv.y * 4.18 - .68;
        float fringe = 1. - smoothstep(.35, 1.55, thickness);
        coverage *= 1. - fringe * (.11 + .22 * smoothstep(.33, .84, projectedHeight));
        float2 meters = stepUV * float2(6., 4.18);
        float2 dz = float2(FluidNeighborDepth(right, center.g) - FluidNeighborDepth(left, center.g),
                           FluidNeighborDepth(above, center.g) - FluidNeighborDepth(below, center.g))
                    / (2. * meters);
        float2 thicknessSlope = float2(right.r - left.r, above.r - below.r)
            / (1. + .32 * thickness);
        float freeEdge = 1. - smoothstep(.62, 2.7, thickness);
        // The signed tracer travels with the liquid. Its transverse derivative bends
        // light along individual streaks while the large front-depth normal stays coherent.
        float2 capillaryGradient = float2(right.a - left.a, above.a - below.a) / (2. * meters);
        float speed = saturate(center.b);
        float fineStrength = lerp(.024, .052, speed) * smoothstep(.14, .78, thickness);
        float2 smallSlope = clamp(capillaryGradient * fineStrength, -1.08, 1.08);
        slope = dz * .71 + thicknessSlope * (.23 + .94 * freeEdge) + smallSlope;
        float curvature = max(0., center.r - .25 * (left.r + right.r + below.r + above.r));
        float facing = dot(-thicknessSlope, float2(-.51, .86));
        crest = saturate(.61 * length(slope) + .17 * curvature + .17 * speed);
        float convergence = dot(smallSlope, float2(-.55, .835));
        fineHighlight = smoothstep(.16, .75, convergence) * (.28 + .72 * speed);
        trough = smoothstep(.13, .72, -convergence) * (.32 + .68 * speed);
        edgeLight = (1. - smoothstep(.43, 1.59, thickness)) *
            smoothstep(.045, .66, length(thicknessSlope)) * (.30 + .70 * smoothstep(-.36, .50, facing));
        swell = smoothstep(.16, 3.8, thickness);
        absorption = 1. - exp(-.29 * thickness);
    }

    // View-dependent reflection of a broad sky and two narrow light sources on the evolving normal.
    float3 normal = normalize(float3(-slope, 1.));
    float facingUp = dot(normal.xy, float2(-.49, .87));
    float fresnel = pow(saturate(1. - normal.z), 1.55);
    float broad = smoothstep(.12, .76, facingUp) * (.20 + .80 * crest);
    float key = pow(saturate(dot(normal, float3(-.384, .529, .756))), 84.);
    float shoulder = pow(saturate(dot(normal, float3(.596, .307, .742))), 61.);
    float ridge = smoothstep(.32, .45, facingUp) * (1. - smoothstep(.50, .62, facingUp));
    float reflection = enabled * saturate(.10 * broad + .83 * key + .47 * shoulder +
        .45 * edgeLight + .35 * fresnel + .20 * ridge * crest + .39 * fineHighlight);
    float clarity = .060 + .095 * swell + .103 * fresnel + .15 * trough;
    Depth = absorption;
    Reflectance = reflection;
    WetAlpha = saturate(coverage * (clarity + .65 * reflection));
    Slope = -slope * enabled * 1.70;
    Crest = crest;
}

void FluidBackground_float(float4 ScreenUV, float2 Slope, float Crest, float4 Optics,
    float3 WaterColor, float WetAlpha, out float3 Color, out float Alpha)
{
    Color = WaterColor;
    Alpha = WetAlpha;
    ApplyWaterStudyOptics(ScreenUV.xy, Slope, Crest, Optics, Color, Alpha);
}
#endif
