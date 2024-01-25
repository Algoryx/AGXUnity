// Contains utility methods for generating fragments with reconstructed world space positions based on the
// current camera depth buffer.
// Adapted from https://github.com/keijiro/DepthInverseProjection/blob/master/Assets/InverseProjection/Resources/InverseProjection.shader

// Sets up a vertex as part of a fullscreen triangle meant to be used to reconstruct the current
// fragment positions based on the depth buffer.
inline void DIPVertex(uint id, out float4 position, out float2 texcoord, out float3 ray){
	// Render settings
	float far = _ProjectionParams.z;
	float2 orthoSize = unity_OrthoParams.xy;
	float isOrtho = unity_OrthoParams.w; // 0: perspective, 1: orthographic

	// Vertex ID -> clip space vertex position
	float x = (id != 1) ? -1 : 3;
	float y = (id == 2) ? -3 : 1;
	float3 vpos = float3(x, y, 1.0);

	// Perspective: view space vertex position of the far plane
	float3 rayPers = mul(unity_CameraInvProjection, vpos.xyzz * far).xyz;

	// Orthographic: view space vertex position
	float3 rayOrtho = float3(orthoSize * vpos.xy, 0);

	position = float4(vpos.x, -vpos.y, 1, 1);
	texcoord = (vpos.xy + 1) / 2;
	ray = lerp(rayPers, rayOrtho, isOrtho);
}

// Reconstructs the world fragment position based on the current camera depth buffer.
inline float3 DIPFragment(float2 texcoord, float3 ray){
    // Render settings
    float near = _ProjectionParams.y;
    float far = _ProjectionParams.z;
    float isOrtho = unity_OrthoParams.w; // 0: perspective, 1: orthographic

    // Z buffer sample
    float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, texcoord);

    // Far plane exclusion
    #if defined(UNITY_REVERSED_Z)
    float mask = z > 0;
    #else
    float mask = z < 1;
    #endif

    // Perspective: view space position = ray * depth
    float3 vposPers = ray * Linear01Depth(z);

    // Orthographic: linear depth (with reverse-Z support)
    #if defined(UNITY_REVERSED_Z)
    float depthOrtho = -lerp(far, near, z);
    #else
    float depthOrtho = -lerp(near, far, z);
    #endif

    // Orthographic: view space position
    float3 vposOrtho = float3(ray.xy, depthOrtho);

    // Result: view space position
    return lerp(vposPers, vposOrtho, isOrtho) * mask;
}