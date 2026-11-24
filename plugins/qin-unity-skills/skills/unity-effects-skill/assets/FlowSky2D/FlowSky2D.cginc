#ifndef QIN_FLOW_SKY_2D_INCLUDED
#define QIN_FLOW_SKY_2D_INCLUDED

static const float QinFlow_TAU = 6.28318530718;
static const float3 QinFlow_indigo = float3(.025,.06,.23);
static const float3 QinFlow_deep = float3(.013,.12,.25);
static const float3 QinFlow_sea = float3(.015,.48,.58);
static const float3 QinFlow_ice = float3(.57,.97,1);
static const float3 QinFlow_mint = float3(.16,.97,.67);
static const float3 QinFlow_lilac = float3(.60,.48,1);
static const float3 QinFlow_gold = float3(1,.61,.19);
static const float3 QinFlow_pearl = float3(1,.94,.79);

// Paint in premultiplied alpha. Each layer can reveal the receiving scene.
void QinFlow_paint(inout float4 canvas, float3 rgb, float opacity)
{
    float a = saturate(opacity);
    canvas = canvas * (1-a) + float4(rgb*a,a);
}
float QinFlow_hash2(float2 p)
{
    float3 h = frac(p.xyx * float3(.1031,.1030,.0973));
    h += dot(h, h.yzx + 33.33);
    return frac((h.x+h.y)*h.z);
}
float QinFlow_noise2(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f*f*(3-2*f);
    return lerp(lerp(QinFlow_hash2(cell),QinFlow_hash2(cell+float2(1,0)),f.x),
                lerp(QinFlow_hash2(cell+float2(0,1)),QinFlow_hash2(cell+1),f.x),f.y);
}
float2 QinFlow_orbit(float angle) { return float2(cos(angle),sin(angle)); }
float QinFlow_softDisk(float d, float radius)
{
    return 1-smoothstep(radius-.004,radius+.006,d);
}
float QinFlow_lineMask(float d, float width) { return QinFlow_softDisk(abs(d),width); }
float QinFlow_halo(float d, float radius)
{
    float x=saturate(1-abs(d)/max(.001,radius));
    return x*x;
}
float QinFlow_tube(float2 p, float2 a, float2 b)
{
    float2 axis = b-a;
    float u=saturate(dot(p-a,axis)/max(dot(axis,axis),.00001));
    return length(p-a-u*axis);
}
float QinFlow_band(float x, float low, float high, float feather)
{
    return smoothstep(low-feather,low,x)*(1-smoothstep(high,high+feather,x));
}
void QinFlow_glint(inout float4 c, float2 v, float s, float3 hue, float strength)
{
    float r=length(v);
    float spikes=max(QinFlow_halo(abs(v.x)+abs(v.y)*.11,s*.15),
                     QinFlow_halo(abs(v.y)+abs(v.x)*.19,s*.16));
    QinFlow_paint(c,hue,(.55*QinFlow_halo(r,s*.84)+spikes+QinFlow_halo(r,s*.20))*strength);
    QinFlow_paint(c,QinFlow_pearl,QinFlow_halo(r,s*.10)*strength);
}
void QinFlow_filament(inout float4 c, float d, float w, float3 hue, float a)
{
    QinFlow_paint(c,hue,(.38*QinFlow_halo(d,w*4)+QinFlow_lineMask(d,w))*a);
}
float QinFlow_wrinkles(float2 p, float t)
{
    return sin(p.x*13+p.y*23-t*2+sin(p.x*8+t))*.5
         + sin(p.x*20-p.y*37-t*3)*.35 + sin(p.y*51+p.x*15+t)*.15;
}

void QinFlow_stream(inout float4 c,float2 p,float t)
{
    float axis=.17*sin(p.x*2.9+.25*sin(t))+p.x*.19;
    float width=.24+.055*cos(p.x*4-.35*cos(t));
    float y=(p.y-axis)/width;
    float edge=abs(y);
    QinFlow_paint(c,lerp(QinFlow_deep,QinFlow_sea,saturate(.40+.46*y+.13*sin(p.x*6-t))),.88*QinFlow_softDisk(edge,1));
    float shear=y*3.7+.30*sin(p.x*6.2-t*2)+.17*sin(p.x*12-t*3+y*4);
    float thread=abs(frac(shear)-.5);
    float patches=smoothstep(-.65,.7,sin(p.x*8-t*3+y*2));
    QinFlow_paint(c,QinFlow_ice,QinFlow_halo(thread,.11)*patches*QinFlow_softDisk(edge,.94)*.86);
    QinFlow_filament(c,edge-1,.021,QinFlow_mint,.75);
    float flecks=smoothstep(.63,.93,QinFlow_wrinkles(p,t));
    QinFlow_paint(c,QinFlow_pearl,flecks*QinFlow_softDisk(edge,.86)*.54);
}

