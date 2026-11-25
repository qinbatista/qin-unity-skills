Shader "Qin/Water/Side Rain"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RainVertex
            #pragma fragment RainFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "WaterStudyOptics.hlsl"

            struct Parcel { float4 placement; float4 motion; };
            StructuredBuffer<Parcel> _RainParcels;
            Texture2D<float4> _RainContacts;
            Texture2D<float2> _RainWaterline;
            SamplerState sampler_RainWaterline;
            float4 _RainClock;

            float ProjectedSurface(float x)
            {
                float u = saturate(x / 6. + .5);
                float height = _RainWaterline.SampleLevel(sampler_RainWaterline,float2(u,.5),0).r;
                return RainSectionHeight(u,height);
            }

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float4 effect : TEXCOORD1; // mode, age, variation, visibility
            };

            Varyings RainVertex(float4 coordinates : TEXCOORD0)
            {
                Varyings output;
                uint id = (uint)coordinates.z;
                Parcel parcel = _RainParcels[id];
                float4 contact = _RainContacts.Load(int3(id,1,0));
                float variation = frac((id + .24) * .618034 + 9.3*max(0.,parcel.placement.w));
                float2 uv = coordinates.xy;
                float age = _RainClock.x - contact.z;
                float visibility = 0;
                float2 position = parcel.placement.xy;
                if (coordinates.w < .5)
                {
                    // The tip is the current parcel. The tail spans one shutter interval
                    // along its measured velocity and cannot precede its emission time.
                    float life = max(0.,_RainClock.x - parcel.placement.w);
                    float2 velocity = parcel.motion.xy;
                    float speed = max(.001,length(velocity));
                    float2 axis = velocity / speed;
                    float2 lateral = float2(-axis.y,axis.x);
                    float halfWidth = lerp(.006,.0135,variation) * lerp(1.,.86,saturate(speed/6.));
                    float exposure = min(life,_RainClock.y);
                    float lengthM = speed * exposure + 2*halfWidth;
                    position.y += (ProjectedSurface(position.x)-.04) * (1.-smoothstep(.055,.28,position.y));
                    position += axis * (halfWidth - .5*(1-uv.y)*lengthM) + lateral * uv.x * halfWidth*1.55;
                    visibility = parcel.placement.w >= 0 && contact.w == 0 && velocity.y < -.10
                        && parcel.placement.y > .048 ? smoothstep(0.,.055,life)*(.55+.45*variation) : 0.;
                }
                else
                {
                    // Anchored to the same water-plane intersection that deposited momentum.
                    position = float2(contact.x,ProjectedSurface(contact.x)) + float2(uv.x*.235,(uv.y+1.)*.115-.018);
                    visibility = contact.w > 0 && age >= 0 && age < .39
                        ? saturate((contact.w-1.)*.23)*_RainClock.w : 0.;
                }
                output.position = visibility > 0. ? TransformObjectToHClip(float3(position,0))
                                                  : float4(2,2,0,1);
                output.uv = uv;
                output.effect = float4(coordinates.w,age,variation,visibility*_RainClock.z);
                return output;
            }

            float Line(float distanceM, float radiusM)
            {
                float filter = max(.0008,.66*fwidth(distanceM));
                return 1.-smoothstep(radiusM-filter,radiusM+filter,distanceM);
            }

            half4 RainFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float variation = input.effect.z;
                if (input.effect.x < .5)
                {
                    float forward = .5+.5*uv.y;
                    float taper = lerp(.050,.76,sqrt(saturate(forward)));
                    float nose = smoothstep(0,.13,1-forward);
                    float coreWidth = taper * (.15+.85*sqrt(nose));
                    float cover = Line(abs(uv.x),coreWidth);
                    float shine = Line(abs(uv.x + coreWidth*.22),coreWidth*.38);
                    float3 glass = lerp(float3(.18,.46,.56),float3(.74,.89,.91),(.50+.48*shine)*forward);
                    float alpha = cover*input.effect.w*smoothstep(0,.18,forward)*(.35+.62*forward);
                    return half4(glass,alpha);
                }

                float age = input.effect.y;
                float2 p = float2(uv.x*.235,(uv.y+1.)*.115-.018);
                float signX = p.x < 0 ? -1. : 1.;
                float fullness = variation*variation;
                float energy = input.effect.w * (.32+.68*fullness);
                float side = frac(variation+signX*.239);
                float strongSide = signX * (variation < .39 ? -1. : 1.) > 0. ? 1. : 0.;
                float sideWeight = lerp(.14+.51*fullness,1.,strongSide);
                float life = lerp(.090,.175,fullness) * (.84+.17*side);
                float t = saturate(age/life);
                float reach = (.038+.123*fullness) * (.68+.39*side+.22*strongSide);
                float x = abs(p.x);
                float u = x / max(.006,reach*sqrt(t));
                float lift = (.014+.069*fullness) * sin(3.14159*t) * (.63+.57*side);
                // A near lip starts attached and spreads outward, then breaks at the tip.
                float lipHeight = lift * saturate(u) * (.56+.58*saturate(u));
                float lip = Line(abs(p.y-lipHeight),lerp(.008,.003,saturate(u)))
                          * smoothstep(.02,.21,u) * (1.-smoothstep(.78,1.05,u))
                          * smoothstep(0.,.021,age)*(1.-smoothstep(.55,1.,t))*sideWeight;
                float spray = 0.;
                [unroll] for (int i=0; i<3; i++)
                {
                    float key = frac(variation + .382*i);
                    float born = .016+.016*i;
                    float flight = age-born;
                    float direction = i == 1 ? -1. : 1.;
                    if (variation < .39) direction *= -1.;
                    float2 v = float2(direction*(.18+.43*key),.49+(.41+.83*key)*sqrt(fullness));
                    float2 bead = float2(direction*reach*.37,lift*.28) + v*flight - float2(0,4.905*flight*flight);
                    float2 delta = p-bead;
                    float radius = .004+.005*key*fullness;
                    // Weak glancing hits shed at most one small drop. Larger crowns shed more.
                    float detached = smoothstep(.18+.29*i,.32+.29*i,variation);
                    float2 currentVelocity = v-float2(0,9.81*flight);
                    float2 axis = currentVelocity/max(.001,length(currentVelocity));
                    float2 stretched = delta-axis*dot(delta,axis)*.33;
                    if (flight > 0 && bead.y > .003)
                        spray = max(spray,detached*Line(length(stretched),radius)*saturate(bead.y/.018));
                }
                float alpha = max(lip*.84,spray*.85)*energy;
                return half4(lerp(float3(.44,.72,.77),float3(.74,.88,.88),spray),alpha);
            }
            ENDHLSL
        }
    }
}
