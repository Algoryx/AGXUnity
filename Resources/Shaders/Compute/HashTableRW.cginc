#include "./HashTableEntry.cginc"

// Buffer for the hash table
RWStructuredBuffer<VoxelEntry> hashTableBuffer;
RWStructuredBuffer<uint> hashTableOccupancy;

#include "./HashTableBase.cginc"

#define ZEROING_GRP_SIZE 128

void InsertIndex(int3 index, int room, float3 position, float originalMass, float3 velocity, float3 minBound, float3 maxBound, float3 innerMinBound, float3 innerMaxBound)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    [allow_uav_condition]
    for (int i = 0; i < (int) tableSize; i++)
    {
        uint prev;
        InterlockedCompareExchange(HASH_OCCUPANCY(hashValue), EMPTY, TAKEN, prev);
        if (prev == EMPTY)
        {
            INDEX_AT_HASH(hashValue) = index;
            ROOM_AT_HASH(hashValue) = room;
            POSITION_AT_HASH(hashValue) = position;
            ORIGINAL_MASS_AT_HASH(hashValue) = originalMass;
            VELOCITY_AT_HASH(hashValue) = velocity;
            MINBOUND_AT_HASH(hashValue) = minBound;
            MAXBOUND_AT_HASH(hashValue) = maxBound;
            INNER_MINBOUND_AT_HASH(hashValue) = innerMinBound;
            INNER_MAXBOUND_AT_HASH(hashValue) = innerMaxBound;
            return;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
}

void DeleteIndex(int3 index)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return;
        }
        if (all(INDEX_AT_HASH(hashValue) == index))
        {
            HASH_OCCUPANCY(hashValue) = EMPTY;
            return;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
}

void AddToRoomAtIndex(int3 index, int amount, out int result)
{
    uint hashValue = ComputeHash(index);

    result = UNDEFINED_INT;
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            InterlockedAdd(ROOM_AT_HASH(hashValue), amount, result);
            return;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
}

[numthreads(ZEROING_GRP_SIZE, 1, 1)]
void ClearTable(uint3 id : SV_DispatchThreadID)
{
    uint myId = id.x;

    if (myId >= tableSize)
        return;

    HASH_OCCUPANCY(myId) = EMPTY;
}
