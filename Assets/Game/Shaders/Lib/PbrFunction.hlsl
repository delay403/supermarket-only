
#ifndef PBR_Function_INCLUDE
#define PBR_Function_INCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

half LerpWhite2(half b, half t)
{
    half oneMinusT = 1.0 - t;
    return oneMinusT + b * t;
}
//法线微表面分布函数 :Based on Minimalist CookTorrance BRDF /URP NDF [Modified] GGX .../Lighting.hlsl -326
half D_Function(half NdotH,half LdotH, half roughness)
{
    half a2 = roughness*roughness;
    half nTerm = roughness * 4.0h + 2.0h;
    float d = NdotH * NdotH * (a2-1.0h) + 1.00001f; //must be float
    half LoH2 = LdotH * LdotH;
    half specTerm = a2 / ((d * d) * max(0.1h, LoH2) * nTerm);
    specTerm = specTerm - HALF_MIN;
    return specTerm = clamp(specTerm, 0.0, 100.0);
}

half D_Function2(half NdotH,half LdotH, half roughness)
{
    half a2 = roughness*roughness;
    half d = NdotH * NdotH * (a2-1.0h) + 1.0001h; //must be half
    half LoH2 = LdotH * LdotH;
    half specTerm = a2 / ((d * d) * max(0.1h, LoH2) * (roughness + 0.5)*4);
    specTerm = min(specTerm, 10);
    return specTerm;
}

     
 float G_section(float dot,float k)
 {
     float nom=dot;
     float denom=lerp(dot,1,k);
     return nom/denom;
 }

 
 float G_Function(float NdotL,float NdotV,float roughness)
 {
     float k=pow(1+roughness,2)/8;
     float Gnl=G_section(NdotL,k);
     float Gnv=G_section(NdotV,k);
     return Gnl*Gnv;
 }

 
float3 F_Function(float HdotL,float3 F0)
{
 float Fre=exp2((-5.55473*HdotL-6.98316)*HdotL);
 return lerp(Fre,1,F0);
}

 
float3 IndirF_Function(float NdotV,float3 F0,float roughness)
{
 float Fre=exp2((-5.55473*NdotV-6.98316)*NdotV);
 return F0+Fre*saturate(1-roughness-F0);
}

 
float3 CatSHEvalLinearL0L1(float3 N, float4 shAr, float4 shAg, float4 shAb)
{
    float4 vA = real4(N, 1.0);

    float3 x1;
    // Linear (L1) + constant (L0) polynomial terms
    x1.r = dot(shAr, vA);
    x1.g = dot(shAg, vA);
    x1.b = dot(shAb, vA);

    return x1;
}

float3 CatSHEvalLinearL2(float3 N, float4 shBr, float4 shBg, float4 shBb, float4 shC)
{
    float3 x2;
    // 4 of the quadratic (L2) polynomials
    float4 vB = N.xyzz * N.yzzx;
    x2.r = dot(shBr, vB);
    x2.g = dot(shBg, vB);
    x2.b = dot(shBb, vB);

    // Final (5th) quadratic (L2) polynomial
    float vC = N.x * N.x - N.y * N.y;
    float3 x3 = shC.rgb * vC;

    return x2 + x3;
}

half3 CatSampleSH9_Half(half4 SHCoefficients[7], half3 N)
{
    half4 shAr = SHCoefficients[0];
    half4 shAg = SHCoefficients[1];
    half4 shAb = SHCoefficients[2];
    half4 shBr = SHCoefficients[3];
    half4 shBg = SHCoefficients[4];
    half4 shBb = SHCoefficients[5];
    half4 shCr = SHCoefficients[6];

    // Linear + constant polynomial terms
    half3 res = CatSHEvalLinearL0L1(N, shAr, shAg, shAb);
    // Quadratic polynomials
    res += CatSHEvalLinearL2(N, shBr, shBg, shBb, shCr);
    return res;
}


// SH Vertex Evaluation.间接光 球谐光照 .../Lighting -386
 half3 SH_IndirectionDiff(half3 normalWS)
 {
     half4 SHCoefficients[7];
     SHCoefficients[0]=unity_SHAr;
     SHCoefficients[1]=unity_SHAg;
     SHCoefficients[2]=unity_SHAb;
     SHCoefficients[3]=unity_SHBr;
     SHCoefficients[4]=unity_SHBg;
     SHCoefficients[5]=unity_SHBb;
     SHCoefficients[6]=unity_SHC;
           
     return max(half3(0, 0, 0), CatSampleSH9_Half(SHCoefficients, normalWS));
 }

