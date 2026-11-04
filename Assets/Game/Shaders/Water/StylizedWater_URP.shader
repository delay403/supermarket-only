Shader "Game/Water/StylizedWater_URP"
{
    Properties
    {
        [HDR] _ShallowColor ("Shallow Color", Color) = (0.25, 0.75, 0.95, 0.65)
        [HDR] _DeepColor ("Deep Color", Color) = (0.05, 0.25, 0.55, 0.85)
        _FlowSpeed ("Flow Speed", Vector) = (0.05, 0.03, 0, 0)
        _WaveFrequency ("Wave Frequency", Range(0.1, 8)) = 2.5
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.04
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.8
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.6
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.5
        [HDR] _FresnelColor ("Fresnel Color", Color) = (0.7, 0.95, 1.0, 1)
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.75
        _DepthFadeDistance ("Depth Fade Distance", Range(0.01, 5)) = 1.2
        [NoScaleOffset] _MainTex ("Flow Texture", 2D) = "white" {}
        _MainTexScale ("Flow Tiling", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "StylizedWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma target 2.0
            #pragma prefer_hlslcc gles

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FlowSpeed;
                half _WaveFrequency;
                half _WaveAmplitude;
                half _WaveSpeed;
                half _NormalStrength;
                half _FresnelPower;
                half4 _FresnelColor;
                half _FresnelIntensity;
                half _DepthFadeDistance;
                half4 _MainTexScale;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float SampleSceneDepth01(float4 screenPos)
            {
                float2 uv = screenPos.xy / screenPos.w;
            #if UNITY_REVERSED_Z
                return SampleSceneDepth(uv);
            #else
                return lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
            }

            float3 ApplyWave(float3 positionWS, float2 uv)
            {
                float t = _Time.y * _WaveSpeed;
                float wave = sin((positionWS.x + positionWS.z) * _WaveFrequency + t);
                wave += cos((positionWS.x * 0.7 - positionWS.z * 1.1) * _WaveFrequency * 0.8 + t * 1.2);
                positionWS.y += wave * _WaveAmplitude;
                return positionWS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWave(positionWS, input.uv);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _MainTexScale.xy;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flowUv1 = input.uv + _Time.y * _FlowSpeed.xy;
                float2 flowUv2 = input.uv * 1.37 + _Time.y * _FlowSpeed.xy * float2(-0.8, 1.1);
                half flow1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUv1).r;
                half flow2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUv2).r;
                half flow = (flow1 + flow2) * 0.5;

                float sceneDepth = SampleSceneDepth01(input.screenPos);
                float surfaceDepth = input.screenPos.z / input.screenPos.w;
                float depthDiff = max(sceneDepth - surfaceDepth, 0.0);
                half depthFade = smoothstep(0.05, max(_DepthFadeDistance, 0.06), depthDiff);

                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 normalWS = normalize(input.normalWS + half3(flow - 0.5, 1.0, flow - 0.5) * _NormalStrength);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                half4 waterCol = lerp(_DeepColor, _ShallowColor, depthFade);
                waterCol.rgb += flow * 0.08;
                waterCol.rgb += _FresnelColor.rgb * fresnel * _FresnelIntensity;
                waterCol.a = lerp(_DeepColor.a, _ShallowColor.a, depthFade);

                waterCol.rgb = MixFog(waterCol.rgb, input.fogFactor);
                return waterCol;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