void QinFlow_shore(inout float4 c,float2 p,float t)
{
    float wind=.085*sin(p.x*4-t)+.045*sin(9*p.x+t*2);
    float coast=p.y+.30*p.x-wind;
    float head=.12+.13*sin(t);
    float surf=head-coast;
    QinFlow_paint(c,lerp(QinFlow_sea,QinFlow_deep,saturate(surf)),smoothstep(-.012,.07,surf)*.87);
    float swell=frac(surf*2.0-t/QinFlow_TAU+.045*sin(p.x*9-t));
    QinFlow_paint(c,QinFlow_ice,QinFlow_halo(swell-.48,.11)*smoothstep(.04,.26,surf)*.65);
    float lace=QinFlow_noise2(float2(p.x*16,surf*45)+QinFlow_orbit(t)*.57);
    QinFlow_paint(c,QinFlow_ice,smoothstep(.29,.62,lace)*QinFlow_halo(surf-.074,.19)*.90);
    QinFlow_filament(c,surf,.013,QinFlow_pearl,.94);
}

void QinFlow_rings(inout float4 c,float2 p,float life,float extent,float3 hue,float strength)
{
    float d=length(p*float2(1,3.5));
    float front=extent*sqrt(saturate(life));
    QinFlow_filament(c,d-front,.009+.012*(1-life),hue,
             smoothstep(.0,.12,life)*(1-smoothstep(.52,1,life))*strength);
}
void QinFlow_cascade(inout float4 c,float2 p,float t)
{
    float y=saturate((p.y+.56)/1.40);
    float x=p.x-.12*sin(y*3)-.06;
    float w=.19+.11*(1-y)+.017*sin(y*18+t*2);
    float strands=.5+.5*sin(x*82+sin(p.y*11+t*3)+cos(x*47-t));
    float fall=QinFlow_band(p.y,-.60,.83,.075);
    QinFlow_paint(c,lerp(QinFlow_deep,QinFlow_ice,.18+.58*strands),fall*QinFlow_softDisk(abs(x),w)*.9);
    float dash=.5+.5*cos(p.y*23+t*4+x*39);
    QinFlow_paint(c,QinFlow_ice,pow(strands,5)*dash*fall*QinFlow_softDisk(abs(x),w)*.95);
    QinFlow_filament(c,abs(x)-w,.013,QinFlow_mint,fall*.70);
    float2 foot=p-float2(.06,-.60);
    QinFlow_paint(c,QinFlow_sea,.78*QinFlow_halo(length(foot*float2(1,4)),1.18));
    [loop] for(int i=0;i<3;i++)
        QinFlow_rings(c,foot,frac(t/QinFlow_TAU+i/3.0),1.16,QinFlow_ice,.92);
    [loop] for(int j=0;j<9;j++)
    {
        float age=frac(t/QinFlow_TAU+j*.38197);
        float angle=.36+j*2.40;
        float2 drop=float2(cos(angle)*age*.80,age*(1-age)*(.7+.65*abs(sin(angle))));
        QinFlow_paint(c,QinFlow_ice,QinFlow_softDisk(length(foot-drop),.013+.025*(1-age))*sin(age*3.14159));
    }
}

void QinFlow_cavernDrips(inout float4 c,float2 p,float t)
{
    float ceiling=.61+.07*sin(p.x*5)+.04*cos(p.x*10);
    QinFlow_paint(c,QinFlow_deep,QinFlow_band(p.y,ceiling,.78,.025)*.64);
    QinFlow_filament(c,p.y-ceiling,.012,QinFlow_sea,.80);
    [loop] for(int i=0;i<4;i++)
    {
        float x=-1.17+i*.72+.10*sin(i*1.9);
        float source=.61+.07*sin(x*5)+.04*cos(x*10);
        float age=frac(t/QinFlow_TAU+i*.382);
        float fall=saturate((age-.25)/.48);
        float y=lerp(source-.06,-.62,fall*fall);
        float2 v=p-float2(x,y);
        float bead=length(v*float2(1,lerp(.74,.50,fall)));
        float radius=.035+.016*smoothstep(0,.25,age);
        float wet=1-smoothstep(.70,.75,age);
        QinFlow_paint(c,QinFlow_sea,QinFlow_softDisk(bead,radius)*wet);
        QinFlow_filament(c,bead-radius,.006,QinFlow_ice,wet);
        QinFlow_paint(c,QinFlow_pearl,QinFlow_halo(length(v-float2(-.012,.017)),.019)*wet);
        QinFlow_rings(c,p-float2(x,-.63),saturate((age-.71)/.29),.37,QinFlow_ice,1);
    }
}

