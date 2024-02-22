#define NAN asfloat(0xffffffff)
#define GRP_SIZE_VOXELS 128
#define GRP_SIZE_PARTICLES 1024
#define EXTRA_PARTICLES_IN_VOXEL 0
#define EASE(p) ((p).positionAndEase.w)
//#define EASE(p) ((p).w)
#define VELOCITY(p) ((p).velocityAndMass.xyz)
#define INDEX(p) ((p).index.xyz)
#define MASS(p) ((p).velocityAndMass.w)
#define VOXEL_POSITION(p) ((p).position.xyz)
#define FP_POSITION(p) ((p).positionAndEase.xyz)
//#define FP_POSITION(p) ((p).xyz)
#define CP_POSITION(p) ((p).positionAndRadius.xyz)
#define RADIUS(p) ((p).positionAndRadius.w)
#define RAND(p) ((p).velocityAndMass.w)

float distance2(float3 pt1, float3 pt2)
{
    float3 v = pt1 - pt2;
    return dot(v, v);
}

int3 getVoxelIndexFromWorldPosition(float3 position, float voxelSize)
{
    position /= voxelSize;
    position += sign(position) * 0.5;
    
    return int3((int) position.x, (int) position.y, (int) position.z);
}

//#define FineParticle float4
struct FineParticle
{
    float4 positionAndEase; // xyz is position, w is particle ease
    float4 velocityAndMass;
};

struct CoarseParticle
{
    float4 positionAndRadius; // xyz is position, w is particle radius
    float4 velocityAndMass; // xyz is velocity, w is mass
    //float4 voxelIndex; // xyz is voxel index, w is padding
};

RWStructuredBuffer<int> dispatchArgs;
#define NUM_PARTICLE_THREAD_GROUPS (dispatchArgs[0])

RWStructuredBuffer<uint> drawCallArgs;
#define NUM_FINE_PARTICLES (drawCallArgs[1])
#define NEW_NUM_FINE_PARTICLES (drawCallArgs[5])