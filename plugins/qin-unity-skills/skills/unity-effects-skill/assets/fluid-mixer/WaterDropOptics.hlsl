#ifndef WATER_STUDY_DROP_OPTICS
#define WATER_STUDY_DROP_OPTICS

float _QinFluidNeutralSurface;

static const float3 WaterGlass = float3(.115, .51, .79);
static const float3 WaterFoam = float3(.83, 1.06, 1.18);

float WaterLine(float distanceFromCrest, float width)
{
    float filter = max(.004, fwidth(distanceFromCrest) * .82);
    return 1.0 - smoothstep(width - filter * .6, width + filter, abs(distanceFromCrest));
}

void DetachedWater(float2 q, float t, float seed, out float3 color, out float alpha)
{
    float phase = 11.0 * t + 29.0 * seed;
    float y = q.y + .12 * sin(phase) * (1.0 - q.x * q.x);
    float taper = .67 + .29 * smoothstep(-.94, .66, q.x) + .08 * sin(phase + 8.0 * q.x);
    float r = length(float2(q.x * 1.10, y / taper));
    float film = 1.0 - smoothstep(.69, .93, r);
    float rim = WaterLine(r - .70, .097) * smoothstep(-.48, .51, y + .31 * q.x);
    float glint = exp(-19.0 * dot(float2(q.x - .33 + .09 * sin(phase), y - .32), float2(q.x - .33 + .09 * sin(phase), y - .32)));
    color = lerp(WaterGlass, WaterFoam, saturate(.54 * rim + .69 * glint));
    alpha = film * (.27 + .48 * rim + .51 * glint);
}

// Retain the bound Profile slot so the composed VFX output can reuse its saved graph wiring.
void WaterDropOptics_float(float4 UV, float Profile, float Opacity, float PatternSeed,
    float ParticleAge, out float3 Color, out float Alpha)
{
    float live = 1. - saturate(_QinFluidNeutralSurface);
    float t = lerp(1.02, ParticleAge, live);
    float seed = lerp(.41, PatternSeed, live);
    DetachedWater(2. * UV.xy - 1., t, seed, Color, Alpha);
}
#endif
