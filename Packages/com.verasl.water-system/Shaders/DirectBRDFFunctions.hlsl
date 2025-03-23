#ifndef DIRECT_BRDF_FUNCTIONS_INCLUDED
#define DIRECT_BRDF_FUNCTIONS_INCLUDED

// DirectBDRF function variants for various parameter combinations
half3 DirectBDRF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    float3 halfDir = SafeNormalize(lightDirectionWS + viewDirectionWS);
    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    float d = NoH * NoH * (brdfData.roughness2MinusOne + 1.0) + 1.00001f;
    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / (d * max(0.1h, LoH2) * brdfData.normalizationTerm);

#if defined(SHADER_API_MOBILE) || defined(SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    return specularTerm * brdfData.specular + brdfData.diffuse;
}

half3 DirectBDRF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, half additionalData)
{
    // Same implementation as the 4-parameter variant
    float3 halfDir = SafeNormalize(lightDirectionWS + viewDirectionWS);
    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    float d = NoH * NoH * (brdfData.roughness2MinusOne + 1.0) + 1.00001f;
    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / (d * max(0.1h, LoH2) * brdfData.normalizationTerm);

#if defined(SHADER_API_MOBILE) || defined(SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    return specularTerm * brdfData.specular + brdfData.diffuse;
}

float DirectBDRF(float3 N, float3 L, float3 V, float roughness)
{
    float3 H = normalize(L + V);
    float NoH = saturate(dot(N, H));
    float NoH2 = NoH * NoH;
    float roughness2 = roughness * roughness;
    float roughness2MinusOne = roughness2 - 1.0;
    float d = NoH2 * roughness2MinusOne + 1.00001f;
    float normalizationTerm = roughness * 4.0 + 2.0;
    float LoH = saturate(dot(L, H));
    float LoH2 = LoH * LoH;
    float specularTerm = roughness2 / (d * max(0.1, LoH2) * normalizationTerm);

    specularTerm = clamp(specularTerm, 0.0, 100.0);

    return specularTerm;
}

float DirectBDRF(float3 N, float3 L, float3 V, float roughness, float additionalData)
{
    // Same implementation as the 4-parameter variant
    float3 H = normalize(L + V);
    float NoH = saturate(dot(N, H));
    float NoH2 = NoH * NoH;
    float roughness2 = roughness * roughness;
    float roughness2MinusOne = roughness2 - 1.0;
    float d = NoH2 * roughness2MinusOne + 1.00001f;
    float normalizationTerm = roughness * 4.0 + 2.0;
    float LoH = saturate(dot(L, H));
    float LoH2 = LoH * LoH;
    float specularTerm = roughness2 / (d * max(0.1, LoH2) * normalizationTerm);

    specularTerm = clamp(specularTerm, 0.0, 100.0);

    return specularTerm;
}

#endif // DIRECT_BRDF_FUNCTIONS_INCLUDED
