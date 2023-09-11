Shader "Unlit/unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("", Color) = (1,0,0,1)
        _RWParticleSize("size of RW Particle", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/ScriptsAndCs/DirectedPoint.cginc"
                       
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 wPos : TEXCOORD2;
                float3 normal : TEXCOORD1;
                int life : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4x4 ParentTransform;
            float4 _MainTex_ST;
            float4 _Color;
            float _RWParticleSize;

            StructuredBuffer<DirectedPoint> particles;

            v2f vert (appdata_full v, uint id : SV_InstanceID)
            {
                DirectedPoint p = particles[id];
                // stretch along tangent vector
                float3 disp = p.tangent * dot(p.tangent, v.vertex) * 1.2;
                if(p.isActive == 1) v.vertex.xyz *= _RWParticleSize;

                // scale up unit sphere
                v.vertex.xyz *= 1.6;
                
                v2f o;
                
                o.wPos = v.vertex + p.position;// + disp;
                o.wPos = mul(ParentTransform, float4(o.wPos, 1)).xyz;
                o.vertex = UnityObjectToClipPos(o.wPos);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.life = p.life;
                
                return o;
            }

            float3 hsv2rgb_smooth(float3 c )
            {
                float3 rgb = clamp( abs(fmod(c.x*6.0+float3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0 );

	            rgb = rgb*rgb*(3.0-2.0*rgb); // cubic smoothing	

	            return c.z * lerp(float3(1.0, 1.0, 1.0), rgb, c.y);
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float o2camera = length(_WorldSpaceCameraPos);
                float d = 0.2 * length(_WorldSpaceCameraPos - i.wPos) / o2camera;
                d = saturate(1-d);
                //return float4(d.xxx, 1.0);
                
                float c = log(i.life*0.1 + 1)/6.0;
                //c = i.life / 800.0;
                c = frac(c);
                float3 rgb1 = float3(c,c,c);
                float3 rgb2 = hsv2rgb_smooth(float3(c, 0.6, 0.6));

                float3 rgb = lerp(rgb1, rgb2, saturate(i.life/800.0));
                rgb *= d;
                rgb.r *= 0.8;
                rgb.g = saturate(rgb.g * 1.2);
                return float4(rgb, 1);
            }
            ENDCG
        }
    }
}
