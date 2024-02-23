#define EMPTY 0
#define TAKEN 1
#define NAN asfloat(0xffffffff)
#define UNDEFINED_INT -2147483647
#define UNDEFINED_FLOAT3 float3(340000000000000000000000000000000.0f, 340000000000000000000000000000000.0f, 340000000000000000000000000000000.0f)
#define INDEX_AT_HASH(h) (hashTableBuffer[h].indexAndRoom.xyz)
#define ROOM_AT_HASH(h) (hashTableBuffer[h].indexAndRoom.w)
#define VELOCITY_AT_HASH(h) (hashTableBuffer[h].velocity.xyz)
#define POSITION_AT_HASH(h) (hashTableBuffer[h].positionAndMass.xyz)
#define ORIGINAL_MASS_AT_HASH(h) (hashTableBuffer[h].positionAndMass.w)
#define MINBOUND_AT_HASH(h) (hashTableBuffer[h].minBound.xyz)
#define MAXBOUND_AT_HASH(h) (hashTableBuffer[h].maxBound.xyz)
#define INNER_MINBOUND_AT_HASH(h) (hashTableBuffer[h].innerMinBound.xyz)
#define INNER_MAXBOUND_AT_HASH(h) (hashTableBuffer[h].innerMaxBound.xyz)
#define HASH_OCCUPANCY(h) (hashTableOccupancy[h])

// Constant buffer for hash table configuration
uint tableSize;

uint ComputeHash(int3 key)
{
    int prime = 2362873;
    uint hashValue = 1;
    
    hashValue = (hashValue * prime) + key.x;
    hashValue = (hashValue * prime) + key.y;
    hashValue = (hashValue * prime) + key.z;
    
    hashValue = (hashValue ^ (hashValue >> 17)) * prime;
    hashValue = (hashValue ^ (hashValue >> 13)) * prime;
    hashValue = hashValue ^ (hashValue >> 16);
    
    return hashValue & (tableSize - 1);
}

bool LookupRoom(int3 index, out int room)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    room = UNDEFINED_INT;
    for (int i = 0; i < (int)tableSize; i++) {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            room = ROOM_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY) {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupPosAndVel(int3 index, out float3 position, out float3 velocity)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    position = UNDEFINED_FLOAT3;
    velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            position = POSITION_AT_HASH(hashValue);
            velocity = VELOCITY_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupOriginalMass(int3 index, out float ogMass)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    //velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            ogMass = ORIGINAL_MASS_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupVel(int3 index, out float3 velocity)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    //velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            velocity = VELOCITY_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupBounds(int3 index, out float3 minBounds, out float3 maxBounds, out float3 innerMinBounds, out float3 innerMaxBounds)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    //velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            minBounds = MINBOUND_AT_HASH(hashValue);
            maxBounds = MAXBOUND_AT_HASH(hashValue);
            innerMinBounds = INNER_MINBOUND_AT_HASH(hashValue);
            innerMaxBounds = INNER_MAXBOUND_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupBounds(int3 index, out float3 minBounds, out float3 maxBounds)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    //velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            minBounds = MINBOUND_AT_HASH(hashValue);
            maxBounds = MAXBOUND_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}

bool LookupRoomAndBounds(int3 index, out int room, out float3 minBounds, out float3 maxBounds, out float3 innerMinBounds, out float3 innerMaxBounds)
{
    // Compute the hash value
    uint hashValue = ComputeHash(index);

    //velocity = float3(0, 0, 0);
    for (int i = 0; i < (int) tableSize; i++)
    {
        if (HASH_OCCUPANCY(hashValue) == TAKEN && all(INDEX_AT_HASH(hashValue) == index))
        {
            room = ROOM_AT_HASH(hashValue);
            minBounds = MINBOUND_AT_HASH(hashValue);
            maxBounds = MAXBOUND_AT_HASH(hashValue);
            innerMinBounds = INNER_MINBOUND_AT_HASH(hashValue);
            innerMaxBounds = INNER_MAXBOUND_AT_HASH(hashValue);
            return true;
        }
        if (HASH_OCCUPANCY(hashValue) == EMPTY)
        {
            return false;
        }
        //Linearly probe forward
        hashValue = (hashValue + 1) & (tableSize - 1);
    }
    return false;
}
