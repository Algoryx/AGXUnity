#include "./HashTableEntry.cginc"

// Buffer for the hash table
StructuredBuffer<VoxelEntry> hashTableBuffer;
StructuredBuffer<uint> hashTableOccupancy;

#include "./HashTableBase.cginc"
