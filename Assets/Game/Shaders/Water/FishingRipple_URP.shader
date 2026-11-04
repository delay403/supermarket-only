Shader "Game/Water/FishingRipple_URP"
{
    Properties
    {
        [HDR] _Color ("Ripple Color", Color) = (0.75, 0.95, 1.0, 0.85)
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.12
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _Progress ("Progress", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "FishingRipple"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma prefer_hlslcc gles

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RingWidth;
                half _Softness;
                half _Progress;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0;
                float radius = lerp(0.05, 0.95, _Progress);
                float ring = 1.0 - abs(dist - radius) / max(_RingWidth, 0.001);
                ring = saturate(ring);
                ring = smoothstep(0.0, _Softness, ring);

                half4 col = _Color;
                col.a *= ring * saturate(_Fade);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
