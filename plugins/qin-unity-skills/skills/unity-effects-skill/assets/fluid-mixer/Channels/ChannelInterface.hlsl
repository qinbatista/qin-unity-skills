#ifndef QIN_CHANNEL_INTERFACE
#define QIN_CHANNEL_INTERFACE
void ChannelInterface(float2 uv, UnityTexture2D field, out float pigment, out float rim,
    out float opacity, out float2 bend, out float crest)
{
    float4 surface = SAMPLE_TEXTURE2D(field.tex, field.samplerstate, float2(uv.x, .625));
    float4 motion = SAMPLE_TEXTURE2D(field.tex, field.samplerstate, float2(uv.x, .875));
    float y = (uv.y - .5) * 2.4;
    float below = surface.x - y;
    float depth = max(0., surface.x - surface.y);
    float coverage = smoothstep(-.008, .008, below) * smoothstep(-.010, .015, y - surface.y)
                   * smoothstep(.002, .026, depth);
    float speed = saturate(abs(motion.x));
    float tracer = surface.z + .53 * surface.w;
    float layer = saturate(below / max(.025, depth));
    float vein = sin(19. * below + 6.2 * tracer + 1.1 * sin(35. * below + 4. * surface.w));
    float thread = smoothstep(.68, .985, vein) * smoothstep(.08, .6, surface.z)
                 * (1. - smoothstep(.25, .85, layer));
    float lip = 1. - smoothstep(.008, .042 + .017 * speed, abs(below));
    float skin = (1. - smoothstep(.027, .13, below)) * (.32 + .56 * surface.z);
    pigment = saturate(.14 + .66 * layer + .11 * surface.w);
    rim = saturate(lip * (.60 + .23 * surface.w) + .25 * skin + thread * (.08 + .23 * speed));
    crest = saturate(.57 * lip + .52 * motion.z + .18 * thread);
    opacity = coverage * (.34 + .25 * layer + .34 * rim);
    bend = coverage * float2(-motion.y * 1.53 + .25 * (surface.z - .5), .19 * vein * (.25 + speed));
}
#endif
