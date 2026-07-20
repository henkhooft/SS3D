#ifndef SS3D_ST_FRAGMENT_INCLUDED
#define SS3D_ST_FRAGMENT_INCLUDED

#include "STLighting.hlsl"

STSurfaceInput ST_GetSurface(STVaryings input)
{
    STSurfaceInput surface;
    surface.uv = input.uv;
    surface.normalWS = input.normalWS;
    surface.viewDirWS = input.viewDirWS;
    surface.positionWS = input.positionWS;
    return surface;
}

half4 ST_FragLit(STVaryings input, bool swapLightColorBlend, bool gateZeroLight, half alphaMultiplier)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 col = ST_EvaluateLighting(ST_GetSurface(input), input.positionCS, swapLightColorBlend, gateZeroLight, alphaMultiplier);
    return col;
}

#endif