void QinFlow_rainyPool(inout float4 c,float2 p,float t)
{
    float2 basePoint=p-float2(0,-.45);
    float pool=length(basePoint*float2(.78,2.8));
    QinFlow_paint(c,QinFlow_deep,.65*QinFlow_softDisk(pool,1.15));
    QinFlow_paint(c,QinFlow_sea,.47*QinFlow_halo(pool-.85,.59));
    [loop] for(int i=0;i<14;i++)
    {
        float seed=QinFlow_hash2(float2(i,6));
        float age=frac(t/QinFlow_TAU*2+seed);
        float2 pos=float2((QinFlow_hash2(float2(i,7))-.5)*2.5,-.45+(seed-.5)*.42);
        float drop=saturate(age/.61);
        float2 a=pos+float2(.21,1.38)*(1-drop*drop);
        float visibility=sin(drop*3.14159);
        QinFlow_filament(c,QinFlow_tube(p,a,a+float2(.016,.14)),.006,QinFlow_ice,visibility*.84);
        QinFlow_rings(c,p-pos,saturate((age-.60)/.40),.34+.20*seed,QinFlow_mint,.96);
    }
}
void QinFlow_swirlPool(inout float4 c,float2 p,float t)
{
    float2 v=p*float2(.87,1.64);
    float r=length(v), angle=atan2(v.y,v.x);
    float rim=1+.025*sin(angle*5+t)+.02*sin(angle*8-t*2);
    float m=QinFlow_softDisk(r,rim);
    float spiral=angle*3+r*16-t*2+.32*sin(angle*4+t);
    float fold=.5+.5*cos(spiral);
    QinFlow_paint(c,lerp(QinFlow_deep,QinFlow_sea,fold*.77),m*.94);
    QinFlow_paint(c,QinFlow_ice,m*pow(fold,12)*smoothstep(.10,.36,r)*(.55+.35*sin(r*8-t)));
    QinFlow_filament(c,r-rim,.014,QinFlow_mint,.9);
    QinFlow_paint(c,QinFlow_indigo,QinFlow_halo(r,.26)*.95);
    QinFlow_filament(c,r-.13-.014*sin(angle*3-t),.012,QinFlow_ice,.7);
}
void QinFlow_flood(inout float4 c,float2 p,float t)
{
    float front=-.05-.44*cos(t)+.066*sin(p.x*6-t*2)+.022*cos(p.x*14-t*3);
    float below=front-p.y;
    float m=smoothstep(-.009,.015,below);
    QinFlow_paint(c,lerp(QinFlow_sea,QinFlow_deep,saturate(below*1.15)),m*.85);
    float caustic=abs(sin(p.x*9+sin(p.y*8+t))+sin(p.y*12-p.x*2-t*2));
    QinFlow_paint(c,QinFlow_mint,m*QinFlow_halo(caustic,.28)*(.18+.25*exp(-2*max(0,below))));
    QinFlow_filament(c,below,.017,QinFlow_ice,.95);
    [loop] for(int i=0;i<8;i++)
    {
        float life=frac(t/QinFlow_TAU+i*.38197);
        float2 pos=float2(-1.45+i*.41+.04*sin(t+i),-.97+life*1.36);
        float radius=.016+.025*QinFlow_hash2(float2(i,3));
        QinFlow_filament(c,length(p-pos)-radius,.006,QinFlow_ice,m*sin(life*3.14159)*.83);
    }
}
void QinFlow_wake(inout float4 c,float2 p,float t)
{
    float bow=.85+.26*sin(t);
    float x=bow-p.x;
    float spread=.08+.24*x;
    float y=p.y-.12*sin(t+p.x);
    float wedge=saturate(x/.25)*saturate((2.28-x)/.45);
    QinFlow_paint(c,QinFlow_sea,QinFlow_halo(y,spread+.10)*wedge*.66);
    float curl=.035*sin(x*10-t*3)+.019*sin(x*19+t);
    QinFlow_filament(c,abs(y)-spread-curl,.024,QinFlow_ice,wedge*.97);
    float crest=abs(frac(x*1.7-.75*abs(y)-t/QinFlow_TAU*2)-.5);
    QinFlow_paint(c,QinFlow_mint,QinFlow_halo(crest,.14)*QinFlow_halo(y,spread)*wedge*.8);
    QinFlow_glint(c,float2(p.x-bow,y),.20,QinFlow_ice,.77);
}
void QinFlow_dustStars(inout float4 c,float2 p,float t,float density,float motion,float brightness)
{
    float2 grid=(p+float2(sin(t),.36*cos(t))*motion)*density;
    float2 id=floor(grid);
    float seed=QinFlow_hash2(id);
    float2 center=.22+.56*float2(QinFlow_hash2(id+13),QinFlow_hash2(id+37));
    float2 q=(frac(grid)-center)/density;
    float s=(.065+.11*seed)/density;
    float blink=.63+.37*sin(t*(1+floor(seed*3))+seed*QinFlow_TAU);
    float3 hue=lerp(float3(.38,.51,1),QinFlow_pearl,seed);
    QinFlow_paint(c,hue,QinFlow_halo(length(q),s)*blink*brightness);
    if (seed>.81) QinFlow_glint(c,q,s*2.2,hue,blink*brightness);
}
void QinFlow_skyVeil(inout float4 c,float2 p,float t)
{
    float w=p.y-.16*sin(p.x*2.7+.2*cos(t))-.22*p.x;
    float cloud=QinFlow_noise2(p*3+QinFlow_orbit(t)*.13);
    QinFlow_paint(c,lerp(QinFlow_indigo,QinFlow_lilac,.23*cloud),QinFlow_halo(w,.74)*(.40+.44*cloud));
}
void QinFlow_starfield(inout float4 c,float2 p,float t)
{
    QinFlow_skyVeil(c,p,t);
    QinFlow_dustStars(c,p,t,11,.065,.72);
    QinFlow_dustStars(c,p+7,t,4.4,.24,1.0);
    QinFlow_dustStars(c,p+13,t,1.72,.47,1.0);
}
void QinFlow_comet(inout float4 c,float2 p,float2 head,float2 direction,float len,float3 hue,float a)
{
    float2 v=p-head;
    float along=-dot(v,direction);
    float across=dot(v,float2(-direction.y,direction.x));
    float u=saturate(along/len);
    float weight=QinFlow_band(along,0,len,.012)*(1-u);
    float taper=.009+.040*(1-u);
    QinFlow_paint(c,hue,QinFlow_halo(across,taper*3.0)*weight*a*.79);
    QinFlow_paint(c,QinFlow_ice,QinFlow_halo(across,taper*.62)*weight*a);
    QinFlow_glint(c,v,.25,hue,a);
}
void QinFlow_shootingStars(inout float4 c,float2 p,float t)
{
    QinFlow_skyVeil(c,p,t);
    QinFlow_dustStars(c,p+2,t,6,.018,.65);
    [loop] for(int i=0;i<3;i++)
    {
        float age=frac(t/QinFlow_TAU+i*.332);
        float track=age*age*(3-2*age);
        float2 head=lerp(float2(1.56,.78-.12*i),float2(-1.34,-.30-.15*i),track);
        QinFlow_comet(c,p,head,normalize(float2(-2.9,-1.08-.03*i)),.70+.16*i,
              lerp(QinFlow_ice,QinFlow_gold,i*.37),sin(age*3.14159));
    }
}

