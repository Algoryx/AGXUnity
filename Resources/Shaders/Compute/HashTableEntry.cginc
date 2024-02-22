// Hash table entry structure
struct VoxelEntry
{
    int4 indexAndRoom; //xyz is index, w is amount of particles that fit in the voxel
    float4 positionAndMass; //xyz is position, w is original mass
    float4 velocity; //xyz is velocity, w is padding
    // Spawning bounds use an inner bound which adheres to voxel edges and shrinks when not in contact with an edge
    // This inner bound is used to produce bigger bounds with rounded corners in "SpawnParticles"
    // The real bounds are also saved as the original rectangles
    float4 minBound; // xyz is min corner of the total spawning bound, w is padding
    float4 maxBound; // xyz is max corner of the inner spawning bound, w is padding
    float4 innerMinBound; // xyz is min corner of the inner spawning bound, w is padding
    float4 innerMaxBound; // xyz is max corner of the inner spawning bound, w is padding
};