half3 MYDecodeHDREnvironment(half4 encodedIrradiance, half4 decodeInstructions)
{
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    half alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);

    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * encodedIrradiance.rgb;
}
 
half3 IndirSpeCube(float3 normalWS,float3 viewWS,float roughness,float AO)
{
     float3 reflectDirWS=reflect(-viewWS,normalWS);
     roughness=roughness*(1.7-0.7*roughness);
     float MidLevel=roughness*6;
     half4 speColor=SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0,reflectDirWS,MidLevel);
#if !defined(UNITY_USE_NATIVE_HDR)
     return MYDecodeHDREnvironment(speColor,unity_SpecCube0_HDR)*AO;
#else
     return speColor.xyz*AO;
#endif
}

 
float3 IndirSpeFactor(float roughness,float smoothness,float3 BRDFspe,float3 F0,float NdotV)
{
#ifdef UNITY_COLORSPACE_GAMMA
     float SurReduction=1-0.28*roughness*roughness;
#else
     float SurReduction=1/(roughness*roughness+1);
#endif
    
#if defined(SHADER_API_GLES)
     float Reflectivity=BRDFspe.x;
#else
     float Reflectivity=max(max(BRDFspe.x,BRDFspe.y),BRDFspe.z);
#endif
    
     half GrazingTSection=saturate(Reflectivity+smoothness);
     float Fre=Pow4(1-NdotV); 
     return lerp(F0,GrazingTSection,Fre)*SurReduction;
}

half3 IndirSpecFactor2(half roughness, half3 IndirSpeCubeColor, float3 F0, half metallic, half smoothness, float NdotV)
{
#ifdef UNITY_COLORSPACE_GAMMA
    half surfReduc=1-0.28*roughness*roughness;
#else
    half surfReduc = 1.0 / (roughness*roughness+1.0);
#endif
    //half3 myIndirectSpecular = GlossyEnvironmentReflection(rdotv, TEMProughness, occlusion);
    half3 mySpecular = F0;
    half myReflect = 1.0-(0.96-metallic*0.96); //focus metallic smoothness!
    half myGrazingTerm = saturate((1-smoothness) + myReflect); //.../Lighthing 268
    half myFresnelTerm = Pow4(1.0 - NdotV); //.../Lighting 501
    //half3 c = surfReduc*myIndirectSpecular*lerp(mySpecular, myGrazingTerm, myFresnelTerm);
    half3 c = surfReduc*IndirSpeCubeColor*lerp(mySpecular, myGrazingTerm, myFresnelTerm);
    return c;
}

//-----------------Fabric
half3 CatDiffuseFabric(half3 diffuseColor, half roughness)
{
    half3 invDiffCol = diffuseColor*0.3183091;
    return invDiffCol*lerp(1, 0.5, roughness);
}

real CatCharlieL(real x, real r)
{
    r = saturate(r);
    r = 1.0 - (1.0 - r) * (1.0 - r);

    float a = lerp(25.3245, 21.5473, r);
    float b = lerp(3.32435, 3.82987, r);
    float c = lerp(0.16801, 0.19823, r);
    float d = lerp(-1.27393, -1.97760, r);
    float e = lerp(-4.85967, -4.32054, r);

    return a / (1. + b * PositivePow(x, c)) + d * x + e;
}

float CatVisCharlieFabic(float NoL, float NoV, float Roughness)
{
    float lambdaV = NoV < 0.5 ? exp(CatCharlieL(NoV, Roughness)) : exp(2 * CatCharlieL(0.5, Roughness) - CatCharlieL(1 - NoV, Roughness));
    float lambdaL = NoL < 0.5 ? exp(CatCharlieL(NoL, Roughness)) : exp(2 * CatCharlieL(0.5, Roughness) - CatCharlieL(1 - NoL, Roughness));
			
    return rcp( (1 + lambdaV + lambdaL) * (4 * NoV * NoL) );
}

real CatD_CharlieNoPI(real NdotH, real roughness)
{
    float invR = rcp(roughness);
    float cos2h = NdotH * NdotH;
    float sin2h = 1.0 - cos2h;
    // Note: We have sin^2 so multiply by 0.5 to cancel it
    return (2.0 + invR) * PositivePow(sin2h, invR * 0.5) / 2.0;
}