float2 QinFlow_constellationPoint(int i)
{
    if (i==0) return float2(-1.16,-.33);
    if (i==1) return float2(-.73,.09);
    if (i==2) return float2(-.38,.01);
    if (i==3) return float2(.03,.43);
    if (i==4) return float2(.60,.33);
    if (i==5) return float2(.83,-.16);
    return float2(.23,-.28);
}
void QinFlow_constellation(inout float4 c,float2 p,float t)
{
    QinFlow_skyVeil(c,p,t);
    QinFlow_dustStars(c,p-4,t,9,.03,.49);
    [loop] for(int i=0;i<7;i++)
    {
        float2 a=QinFlow_constellationPoint(i);
        float2 b=QinFlow_constellationPoint(i==6?3:i+1);
        float phase=frac(t/QinFlow_TAU-i/7.0);
        float pulse=.58+.42*cos(phase*QinFlow_TAU);
        QinFlow_filament(c,QinFlow_tube(p,a,b),.006,QinFlow_ice,.52+.36*pulse);
        float2 travelling=lerp(a,b,smoothstep(0,1,phase));
        QinFlow_glint(c,p-travelling,.074,QinFlow_mint,sin(phase*3.14159)*.82);
        QinFlow_glint(c,p-a,.20+.075*pulse,lerp(QinFlow_ice,QinFlow_gold,i/8.0),1.0);
    }
}
void QinFlow_aurora(inout float4 c,float2 p,float t)
{
    QinFlow_dustStars(c,p,t,7,.025,.62);
    [loop] for(int i=0;i<3;i++)
    {
        float x=p.x+i*.32;
        float fold=.19*sin(x*2.6+.47*sin(t+i))+.10*sin(x*5.3-t)-.26+i*.14+.15*x;
        float y=p.y-fold;
        float filamentWave=.5+.5*sin(x*39+3*sin(x*6+t)+t*2);
        float height=.53+.21*sin(x*4-t+i);
        float skirt=QinFlow_band(y,0,height,.05)*(1-smoothstep(0,height+.09,y));
        float3 hue=lerp(QinFlow_mint,QinFlow_lilac,saturate(y/height+i*.21));
        QinFlow_paint(c,hue,skirt*(.28+.43*filamentWave));
        QinFlow_filament(c,y,.011,lerp(QinFlow_mint,QinFlow_ice,i*.40),.60);
    }
}
void QinFlow_galaxy(inout float4 c,float2 p,float t)
{
    QinFlow_dustStars(c,p,t,7,.025,.52);
    float2 v=float2(p.x+.25*p.y,(p.y-.22*p.x)*1.70);
    float r=length(v), angle=atan2(v.y,v.x);
    float ripple=sin(3*angle-12*sqrt(max(.0001,r))+t+.45*sin(angle*5+r*8-t));
    float gas=QinFlow_noise2(v*7+QinFlow_orbit(t)*.16);
    float arm=pow(saturate(.49+.51*ripple),2.4)*QinFlow_halo(r-.43,1.0);
    QinFlow_paint(c,QinFlow_lilac,.26*QinFlow_halo(r,1.38));
    QinFlow_paint(c,lerp(float3(.29,.17,.76),float3(.48,.70,1),gas),arm*(.38+.79*gas));
    float beads=QinFlow_noise2(v*40+QinFlow_orbit(t)*.25);
    QinFlow_paint(c,QinFlow_pearl,smoothstep(.54,.84,beads)*arm*.90);
    QinFlow_paint(c,QinFlow_gold,QinFlow_halo(r,.35)*.95);
    QinFlow_glint(c,v,.33,QinFlow_pearl,.95);
}
void QinFlow_nebula(inout float4 c,float2 p,float t)
{
    float2 drift=QinFlow_orbit(t)*.17;
    float n=QinFlow_noise2(p*2.5+drift);
    float cloud=QinFlow_noise2(p*6.2+float2(n*2,-n*2)-drift);
    float fine=QinFlow_noise2(p*17-float2(cloud,n)*2+drift);
    float lane=p.y-.22*sin(p.x*2.1)-.32*p.x;
    float shape=QinFlow_halo(lane+.64*(n-.50),.76);
    float glow=shape*smoothstep(.20,.76,.58*cloud+.42*fine);
    QinFlow_paint(c,lerp(QinFlow_indigo,float3(.39,.09,.55),n),shape*.80);
    QinFlow_paint(c,lerp(float3(.85,.22,.41),float3(.15,.67,.99),smoothstep(-.62,.60,p.x+.48*cloud)),
          glow*.87);
    float rim=QinFlow_halo(cloud-.57,.12)*shape;
    QinFlow_paint(c,QinFlow_pearl,rim*(.16+.28*fine));
    QinFlow_dustStars(c,p+8,t,5.8,.045,.89);
}
void QinFlow_eclipseCorona(inout float4 c,float2 p,float t)
{
    QinFlow_dustStars(c,p,t,6,.035,.45);
    float2 v=p-float2(.09*sin(t),.03*cos(t));
    float r=length(v), angle=atan2(v.y,v.x);
    float lace=.5+.5*sin(17*angle+1.4*sin(angle*7-t)+t);
    float tongue=.08+.18*pow(lace,2)+.11*pow(.5+.5*cos(3*angle-t),4);
    float edge=r-.44;
    QinFlow_paint(c,float3(.91,.21,.065),QinFlow_halo(edge,tongue+.17)*.87);
    QinFlow_paint(c,QinFlow_gold,QinFlow_halo(edge,tongue*.77)*.98);
    QinFlow_filament(c,edge,.017,QinFlow_pearl,.98);
    QinFlow_paint(c,float3(.012,.013,.046),QinFlow_softDisk(r,.425)*.97);
    QinFlow_glint(c,v-.445*QinFlow_orbit(t),.31,QinFlow_pearl,.95);
}
void QinFlow_starShower(inout float4 c,float2 p,float t)
{
    QinFlow_dustStars(c,p,t,5.5,.05,.75);
    [loop] for(int i=0;i<14;i++)
    {
        float seed=QinFlow_hash2(float2(i,31));
        float age=frac(t/QinFlow_TAU*(1+floor(seed*2))+seed);
        float2 location=float2(lerp(-1.30,1.25,QinFlow_hash2(float2(i,61))),.87);
        location+=float2(-.41*age+.12*sin(age*3.14159),-1.65*age*age);
        float visibility=smoothstep(0,.13,age)*(1-smoothstep(.78,1,age));
        float2 dir=normalize(float2(-.41+.377*cos(age*3.14159),-3.30*age));
        QinFlow_comet(c,p,location,dir,.10+.22*age,lerp(QinFlow_gold,QinFlow_lilac,seed),visibility*(.65+.28*seed));
    }
}
float QinFlow_lifetime(float p)
{
    return smoothstep(0,.08,p)*(1-smoothstep(.84,1,p));
}
void QinFlow_burst(inout float4 c,float2 p,float life,float radius,float3 hue)
{
    float ease=1-(1-life)*(1-life);
    float strength=sin(life*3.14159);
    [loop] for(int i=0;i<11;i++)
    {
        float seed=QinFlow_hash2(float2(i,27));
        float2 tangent=QinFlow_orbit(i*2.39996+.19*seed);
        float2 travel=tangent*(ease*radius*(.45+.52*seed));
        travel.y-=life*life*.25;
        float d=QinFlow_tube(p,travel,travel-tangent*.080*(1-life));
        QinFlow_filament(c,d,.007+.015*(1-life),hue,strength);
    }
}
void QinFlow_waterBolt(inout float4 c,float2 p,float phase)
{
    float shot=smoothstep(.22,.70,phase);
    float2 origin=float2(-1.09,-.19);
    float2 tip=lerp(origin,float2(.98,.12),shot);
    tip.y+=.25*sin(shot*3.14159);
    float radius=.09+.035*sin(phase*21);
    float aim=(1-smoothstep(.24,.38,phase));
    QinFlow_filament(c,length((p-origin)*float2(1.32,1))-radius*2,.014,QinFlow_ice,aim);
    float delta=tip.x-p.x;
    float tail=.68*sin(shot*3.14159)+.20;
    float u=saturate(delta/tail);
    float center=tip.y-.075*u+.090*sin(u*8+phase*13)*u;
    float width=radius*(1-u)*(.83+.16*sin(u*12-phase*16));
    float visible=QinFlow_band(delta,0,tail,.024)*(1-smoothstep(.64,.80,phase));
    QinFlow_paint(c,QinFlow_sea,QinFlow_softDisk(abs(p.y-center),width)*visible);
    QinFlow_filament(c,abs(p.y-center)-width,.013,QinFlow_ice,visible);
    float core=length((p-tip)*float2(.86,1.5));
    QinFlow_paint(c,QinFlow_ice,QinFlow_softDisk(core,radius)*(1-smoothstep(.65,.79,phase)));
    float impact=smoothstep(.64,.98,phase);
    QinFlow_burst(c,p-float2(.98,.12),impact,.72,QinFlow_ice);
    QinFlow_rings(c,p-float2(.98,.08),impact,.56,QinFlow_mint,.88);
}
void QinFlow_tidalSlash(inout float4 c,float2 p,float phase)
{
    float2 v=float2(p.x*1.03,p.y*1.50);
    float r=length(v);
    float a=frac((atan2(v.y,v.x)+2.63)/QinFlow_TAU);
    float reach=smoothstep(.09,.63,phase)*.77;
    float cut=smoothstep(.58,1,phase)*.77;
    float arc=QinFlow_band(a,cut,reach,.017)*smoothstep(.015,.16,reach);
    float taper=saturate((reach-a)*12)*saturate((a-cut)*8);
    float rad=.91-.12*sin(a*QinFlow_TAU)+.027*sin(a*39-phase*15);
    float thickness=.030+.095*taper;
    QinFlow_paint(c,lerp(QinFlow_deep,QinFlow_sea,saturate((r-rad)/thickness+.65)),QinFlow_softDisk(abs(r-rad),thickness)*arc);
    QinFlow_filament(c,r-rad-thickness,.015,QinFlow_ice,arc);
    QinFlow_filament(c,r-rad+.045*sin(a*51-phase*18),.010,QinFlow_mint,arc*taper);
    float2 tip=QinFlow_orbit(reach*QinFlow_TAU-2.63)*(.91-.12*sin(reach*QinFlow_TAU));
    QinFlow_glint(c,v-tip,.23,QinFlow_ice,arc+.42*sin(phase*3.14159));
}
void QinFlow_geyser(inout float4 c,float2 p,float phase)
{
    float up=smoothstep(.16,.49,phase);
    float retreat=smoothstep(.61,.97,phase);
    float base=-.58;
    float2 foot=p-float2(0,base);
    float strength=up*(1-retreat);
    float height=1.30*strength;
    float u=saturate(foot.y/max(.012,height));
    float lift=smoothstep(0,.16,height);

    // The issuing sheet flares into its source pool and forks into spray at its lip.
    float axis=.023*sin(u*10-phase*18)*u;
    float radius=.092+.22*pow(1-u,4)+.04*sin(u*8+phase*13)*u;
    float crest=.91+.07*sin(foot.x*38+phase*6);
    float wet=QinFlow_band(foot.y,-.015,height*crest,.045)*lift;
    float surface=abs(foot.x-axis)-radius;
    float streak=.5+.5*sin(foot.x*69+u*6+sin(u*14-phase*21));
    QinFlow_paint(c,lerp(QinFlow_sea,QinFlow_ice,.24+.46*streak),
                  QinFlow_softDisk(surface,0)*wet*.92);
    QinFlow_filament(c,surface,.011,QinFlow_ice,wet*.81);
    [loop] for(int i=0;i<4;i++)
    {
        float side=i<2?-1:1;
        float lane=i==0 || i==3?1:.57;
        float fork=side*(.046+.37*u*u*u)*lane+.024*sin(u*11+phase*9+i)*u;
        float tip=height*(.76+.17*lane+.038*sin(i+phase*9));
        float spray=QinFlow_band(foot.y,.028,tip,.039)*lift;
        float width=(.032+.026*(1-u))*(1-smoothstep(tip-.14,tip+.024,foot.y));
        QinFlow_filament(c,foot.x-fork,width,lerp(QinFlow_ice,QinFlow_mint,.32*lane),spray*.92);
    }
    float shoe=length(foot*float2(1,3.4));
    QinFlow_paint(c,QinFlow_sea,QinFlow_halo(shoe,.73)*lift*.76);
    float foam=.5+.5*sin(foot.x*44-phase*19+sin(foot.x*17+phase*12));
    QinFlow_paint(c,QinFlow_ice,QinFlow_halo(shoe-.26,.15)*lift*(.38+.48*foam));
    QinFlow_rings(c,foot,saturate(phase/.66),1.03,QinFlow_mint,1);
    QinFlow_burst(c,p-float2(0,.53),smoothstep(.39,.93,phase),.90,QinFlow_ice);
    QinFlow_rings(c,foot,smoothstep(.46,1,phase),1.27,QinFlow_ice,1);
}
void QinFlow_shield(inout float4 c,float2 p,float phase)
{
    float built=smoothstep(.03,.37,phase);
    float retract=smoothstep(.68,.98,phase);
    float2 v=p*float2(.94,1.0);
    float r=length(v), angle=atan2(v.y,v.x);
    float radius=.64*(.26+.74*built)*(1-.74*retract);
    float ripple=.023*sin(angle*6-phase*21)+.013*cos(angle*11+phase*17);
    float d=r-radius-ripple;
    float rim=saturate((r/max(.08,radius))*(r/max(.08,radius)));
    QinFlow_paint(c,lerp(QinFlow_deep,QinFlow_sea,rim),QinFlow_softDisk(d,0)*(.15+.53*rim));
    QinFlow_filament(c,d,.025,QinFlow_ice,.94);
    float caustic=sin(v.x*14+v.y*6+phase*14)+.56*sin(v.y*16-v.x*3-phase*10);
    QinFlow_paint(c,QinFlow_mint,QinFlow_halo(caustic,.24)*QinFlow_softDisk(d,-.018)*(.14+.39*rim));
    float2 highlight=radius*QinFlow_orbit(phase*QinFlow_TAU*1.7+1.8);
    QinFlow_glint(c,v-highlight,.23,QinFlow_ice,1);
    float2 hit=v-radius*QinFlow_orbit(.32);
    QinFlow_burst(c,hit,smoothstep(.46,.76,phase),.42,QinFlow_mint);
}
void QinFlow_healingSpring(inout float4 c,float2 p,float phase)
{
    float base=-.52;
    float grown=smoothstep(.01,.32,phase);
    QinFlow_rings(c,p-float2(0,base),saturate(phase/.76),1.03,QinFlow_mint,1.0);
    float u=saturate((p.y-base)/1.22);
    float height=grown*(1-smoothstep(.74,1,phase));
    float visible=QinFlow_band(p.y,base,base+1.22*height,.042);
    float helix=(.12+.25*u)*sin(u*10-phase*15);
    QinFlow_paint(c,QinFlow_mint,QinFlow_halo(p.x,(.44-.16*u))*visible*.25);
    QinFlow_filament(c,p.x-helix,.024,QinFlow_mint,visible);
    QinFlow_filament(c,p.x+helix,.014,QinFlow_ice,visible*.77);
    [loop] for(int i=0;i<8;i++)
    {
        float age=frac(phase*1.7+i*.38197);
        float2 pos=float2((QinFlow_hash2(float2(i,19))-.5)*1.42+.05*sin(age*8+i),base+age*1.29);
        QinFlow_glint(c,p-pos,.11+.07*age,lerp(QinFlow_mint,QinFlow_gold,i*.10),sin(age*3.14159)*grown);
    }
}
void QinFlow_starLance(inout float4 c,float2 p,float phase)
{
    float2 start=float2(-.96,-.32);
    float2 dir=normalize(float2(2.12,.67));
    float2 v=p-start;
    float x=dot(v,dir),y=dot(v,float2(-dir.y,dir.x));
    float charge=(1-smoothstep(.35,.49,phase));
    float radius=lerp(.27,.069,smoothstep(0,.35,phase));
    QinFlow_filament(c,length(v)-radius,.010,QinFlow_lilac,charge*.90);
    QinFlow_glint(c,v,.22+.28*charge,QinFlow_gold,charge);
    float front=2.20*smoothstep(.27,.57,phase);
    float back=2.20*smoothstep(.54,.78,phase);
    float span=max(.002,front-back);
    float u=saturate((x-back)/span);
    float remaining=saturate((1-u)/.26);
    float tipShape=min(saturate(u/.74),remaining);
    float thick=.106*saturate(span*4)*tipShape;
    float beam=QinFlow_band(x,back,front,.017)*smoothstep(0,.15,span);
    QinFlow_paint(c,QinFlow_lilac,QinFlow_halo(y,thick*2+.04)*beam*.69);
    QinFlow_paint(c,QinFlow_gold,QinFlow_softDisk(abs(y),thick)*beam);
    QinFlow_filament(c,y,.009,QinFlow_pearl,beam*saturate(tipShape*5));
    QinFlow_filament(c,abs(y)-thick,.007,QinFlow_pearl,beam*smoothstep(.58,.81,u));
    float firing=smoothstep(.18,.35,phase)*(1-smoothstep(.56,.75,phase));
    QinFlow_glint(c,v-dir*front,.20,QinFlow_gold,firing);
    QinFlow_burst(c,v-dir*2.20,smoothstep(.55,.97,phase),.78,QinFlow_gold);
}
void QinFlow_gravityWell(inout float4 c,float2 p,float phase)
{
    float grown=smoothstep(.03,.43,phase)*(1-smoothstep(.72,.99,phase));
    float2 v=float2(p.x+.18*p.y,p.y*1.32-.24*p.x);
    float r=length(v),angle=atan2(v.y,v.x);
    float rad=.31*grown+.04;
    float swirl=.5+.5*cos(3*angle+10*r-phase*21);
    float gas=QinFlow_halo(r-rad,.85*grown+.06)*swirl*swirl;
    QinFlow_paint(c,QinFlow_lilac,gas*.84);
    [loop] for(int i=0;i<8;i++)
    {
        float life=frac(phase+i*.125);
        float2 mote=QinFlow_orbit(i*2.39996+life*3.6)*(rad+(1-life)*(1-life)*1.14);
        QinFlow_glint(c,v-mote,.12,QinFlow_gold,grown*sin(life*3.14159));
    }
    QinFlow_paint(c,float3(.009,.006,.028),QinFlow_softDisk(r,rad)*.98);
    QinFlow_filament(c,r-rad,.020,QinFlow_lilac,1);
    QinFlow_filament(c,r-rad-.03,.009,QinFlow_gold,.82);
}
void QinFlow_meteorImpact(inout float4 c,float2 p,float phase)
{
    float2 site=float2(.48,-.44);
    float arrival=smoothstep(.09,.55,phase);
    float2 head=lerp(float2(-1.15,.78),site,arrival);
    float fly=1-smoothstep(.52,.60,phase);
    QinFlow_comet(c,p,head,normalize(float2(1.63,-1.22)),.68,QinFlow_gold,fly);
    float2 v=(p-head)*float2(1,1.12);
    QinFlow_paint(c,float3(.90,.28,.09),QinFlow_softDisk(length(v),.084)*fly);
    QinFlow_glint(c,v,.16,QinFlow_gold,fly);
    float life=smoothstep(.50,.98,phase);
    float flash=sin(life*3.14159);
    QinFlow_paint(c,QinFlow_gold,QinFlow_halo(length(p-site),.24+.29*life)*flash*.73);
    QinFlow_burst(c,p-site,life,1.05,QinFlow_gold);
    QinFlow_rings(c,p-site,life,1.14,QinFlow_pearl,1);
}

