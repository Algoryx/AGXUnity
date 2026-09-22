void undistortedPointBrownConrady_float(float2 uv, float k1, float k2, float k3, float p1, float p2, out float2 distorted)
{
    float2 p = uv;
    for (int i = 0; i < 10; ++i) {
        float r2 = p.x*p.x + p.y*p.y;

        float L = 1.0 + r2*(k1 + r2*(k2 + r2*k3));

        float dx = 2.0*p1*p.x*p.y + p2*(r2 + 2.0*p.x*p.x);
        float dy = 2.0*p2*p.x*p.y + p1*(r2 + 2.0*p.y*p.y);

        float2 pNext = float2((uv.x - dx) / L, (uv.y - dy) / L);

        float cx = abs(pNext.x - p.x);
        float cy = abs(pNext.y - p.y);
        p = pNext;

        if (abs(cx) < 1e-4 && abs(cy) < 1e-4) // This should be ok up to 1e4 resolution
            break;
    }

    distorted = p;
}
