#ifndef SS3D_ST_LIGHTING_INCLUDED
#define SS3D_ST_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "STCore.hlsl"

struct STSurfaceInput
{
    float2 uv;
    float3 normalWS;
    float3 viewDirWS;
    float3 positionWS;
};

float3 ST_GetCameraForwardWS()
{
    return mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 0.0, 1.0));
}

void ST_BuildInputData(STSurfaceInput surface, float4 positionCS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = surface.positionWS;
    inputData.normalWS = normalize(surface.normalWS);
    inputData.viewDirectionWS = normalize(surface.viewDirWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    inputData.shadowCoord = TransformWorldToShadowCoord(surface.positionWS);
#else
    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
#endif
}

AmbientOcclusionFactor ST_GetDefaultAmbientOcclusion()
{
    AmbientOcclusionFactor aoFactor;
    aoFactor.directAmbientOcclusion = half(1.0);
    aoFactor.indirectAmbientOcclusion = half(1.0);
    return aoFactor;
}

float4 ST_EvaluateDirectLight(
    STSurfaceInput surface,
    half3 lightColor,
    half3 lightDirectionWS,
    half lightAttenuation,
    bool swapLightColorBlend)
{
    STToonSettings settings = ST_GetToonSettings();

    if (dot(lightColor, 1.0) <= 0.0 || lightAttenuation <= 0.0)
    {
        return 0.0;
    }

    float3 normal = normalize(surface.normalWS);
    float3 lightDir = normalize(lightDirectionWS);

    float ndotl = dot(normal, lightDir);
    // Geometric half-toon / cel banding only. Shadow + distance attenuation must
    // scale the final contribution — folding atten into the shade/lit blend left
    // cast shadows as a pastel tint of the lit color (still looks fully lit).
    float toon = ST_Toon(ndotl, 1.0, settings);

    float4 litCol = swapLightColorBlend
        ? ST_ColorBlend(_Color, float4(lightColor, 1.0), _AmbientCol)
        : ST_ColorBlend(float4(lightColor, 1.0), _Color, _AmbientCol);
    float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, surface.uv) * litCol * _ColIntense + _ColBright;

    float4 shadeCol = (_HalfToon > 0.5)
        ? float4(texCol.rgb * _ShadowTint.rgb, texCol.a)
        : settings.darkColor;
    float4 blendCol = ST_ColorBlend(shadeCol, texCol, toon);
    float4 postCol = ST_PostEffects(blendCol, toon, lightAttenuation, ndotl, settings);

    if (_HalfToon > 0.5 && _SpecIntensity > 0.001)
    {
        float3 viewDir = normalize(surface.viewDirWS);
        float3 halfDir = normalize(lightDir + viewDir);
        float spec = pow(saturate(dot(normal, halfDir)), _SpecPower) * _SpecIntensity;
        postCol.rgb += spec * lightColor * texCol.rgb;
    }

    return postCol * lightAttenuation;
}

void ST_AccumulateLight(
    inout float4 result,
    STSurfaceInput surface,
    Light light,
    bool swapLightColorBlend)
{
    half lightAttenuation = light.shadowAttenuation * light.distanceAttenuation;
    if (dot(light.color, 1.0) <= 0.0 || lightAttenuation <= 0.0)
    {
        return;
    }

    // URP point distanceAttenuation blows up near the source (unlike PR #857's custom
    // deferred falloff). Soften the near field so overhead fills don't plasticize heads.
    lightAttenuation = lightAttenuation / (1.0h + 1.75h * lightAttenuation);

    float4 lit = ST_EvaluateDirectLight(
        surface,
        light.color,
        light.direction,
        lightAttenuation,
        swapLightColorBlend);

    // Half-toon: additive fixture stacking (two wall lights both reach the floor).
    // Full toon: max() preserves the original Simple Toon single-light read.
    if (_HalfToon > 0.5)
    {
        result += lit;
    }
    else
    {
        result = max(result, lit);
    }
}

float4 ST_EvaluateLighting(
    STSurfaceInput surface,
    float4 positionCS,
    bool swapLightColorBlend,
    bool gateZeroLight,
    half alphaMultiplier)
{
    float4 result = 0.0;

    InputData inputData;
    ST_BuildInputData(surface, positionCS, inputData);

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = ST_GetDefaultAmbientOcclusion();

#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
#else
    Light mainLight = GetMainLight();
#endif

    bool mainLightActive = dot(mainLight.color, 1.0) > 0.0;
    if (!gateZeroLight || mainLightActive)
    {
        ST_AccumulateLight(result, surface, mainLight, swapLightColorBlend);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        ST_AccumulateLight(result, surface, light, swapLightColorBlend);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        ST_AccumulateLight(result, surface, light, swapLightColorBlend);
    LIGHT_LOOP_END
#endif

    result.rgb += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, surface.uv).rgb * _EmissionColor.rgb;
    result.a = alphaMultiplier;
    return result;
}

#endif