void FlowSky2D_float(float2 UV,float Case,float Phase,out float3 Color,out float Alpha)
{
    float2 p=(UV*2-1)*float2(1.77778,1);
    float phase=saturate(Phase);
    float t=phase*QinFlow_TAU;
    float4 c=0;
    int id=(int)floor(Case+.5);
    [branch] if (id<8)
    {
        [branch] if (id==0) QinFlow_stream(c,p,t);
        else if (id==1) QinFlow_shore(c,p,t);
        else if (id==2) QinFlow_cascade(c,p,t);
        else if (id==3) QinFlow_cavernDrips(c,p,t);
        else if (id==4) QinFlow_rainyPool(c,p,t);
        else if (id==5) QinFlow_swirlPool(c,p,t);
        else if (id==6) QinFlow_flood(c,p,t);
        else QinFlow_wake(c,p,t);
    }
    else if (id<16)
    {
        [branch] if (id==8) QinFlow_starfield(c,p,t);
        else if (id==9) QinFlow_shootingStars(c,p,t);
        else if (id==10) QinFlow_constellation(c,p,t);
        else if (id==11) QinFlow_aurora(c,p,t);
        else if (id==12) QinFlow_galaxy(c,p,t);
        else if (id==13) QinFlow_nebula(c,p,t);
        else if (id==14) QinFlow_eclipseCorona(c,p,t);
        else QinFlow_starShower(c,p,t);
    }
    else
    {
        [branch] if (id==16) QinFlow_waterBolt(c,p,phase);
        else if (id==17) QinFlow_tidalSlash(c,p,phase);
        else if (id==18) QinFlow_geyser(c,p,phase);
        else if (id==19) QinFlow_shield(c,p,phase);
        else if (id==20) QinFlow_healingSpring(c,p,phase);
        else if (id==21) QinFlow_starLance(c,p,phase);
        else if (id==22) QinFlow_gravityWell(c,p,phase);
        else QinFlow_meteorImpact(c,p,phase);
        c*=QinFlow_lifetime(phase);
    }
    float2 border=min(UV,1-UV);
    float fade=smoothstep(0,.048,border.x)*smoothstep(0,.048,border.y);
    Color=c.rgb/max(c.a,.000001);
    Alpha=c.a*fade;
}

#endif
