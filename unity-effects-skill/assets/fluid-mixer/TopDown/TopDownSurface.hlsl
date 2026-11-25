#ifndef QIN_TOP_DOWN_SURFACE
#define QIN_TOP_DOWN_SURFACE
#include "WaterDomain.hlsl"
TEXTURE2D(_QinTopDownState);
SAMPLER(sampler_QinTopDownState);
float4 _QinTopDownActor;

float BasinHash(float2 p)
{
    float3 v=frac(float3(p.xyx)*float3(.1031,.1030,.0973));
    v+=dot(v,v.yzx+33.33);
    return frac((v.x+v.y)*v.z);
}
float BasinNoise(float2 p)
{
    float2 i=floor(p), f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(BasinHash(i),BasinHash(i+float2(1,0)),f.x),
                lerp(BasinHash(i+float2(0,1)),BasinHash(i+1),f.x),f.y);
}
float4 BasinField(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_QinTopDownState,sampler_QinTopDownState,saturate(uv),0);
}
float3 RiverBed(float2 p)
{
    float broad=BasinNoise(p*1.5)+.34*BasinNoise(p*4.7);
    float2 grid=p*3.1;
    float2 id=floor(grid), q=frac(grid)-.5;
    float pick=BasinHash(id);
    q-=.42*(float2(pick,BasinHash(id+14))-.5);
    float angle=pick*6.28;
    q=float2(q.x*cos(angle)-q.y*sin(angle),q.x*sin(angle)+q.y*cos(angle));
    float pebble=length(q*float2(1,1.65));
    float rock=1-smoothstep(.16*pick+.04,.16*pick+.065,pebble);
    float3 color=lerp(float3(.026,.089,.078),float3(.18,.24,.165),saturate(broad*.84));
    return color+rock*(.032+.065*saturate(.6-q.y*3))*float3(1,.97,.78);
}
float3 BankMaterial(float2 p,float dist)
{
    float n=BasinNoise(p*2.1), grains=BasinNoise(p*38.0);
    float ridges=BasinNoise(p*float2(5,14)+n*2.5);
    float3 soil=lerp(float3(.135,.104,.067),float3(.455,.410,.243),n*.76+grains*.24);
    soil+=float3(.055,.05,.030)*smoothstep(.48,.67,ridges);
    float moss=smoothstep(.35,.7,n+.12*grains-.24*exp(-10*abs(dist)));
    soil=lerp(soil,float3(.098,.174,.100)+.075*n,moss*.77);
    return soil*lerp(.58,1,smoothstep(.0,.17,-dist));
}
float3 BoatPaint(float3 color,float2 p)
{
    float2 axis=_QinTopDownActor.zw;
    axis/=max(.001,length(axis));
    float2 delta=p-_QinTopDownActor.xy;
    float t=dot(delta,axis), side=dot(delta,float2(-axis.y,axis.x));
    float width=.117*(1-smoothstep(.12,.44,t))*(.69+.31*smoothstep(-.34,-.17,t));
    float body=max(abs(side)-width,max(-t-.31,t-.44));
    float mask=1-smoothstep(-.004,.007,body);
    float3 varnish=lerp(float3(.22,.066,.021),float3(.68,.385,.106),.5+.5*sin(side*175+1.4*sin(17*t)));
    float inner=smoothstep(-.057,-.039,body);
    float3 hull=lerp(varnish,float3(.91,.754,.451),inner);
    float bench=(1-smoothstep(.011,.024,abs(abs(t+.006)-.102)))*step(abs(side),.077);
    hull=lerp(hull,float3(.113,.070,.041),bench);
    float keel=(1-smoothstep(.006,.012,abs(side)))*smoothstep(.13,.26,t);
    return lerp(color,lerp(hull,float3(.883,.696,.32),keel),mask);
}

void TopDownSurface_float(float2 UV,float Profile,float BankOpacity,out float3 Color,out float Alpha)
{
    float2 p=(UV-.5)*BasinSize;
    int kind=(int)(Profile+.5);
    float coast=WaterBoundary(p,kind);
    float2 texel=float2(1.0/128.0,1.0/72.0);
    float4 c=BasinField(UV);
    float2 slope=float2(BasinField(UV+float2(texel.x,0)).x-BasinField(UV-float2(texel.x,0)).x,
                        BasinField(UV+float2(0,texel.y)).x-BasinField(UV-float2(0,texel.y)).x)/.125;
    // Millimetre-scale rain crests need stronger optical normals at gameplay scale.
    if (kind==5) slope*=11;
    float2 bent=p+.16*slope;
    float depth=saturate(.22+1.18*coast+.11*BasinNoise(p*1.43));
    float3 bed=RiverBed(bent);
    float3 deep=lerp(float3(.010,.066,.083),float3(.018,.153,.167),BasinNoise(bent*1.4));
    float3 water=lerp(bed*.87+float3(.022,.093,.083),deep,.62*depth);
    water+=float3(.018,.059,.052)*(1-smoothstep(.01,.17,coast));
    float facing=dot(slope,normalize(float2(-.52,.91)));
    float ridge=saturate(max(facing,0)/(0.51+abs(facing)));
    water+=ridge*float3(.062,.141,.149);
    water*=1-.27*saturate(max(-facing,0)/(.40+abs(facing)));
    // A sloped sheet reflects a narrow band of sky; soft shoulders preserve
    // detail in high-amplitude wakes and in the two currents around an island.
    float glintWidth=.10+.045*BasinNoise(15*bent);
    float glint=exp(-abs(facing-.45)/glintWidth)*smoothstep(.18,.52,length(slope));
    water+=float3(.18,.235,.19)*glint*(.34+.45*BasinNoise(9*bent));
    float foam=smoothstep(.60,1.75,facing)*smoothstep(.10,.37,c.w);
    water=lerp(water,float3(.57,.76,.68),foam*.54);
    float dye=c.w;
    float tracerCoverage=smoothstep(.018,.16,dye);
    float grain=BasinNoise(bent*float2(3,13));
    float3 stream=lerp(float3(.054,.315,.30),float3(.41,.67,.54),smoothstep(.11,.58,dye));
    water=lerp(water,stream, tracerCoverage*(.62+.26*grain));
    float4 left=BasinField(UV-float2(texel.x,0));
    float4 low=BasinField(UV-float2(0,texel.y));
    float streak=saturate(5*length(float2(dye-left.w,dye-low.w)));
    water+=streak*float3(.15,.28,.22);
    float3 shore=BankMaterial(p,coast);
    Color=lerp(shore,water,smoothstep(-.005,.006,coast));
    if(kind==0) Color=BoatPaint(Color,p);
    if(kind==4)
    {
        float2 q=p-float2(.10,-.09);
        float shaft=length(q);
        float rim=1-smoothstep(.005,.018,abs(shaft-.147));
        Color=lerp(Color,Color*.39,1-smoothstep(.122,.146,shaft));
        Color=lerp(Color,float3(.43,.42,.26),rim*.8);
    }
    Alpha=lerp(saturate(BankOpacity),1,smoothstep(-.005,.006,coast));
}
void TopDownSurface_half(half2 UV,half Profile,half BankOpacity,out half3 Color,out half Alpha)
{
    float3 rgb; float a; TopDownSurface_float(UV,Profile,BankOpacity,rgb,a); Color=rgb; Alpha=a;
}
#endif
