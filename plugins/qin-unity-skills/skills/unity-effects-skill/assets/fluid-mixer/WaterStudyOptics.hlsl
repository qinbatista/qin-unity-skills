#ifndef WATER_STUDY_OPTICS_INCLUDED
#define WATER_STUDY_OPTICS_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
TEXTURE2D_X(_CameraSortingLayerTexture);
float4 _CameraSortingLayerTexture_TexelSize;
#endif
float _QinFluidOpticsMode;

void ApplyWaterStudyOptics(float2 screenUv, float2 slope, float crest, float4 settings,
    inout float3 color, inout float alpha)
{
#ifndef SHADERGRAPH_PREVIEW
    // The 2D renderer captures through its selected background layer before the water draws.
    float coverage = (1. - alpha) * saturate(alpha * 2.65) * saturate(settings.x);
    if (_QinFluidOpticsMode == 1. || coverage <= .001 ||
        min(_CameraSortingLayerTexture_TexelSize.z, _CameraSortingLayerTexture_TexelSize.w) <= 16.)
        return;

    float2 bend = clamp(slope, -1., 1.) * float2(.0140, .0190) * saturate(settings.x);
    float2 uv = saturate(screenUv + bend * (1. - step(2.5, _QinFluidOpticsMode)));
    float3 scene = SAMPLE_TEXTURE2D_X(_CameraSortingLayerTexture, sampler_LinearClamp, uv).rgb;
    const float3 luminanceWeights = float3(.2126, .7152, .0722);
    float lightness = dot(scene, luminanceWeights);
    float3 reverseChroma = max(0., 2. * lightness - scene);
    float reversal = saturate(settings.y) * (.16 + .84 * saturate(crest))
        * (1. - step(.5, _QinFluidOpticsMode));
    float3 transmitted = lerp(scene, reverseChroma, reversal) * float3(.81, .964, 1.045);

    alpha += coverage;
    color = (color * (alpha - coverage) + transmitted * coverage) / alpha;
#endif
}
#endif
