#ifndef QIN_TOP_DOWN_DOMAIN
#define QIN_TOP_DOWN_DOMAIN

static const float2 BasinSize = float2(8.0, 4.5);
float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 v = b-a;
    return length(p-a-v*saturate(dot(p-a,v)/dot(v,v)));
}

// Positive inside the wet domain. Distances are measured in local metres.
float WaterBoundary(float2 p, int profile)
{
    float distanceToBank = 0;
    if (profile == 1)
    {
        float bank = 1.49 - abs(p.y-.10*sin(1.5*p.x)) + .09*sin(2.1*p.x+.9);
        float2 q = p-float2(.14,.02);
        float island = (length(float2(q.x+.16*q.y,1.18*q.y))-.60) + .040*sin(8*q.y+3*q.x);
        distanceToBank = min(bank,island);
    }
    else if (profile == 2 || profile == 3)
    {
        float2 q = float2(profile == 2 ? -p.x : p.x,p.y);
        float2 fork = float2(-.55,.04);
        float lower = .52-SegmentDistance(q,fork,float2(4.8,-1.71));
        float upper = .60-SegmentDistance(q,fork,float2(4.8,1.80));
        float trunk = .82-SegmentDistance(q,float2(-5,.05),fork);
        distanceToBank = max(trunk,max(lower,upper))+.055*sin(2.3*p.x+2.7*p.y);
    }
    else
    {
        float2 q = abs(p/BasinSize)*2;
        float shelf = (1.0-length(pow(q,float2(4,4))))*.40;
        distanceToBank = shelf + .08*sin(2.4*p.x+.6*p.y)+.04*sin(3.6*p.y-1.2*p.x);
    }
    return distanceToBank;
}

#endif
