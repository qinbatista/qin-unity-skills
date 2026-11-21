#ifndef QIN_PULSE_COMPOSITE_INCLUDED
#define QIN_PULSE_COMPOSITE_INCLUDED

// Shader Graph Custom Function, File mode, PulseComposite, Float precision.
void PulseComposite_float(float4 Body, float4 Line, out float4 Color)
{
    float lineCoverage = saturate(Line.a);
    float uncoveredBody = saturate(Body.a) * (1.0 - lineCoverage);
    float coverage = lineCoverage + uncoveredBody;
    float3 weighted = Line.rgb * lineCoverage + Body.rgb * uncoveredBody;
    Color = float4(coverage > 0.00001 ? weighted / coverage : 0.0, coverage);
}

#endif
