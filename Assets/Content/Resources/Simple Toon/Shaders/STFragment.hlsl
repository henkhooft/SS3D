#ifndef SS3D_ST_FRAGMENT_INCLUDED
#define SS3D_ST_FRAGMENT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "STLighting.hlsl"

// Cotangent frame from screen-space derivatives — works when the mesh has no
// (or zero) tangents. Material preview spheres have tangents; FloorTile often
// does not, which made normals show in the Inspector and vanish in-game.
float3x3 ST_CotangentFrame(float3 normalWS, float3 positionWS, float2 uv)
{
    float3 dp1 = ddx(positionWS);
    float3 dp2 = ddy(positionWS);
    float2 duv1 = ddx(uv);
    float2 duv2 = ddy(uv);

    float3 dp2perp = cross(dp2, normalWS);
    float3 dp1perp = cross(normalWS, dp1);
    float3 tangentWS = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 bitangentWS = dp2perp * duv1.y + dp1perp * duv2.y;

    float invMax = rsqrt(max(dot(tangentWS, tangentWS), dot(bitangentWS, bitangentWS)));
    return float3x3(tangentWS * invMax, bitangentWS * invMax, normalWS);
}

float3 ST_GetNormalWS(STVaryings input)
{
    float3 normalWS = normalize(input.normalWS);

    if (_BumpScale <= 0.001)
    {
        return normalWS;
    }

    float2 bumpUV = TRANSFORM_TEX(input.uv, _BumpMap);
    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV), _BumpScale);

    // Always use derivative TBN. FloorTile quads often ship with missing/wrong mesh
    // tangents — Inspector preview spheres have good ones, so normals showed there
    // and disappeared in-game when we relied on interpolated mesh TBN.
    float3x3 tangentToWorld = ST_CotangentFrame(normalWS, input.positionWS, bumpUV);
    return normalize(TransformTangentToWorld(normalTS, tangentToWorld));
}

STSurfaceInput ST_GetSurface(STVaryings input)
{
    STSurfaceInput surface;
    surface.uv = TRANSFORM_TEX(input.uv, _MainTex);
    surface.normalWS = ST_GetNormalWS(input);
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
