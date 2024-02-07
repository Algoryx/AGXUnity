#ifndef FINE_PARTICLE_BUFFER
#define FINE_PARTICLE_BUFFER

struct FineParticle {
  float4 positionAndEase;
  float4 velocityAndMass;
};

StructuredBuffer<FineParticle> fineParticles;
  
void GetFineParticlePosition_float(float id,out float3 pos)
{
    pos = fineParticles[(uint)id].positionAndEase.xyz;
}

void GetFineParticleEase_float(float id, out float ease)
{
  ease = fineParticles[(uint)id].positionAndEase.w;
}

void GetIDColor_float(float id, out float3 color){
    if(id < 1.0)
        color = float3(1.0f,0.0f,0.0f);
    else if(id < 2.0)
        color = float3(0.0f,1.0f,0.0f);
    else if(id < 3.0)
        color = float3(0.0f,0.0f,1.0f);
    else
        color = float3(0.0f,0.0f,0.0f);
}

void setup(){
}

#endif
