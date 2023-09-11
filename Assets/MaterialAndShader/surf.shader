Shader "Custom/surf"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        _FogStart ("fog start", float) = 0.0
        _FogEnd ("fog end", float) = 100.0
        _FogColor ("fog color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        LOD 200

        CGPROGRAM

        #pragma enable_d3d11_debug_symbols
        #include "UnityCG.cginc"
        #pragma vertex vert
        #pragma surface surf Standard fullforwardshadows
       
        
        #pragma instancing_options procedural:setup


        struct DirectedPoint {
            float3 position;
            float3 tangent;
            int isActive;
        };

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float fogMag;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _FogStart;
        float _FogEnd;
        float4 _FogColor;
        
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<DirectedPoint> particles;
        #endif

        void vert(inout appdata_full v, out Input data)
        {

            UNITY_INITIALIZE_OUTPUT(Input, data);
            
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            DirectedPoint p = particles[unity_InstanceID];
            if(p.isActive == 1) v.vertex *= 0.2;
            v.vertex.xyz *= 1.0;
            v.vertex.xyz += p.position;

            float z = length(_WorldSpaceCameraPos - v.vertex);
            data.fogMag = exp(-0.005 * z);
            
            #endif
        }

        void setup()
        {
            
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            c = lerp(_FogColor, c, IN.fogMag);
            o.Albedo = c;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
