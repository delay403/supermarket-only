
Shader "Custom/Outline"
{
    Properties
    {
        _MainTex("MainTex",2D)="white"{}
        _OutlineColor("OutlineColor",Color)=(1,1,1,1)
        _OutlineColor2("OutlineColor2",Color)=(1,1,1,1)
        _OutlineWidth("OutlineWidth",Range(0,0.5))=0.05
        _OutlineWidth2("OutlineWidth2",Range(0,0.5))=0.05
    }
    SubShader
    {
        Pass
        {
            Tags{"LigthMode"="ForwardBase"}
            Cull Front
            ZWrite On
 
            CGPROGRAM
 
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
 
            float4 _OutlineColor;
            float _OutlineWidth;
 
            struct appdata
            {
                float4 vertex : POSITION;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
 
            v2f vert(appdata v)
            {
                v2f o;
 
                float3 posZ=v.vertex.xyz;
 
                float3 posBig=posZ*(_OutlineWidth+1);
 
                o.pos=UnityObjectToClipPos(float4(posBig,1));
 
                return o;
            }
 
            fixed4 frag(v2f i):SV_Target
            {
                return _OutlineColor;
            }
 
            ENDCG
        }
        Pass
        {
            Tags{"LigthMode"="ForwardBase"}
            Cull Front
            ZWrite On
 
            CGPROGRAM
 
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
 
            float4 _OutlineColor2;
            float _OutlineWidth2;
 
            struct appdata
            {
                float4 vertex : POSITION;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
 
            v2f vert(appdata v)
            {
                v2f o;
 
                float3 posZ=v.vertex.xyz;
 
                float3 posBig=posZ*(_OutlineWidth2+1);
 
                o.pos=UnityObjectToClipPos(float4(posBig,1));
 
                return o;
            }
 
            fixed4 frag(v2f i):SV_Target
            {
                return _OutlineColor2;
            }
 
            ENDCG
        }
        Pass
        {
            Tags{"LigthMode"="ForwardBase"}
            Cull Back
            ZWrite On
 
            CGPROGRAM
 
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
 
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };
 
            v2f vert(appdata v)
            {
                v2f o;
 
                o.pos=UnityObjectToClipPos(v.vertex);
 
                o.worldNormal=UnityObjectToWorldNormal(v.normal);
 
                o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);
 
                return o;
            }
 
            fixed4 frag(v2f i):SV_Target
            {
                fixed3 N = normalize(i.worldNormal);
                fixed3 L = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 col = tex2D(_MainTex,i.uv).rgb;
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * col;
                fixed3 diffuse = _LightColor0.rgb * col * saturate(dot(N,L));
                return fixed4(diffuse+ambient,1);
            }
 
            ENDCG
        }
    }
}