real CatD_Charlie(real NdotH, real roughness)
{
    return INV_PI * CatD_CharlieNoPI(NdotH, roughness);
}

real3 CatF_Schlick(real3 f0, real f90, real u)
{
    real x = 1.0 - u;
    real x2 = x * x;
    real x5 = x * x2 * x2;
    return f0 * (1.0 - x5) + (f90 * x5);        // sub mul mul mul sub mul mad*3
}

half3 CatSpecFabric(half ndoth, half ndotl, half ndotv, half ldoth, half tempRoughness, half3 albedoCol)
{
    half fabric_InvGGX = CatD_Charlie(ndoth, tempRoughness);
    half fabric_Vis = CatVisCharlieFabic(ndotl + 1e-7, ndotv + 1e-7, tempRoughness);
    half3 F0Fabric = lerp(half3(0.5*0.08,0.5*0.08,0.5*0.08), albedoCol, 0.1);
    half3 fabric_Fresnel = CatF_Schlick(F0Fabric, 1, ldoth);
    return (fabric_Vis*fabric_InvGGX)*fabric_Fresnel;
}
//-----------------Fabric

//-----------------UVMovement
float2 UVMovement(float2 uv, float Speed, float Scale)
{
    float2 newUV = uv * Scale + (_Time.y * Speed).rr;
    return newUV;
}
//-----------------UVMovement

//-----------------Depth
float4 ComputeScreenPos(float4 pos, float projectionSign)
{
    float4 o = pos * 0.5f;
    o.xy = float2(o.x, o.y * projectionSign) + o.w;
    o.zw = pos.zw;
    return o;
}
float CustomSampleSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv),1.0).r;
}
float GetDepthFade(float3 WorldPos, float Distance)
{
    
    float4 ScreenPosition = ComputeScreenPos(TransformWorldToHClip(WorldPos), _ProjectionParams.x);
    float EyeDepth = LinearEyeDepth(CustomSampleSceneDepth(ScreenPosition.xy / ScreenPosition.w), _ZBufferParams);
    return saturate((EyeDepth - ScreenPosition.a) / Distance);
}
float3 GetSceneColor(float3 WorldPos)
{
    float4 ScreenPosition = ComputeScreenPos(TransformWorldToHClip(WorldPos), _ProjectionParams.x);
    return SampleSceneColor((ScreenPosition.xy) / ScreenPosition.w);
}

//-----------------Depth

//-----------------WaveFunc
float3 GetGerstnerWave(float4 wave, float3 p, float speed, inout float3 tangent, inout float3 binormal)
{
    float steepness = wave.z;
    float wavelength = wave.w;
    float k = 2 * 3.1415926/ wavelength;
    float c = sqrt(9.8/k);
    float2 d = normalize(wave.xy);
    float f = k * (dot(d, p.xz) - c * _Time.y * speed);
    float a = steepness/k;
    tangent += float3(
        -d.x*d.x*(steepness*sin(f)),
        d.x*(steepness*cos(f)),
        -d.x*d.y*(steepness*sin(f))
    );
    binormal += float3(
        -d.x*d.y*(steepness*sin(f)),
        d.y*(steepness*cos(f)),
        -d.y*d.y*(steepness*sin(f))
    );
    return float3(
        d.x*(a*cos(f)),
        a*sin(f),
        d.y*(a*cos(f))
    );
}
//--------------------WaveFuncEnd

//--------------------VoronoiGen
float2 VoronoiHash(float2 p )
{
    p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
    return frac( sin( p ) *43758.5453);
}
		
float VoronoiFunc( float2 v, float time, float smoothness)
{
    float2 n = floor( v );
    float2 f = frac( v );
    float F1 = 8.0;
    for ( int j = -1; j <= 1; j++ )
    {
        for ( int i = -1; i <= 1; i++ )
        {
            float2 g = float2( i, j );
            float2 o = VoronoiHash(n + g );
            o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 );
            float2 r = f - g - o;
            float d = 0.5 * dot( r, r );
            float h = smoothstep(0.0, 1.0, 0.5 + 0.5 * (F1 - d) / smoothness);
            F1 = lerp(F1, d, h) - smoothness * h * (1.0 - h);
        }
    }
    return F1;
}
//--------------------VoronoiGenEnd

#endif