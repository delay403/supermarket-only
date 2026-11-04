#ifndef SSR_RM_Function_INCLUDE
#define SSR_RM_Function_INCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

    float UVJitter(in float2 uv)
    {
        return frac((52.9829189 * frac(dot(uv, float2(0.06711056, 0.00583715)))));
    }
    
    void SSRRayConvert(float3 worldPos, out float4 clipPos, out float3 screenPos)
    {	
        clipPos = TransformWorldToHClip(worldPos);
        float k = ((1.0) / (clipPos.w));
        screenPos.xy = ComputeScreenPos(clipPos).xy * k;
        screenPos.z = k;
    }
	
	float3 SSRRayMarch(float4 posCS, float3 posWS, float3 R)
	{
		float4 startClipPos;
		float3 startScreenPos;

		SSRRayConvert(posWS, startClipPos, startScreenPos);

		float4 endClipPos;
		float3 endScreenPos;

		SSRRayConvert(posWS + R, endClipPos, endScreenPos);

		if (((endClipPos.w) < (startClipPos.w)))
		{
			return float3(0, 0, 0);
		}

		float3 screenDir = endScreenPos - startScreenPos;

		float screenDirX = abs(screenDir.x);
		float screenDirY = abs(screenDir.y);

		float dirMultiplier = lerp( 1 / (_ScreenParams.y * screenDirY), 1 / (_ScreenParams.x * screenDirX), screenDirX > screenDirY ) * 8;//_SSRSampleStep;Range(4, 32)

		screenDir *= dirMultiplier;

		half lastRayDepth = startClipPos.w;

		half sampleCount = 1 + UVJitter(posCS.xy) * 0.1; 

		float3 lastScreenMarchUVZ = startScreenPos;
		float lastDeltaDepth = 0;

#if defined (SHADER_API_OPENGL) || defined (SHADER_API_D3D11) || defined (SHADER_API_D3D12)
		[unroll(64)]
#else
		UNITY_LOOP
#endif
		for(int i = 0; i < 32; i++)//_SSRMaxSampleCount; i++)Range(0, 64)
		{
			float3 screenMarchUVZ = startScreenPos + screenDir * sampleCount;

			if((screenMarchUVZ.x <= 0) || (screenMarchUVZ.x >= 1) || (screenMarchUVZ.y <= 0) || (screenMarchUVZ.y >= 1))
			{
				break;
			}


			float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenMarchUVZ.xy), _ZBufferParams);


			half rayDepth = 1.0 / screenMarchUVZ.z;

			half deltaDepth = rayDepth - sceneDepth;

			if((deltaDepth > 0) && (sceneDepth > startClipPos.w) && (deltaDepth < (abs(rayDepth - lastRayDepth) * 2)))
			{
				float samplePercent = saturate(lastDeltaDepth / (lastDeltaDepth - deltaDepth));
				samplePercent = lerp(samplePercent, 1, rayDepth >= _ProjectionParams.z);
				float3 hitScreenUVZ = lerp(lastScreenMarchUVZ, screenMarchUVZ, samplePercent);
				return float3(hitScreenUVZ.xy, 1);
			}

			lastRayDepth = rayDepth;
			sampleCount += 1;

			lastScreenMarchUVZ = screenMarchUVZ;
			lastDeltaDepth = deltaDepth;
		}

		float4 farClipPos;
		float3 farScreenPos;

		SSRRayConvert(posWS + R * 100000, farClipPos, farScreenPos);

		if((farScreenPos.x > 0) && (farScreenPos.x < 1) && (farScreenPos.y > 0) && (farScreenPos.y < 1))
		{
			float farDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, farScreenPos.xy), _ZBufferParams);
			
			if(farDepth > startClipPos.w)
			{
				return float3(farScreenPos.xy, 1);
			}
		}

		return float3(0, 0, 0);
	}

	float3 GetSSRUVZ(float4 posCS, float3 posWS, float3 R, float2 screenUV, float NoV)
    {
    	float sUV = screenUV.r * 2 - 1;
    	sUV *= sUV;
    	half ssrWeight = saturate(1 - dot(sUV, sUV));
    	half _noV = NoV * 1.5; //control reflect alpha Z via ndotv theta
    	ssrWeight *= (1 - _noV * _noV);

    	float3 result;
    	
    	if (ssrWeight > 0.005)
    	{
    		float3 uvz = SSRRayMarch(posCS, posWS, R);
    		uvz.z *= ssrWeight;
    		result = uvz;
    	}else
    	{
    		result = float3(0, 0, 0);
    	}
    	
    	return result;
    }

	half4 GetSSRLighting(float4 posCS, float3 posWS, float3 R, float2 screenUV, float NoV, half SSRInt)
    {
    	float3 uvz = GetSSRUVZ(posCS, posWS, R, screenUV, NoV);
    	half3 ssrColor = lerp(half3(0, 0, 0), SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvz.xy).rgb * SSRInt, uvz.z > 0);
    	return half4(ssrColor, uvz.z);
    }

	half GetOneMinusReflectivity(half m)
    {
    	return ((1.0-0.04) - m * (1.0-0.04));
    }

	half GetSSRSpecFactor(half roughness, half smoothness, float3 BRDFspe, float3 F0, float NdotV)
    {
		half SurReduction = 1/(roughness*roughness+1);
    	float Reflectivity = max(max(BRDFspe.x,BRDFspe.y), BRDFspe.z);

    	#if defined(SHADER_API_GLES)
    	Reflectivity = BRDFspe.x;
    	#endif
		
    	half GrazingTSection = saturate(Reflectivity+smoothness);
    	float Fre = Pow4(1-NdotV);
    	return lerp(GrazingTSection, Fre+F0.r, roughness)*SurReduction; //F0 useless for SSR lerp also fresnel will cut Y axis reflection, focus roughness as lerp factor!
    	//return GrazingTSection;
    }	

	half3 GetReflectionWithSSR(half metallic, half AO, float4 posCS, float3 posWS, float3 R, float2 screenUV, half NoV, half SSRInt)
    {
    	half3 ssrLighting = GetSSRLighting(posCS, posWS, R, screenUV, NoV, SSRInt).rgb*GetOneMinusReflectivity(metallic)*AO;
		return ssrLighting;
    }

	half4 GetRefraction(float2 screenUV, float3 vDirWS, float3 nDirWS, float3 screenPos)
    {
    	float2 grabUV = screenUV;

    	half3 worldViewDir = normalize(vDirWS);
    	half worldViewDirY = abs(worldViewDir.y);
    	
    	float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV), _ZBufferParams);
    	depth = depth - screenPos.z;

    	half2 deltaUV = nDirWS.xz * 1.0 * saturate(depth) * worldViewDirY / screenPos.z; //Range(0, 10)
    	
    	float newDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + deltaUV), _ZBufferParams);
    	newDepth = newDepth - screenPos.z;

    	half signDepth = saturate(newDepth * 10);
    	grabUV = grabUV + deltaUV * signDepth;

    	depth = lerp(depth, newDepth, signDepth);

    	half viewMultiplier = (worldViewDirY + 1.0) * 1.0 * 1.0; // Scale=Range(0, 2); Offset;Offset=Range(0, 1);
    	depth *= viewMultiplier;

    	half alpha = saturate(1 - depth);
    	alpha = saturate(1.02 - pow(alpha, (dot(nDirWS.xyz, worldViewDir) * 5 + 6)));

    	half4 refraction = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, grabUV);
    	refraction.rgb = lerp(refraction.rgb, refraction.rgb * half3(1.0,1.0,1.0) * 1.0, alpha); //customCol
    	refraction.a = alpha;

    	return refraction;
    }

#endif