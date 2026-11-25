Shader "Hidden/Qin/FocusedRainStudy"
{
    Properties
    {
        _DryPlate ("Undisturbed pool", 2D) = "gray" {}
        _RainMask ("Water coverage", 2D) = "white" {}
        _UseDryPlate ("Use pool photograph", Float) = 1
        _QinTopDownState ("Solved height and velocity", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment RainFragment
            #include "UnityCG.cginc"

            sampler2D _DryPlate, _RainMask, _QinTopDownState;
            float _UseDryPlate;
            int _RainDropCount, _RainHitCount;
            float4 _RainDrops[64], _RainHits[64];
            static const float2 Domain = float2(8.0, 4.5);
            static const float2 CellUv = float2(1.0/128.0, 1.0/72.0);

            float HeightAt(float2 uv)
            {
                return tex2Dlod(_QinTopDownState, float4(saturate(uv), 0, 0)).r;
            }

            float3 Backdrop(float2 uv)
            {
                if (_UseDryPlate > 0.5)
                    return tex2Dlod(_DryPlate, float4(saturate(uv), 0, 0)).rgb;
                return lerp(float3(.025,.075,.08), float3(.18,.27,.32),
                    smoothstep(-.2,.65,uv.x*.69 + uv.y*.31));
            }

            float2 CapillarySlope(float2 distanceM, float age, float force)
            {
                // These centimetre waves fall below the bulk solver's grid spacing.
                // Differentiate the dispersing radial packet in metres to perturb the
                // optical normal. Its only origin and clock are a recorded water impact.
                if (age <= 0 || age >= 1.08 || max(abs(distanceM.x),abs(distanceM.y))>.38)
                    return 0;
                float radius = length(distanceM);
                float k = 6.283185/lerp(.037,.050,force);
                float tension = .000073*k*k;  // Surface tension divided by water density, times k².
                float omega = sqrt(k*(9.81+tension));
                float groupSpeed = (9.81+3*tension)/(2*omega);
                float width = .023+.022*age;
                float offset = radius - (.022+groupSpeed*age);
                if (abs(offset) > 3*width) return 0;
                float envelope = exp(-.5*offset*offset/(width*width) - 1.35*age);
                float spread = rsqrt(1 + radius/.036);
                float core = saturate(radius/.018);
                float coreGain = core*core*(3-2*core);
                float coreDerivative = 6*core*(1-core)/.018;
                float launch = smoothstep(0,.054,age);
                float retire = 1-smoothstep(.79,1.08,age);
                float amplitude = lerp(.00055,.00165,force*force)*envelope*spread*launch*retire;
                float carrier, quadrature;
                sincos(k*(radius-.022)-omega*age,carrier,quadrature);
                float envelopeDerivative = -offset/(width*width) - .5/(.036+radius);
                float slope = amplitude*(coreGain*(k*quadrature + envelopeDerivative*carrier)
                                       + coreDerivative*carrier);
                return slope*distanceM/max(.0001,radius);
            }

            float3 WetSurface(float2 uv, float wet, float2 capillary)
            {
                // The oblique camera sees a narrower horizontal slice of the physical domain.
                // RainDropTimeline.World projects the world-space impacts with the same scale.
                float2 fieldUv = float2(.5 + .76*(uv.x-.5), uv.y);
                float2 dx = float2(CellUv.x, 0), dy = float2(0, CellUv.y);
                float west = HeightAt(fieldUv-dx), east = HeightAt(fieldUv+dx);
                float south = HeightAt(fieldUv-dy), north = HeightAt(fieldUv+dy);
                float2 gradient = float2(east-west,north-south)/.125;
                float magnitude = length(gradient);
                float2 bend = gradient * (2.0/(1+22*magnitude)) + .97*capillary;
                float2 reflectionUv = uv + bend * float2(.042,.062);
                float tapWet = tex2Dlod(_RainMask, float4(saturate(reflectionUv),0,0)).r;
                float3 reflection = Backdrop(lerp(uv,reflectionUv,wet*smoothstep(.15,.96,tapWet)));
                float2 transmissionUv = uv - bend * float2(.010,.012);
                float3 transmitted = Backdrop(lerp(uv,transmissionUv,wet));
                float3 water = lerp(transmitted,reflection,.85);
                // A narrow sky opening reflects from an inclined shoulder; a nearby canopy
                // silhouette darkens its reverse slope. Both vanish with the measured slope.
                float facing = dot(gradient,float2(-.50,.866)) / (.0023 + magnitude);
                float curvature = 4*HeightAt(fieldUv) - (west+east+south+north);
                float ridge = curvature/(.00040 + abs(curvature));
                float light = max(0,facing) * (.40+.60*max(0,facing));
                float shadow = max(0,-facing);
                float plateValue = dot(water,float3(.24,.62,.14));
                float3 sky = lerp(float3(.25,.36,.40),float3(.75,.84,.86),plateValue);
                water = lerp(water,sky,wet*(.15*light + .045*max(ridge,0)));
                water *= 1 - wet*(.12*shadow + .052*max(-ridge,0));
                // Fine crests tip the reflected ray across a cool opening and nearby dark
                // branches. Opposite slopes sample different light on both sides of the crest.
                float2 opening = normalize(float2(-.39+.42*(.68-uv.x), .88+.19*(uv.y-.45)));
                float shortFacing = dot(capillary,opening);
                float shortLight = max(0,shortFacing)/(.037+abs(shortFacing));
                float shortShade = max(0,-shortFacing)/(.041+abs(shortFacing));
                float alignment = abs(shortFacing)/max(.00001,length(capillary));
                float arc = lerp(.35,1,smoothstep(.05,.84,alignment));
                float3 nearbyReflection = Backdrop(uv + opening*float2(.009,.016));
                float openLight = dot(.6*reflection+.4*nearbyReflection,float3(.24,.62,.14));
                float illumination = lerp(.67,1,smoothstep(.09,.43,openLight));
                float3 canopySky = lerp(float3(.47,.61,.66),float3(.75,.83,.85),plateValue);
                water = lerp(water,canopySky,wet*.59*shortLight*arc*illumination);
                water *= 1 - wet*.44*shortShade*arc;
                return saturate(water);
            }

            float SoftDisc(float2 q, float radius, float pixel)
            {
                return saturate(.5 + (radius-length(q))/pixel);
            }

            float3 Contact(float3 color, float2 p, float age, float force, float phase, float pixel)
            {
                // The crown is the brief overturning lip at the contact. The travelling wave is
                // already in the height grid and remains visible after this local event retires.
                float lifetime = lerp(.105,.165,force);
                float t = saturate(age/lifetime);
                float height = lerp(.022,.051,force) * 4*t*(1-t);
                float radius = lerp(.022,.080,force) * (.48 + .80*t);
                float2 lip = float2(p.x,(p.y - height*.52)/.65);
                float angle = atan2(lip.y,lip.x);
                float fingers = .5 + .5*cos(5*angle + phase);
                float crest = radius*(.93+.14*fingers);
                float lipLine = saturate(.5+(pixel*.52-abs(length(lip)-crest))/pixel);
                float envelope = (1-smoothstep(.49,1,t)) * smoothstep(0,.15,t);
                float lightArc = saturate(.20+.48*dot(normalize(lip+float2(.00001,0)),float2(-.67,.74)) + .50*fingers);
                float lipOpacity = lipLine*envelope*lightArc;
                color = lerp(color,float3(.60,.74,.78),lipOpacity*.68);
                float jetRadius = lerp(.006,.013,force) * (1-t);
                float jet = SoftDisc(float2(p.x*1.2,(p.y-height)/1.65),jetRadius,pixel);
                color = lerp(color,float3(.70,.80,.81),jet*envelope*.64);

                // A few secondary droplets separate from a strong lip on ballistic arcs.
                if (force > .32 && age > .025 && age < .24)
                {
                    float flight = age-.025;
                    [unroll] for (int bead=0; bead<3; bead++)
                    {
                        float azimuth = phase+bead*2.40;
                        float lift = lerp(.64,1.12,force) + .10*cos(azimuth*2.17);
                        float altitude = flight*(lift-4.905*flight);
                        if (altitude <= 0) continue;
                        float2 motion = float2(cos(azimuth),.60*sin(azimuth)) * flight*(.34+.34*force);
                        float2 position = motion + float2(0,altitude*.83);
                        float beadSize = (.005+.007*force)*(.78+.22*sin(azimuth));
                        float opacity = SoftDisc(p-position,beadSize,pixel) * saturate(altitude/.016);
                        color = lerp(color,float3(.70,.79,.79),opacity*.68);
                    }
                }
                return color;
            }

            float3 Airborne(float3 color, float2 uv, float4 drop, float pixel)
            {
                float2 path = (drop.zw-drop.xy)*Domain;
                float2 delta = (uv-drop.xy)*Domain;
                float lengthSquared = dot(path,path);
                float along = saturate(dot(delta,path)/max(.000001,lengthSquared));
                float2 axis = path*rsqrt(max(.000001,lengthSquared));
                float2 across = float2(-axis.y,axis.x);
                float2 q = delta - along*path;
                float scale = saturate(sqrt(lengthSquared)*4.1);
                float width = (.008+.0045*scale)*(1-.53*along);
                float cover = SoftDisc(q,width,pixel);
                float darkEdge = SoftDisc(q+across*width*.92,width*.9,pixel);
                float opacity = (.96-.70*along)*(.76+.24*scale);
                color *= 1-darkEdge*opacity*.36;
                return lerp(color,float3(.79,.87,.88),cover*opacity*.91);
            }

            float4 RainFragment(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float pixel = max(.001, max(fwidth(uv.x)*Domain.x,fwidth(uv.y)*Domain.y));
                float wet = tex2Dlod(_RainMask,float4(uv,0,0)).r;
                float2 capillary = 0;
                [loop] for (int k=0; k<_RainHitCount; k++)
                {
                    float4 hit = _RainHits[k];
                    float2 distanceM = (uv-hit.xy)*float2(.76*Domain.x,Domain.y);
                    capillary += CapillarySlope(distanceM,hit.z,saturate(hit.w));
                }
                float3 color = WetSurface(uv,wet,capillary);
                [loop] for (int j=0; j<_RainHitCount; j++)
                {
                    float4 hit = _RainHits[j];
                    float2 p = (uv-hit.xy)*Domain;
                    if (wet < .8 || max(abs(p.x),abs(p.y)) > .24 || hit.z > .24) continue;
                    float phase = frac(sin(dot(hit.xy,float2(23.71,81.32)))*3658.57)*6.283185;
                    color = Contact(color,p,hit.z,saturate(hit.w),phase,pixel);
                }
                [loop] for (int i=0; i<_RainDropCount; i++)
                {
                    float4 drop = _RainDrops[i];
                    float2 off = max(min(drop.xy,drop.zw)-uv,uv-max(drop.xy,drop.zw))*Domain;
                    if (max(off.x,off.y) > .026) continue;
                    color = Airborne(color,uv,drop,pixel);
                }
                return float4(color,1);
            }
            ENDCG
        }
    }
}
