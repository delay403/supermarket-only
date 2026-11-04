Shader "Custom/Outline Fill URP" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
      "RenderPipeline" = "UniversalPipeline"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref 1
        Comp NotEqual
      }

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float4 _OutlineColor;
        float _OutlineWidth;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        half4 color : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings vert(Attributes input) {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normalOS;
        float3 viewPosition = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
        float3 viewNormal = normalize(TransformObjectToWorldNormal(normal));
        float3 clipPosition = viewPosition + viewNormal * (-viewPosition.z * _OutlineWidth * 0.001);
        output.positionCS = mul(UNITY_MATRIX_P, float4(clipPosition, 1.0));
        output.color = _OutlineColor;
        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        return input.color;
      }
      ENDHLSL
    }
  }
}
