Shader "Hidden/Echoes/ArchiveSculpture"
{
    Properties
    {
        _Tint ("Archive tint", Color) = (1,1,1,1)
        _Glow ("Gentle light", Float) = .24
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Tint, _EchoesAir;
            float _Glow;
            struct Vertex { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct Fragment
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float3 model:TEXCOORD2;
                float3 color:COLOR;
            };

            Fragment vert(Vertex v)
            {
                Fragment o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.model = v.vertex.xyz;
                o.color = _Tint.rgb * v.color.rgb;
                return o;
            }

            float4 frag(Fragment i) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos - i.world;
                float range = max(.01,length(eye));
                eye /= range;
                float3 n = normalize(i.normal);
                float key = .45 + .55*saturate(dot(n,normalize(float3(-.49,.72,-.48))));
                float edge = pow(1-saturate(abs(dot(n,eye))),2.1);
                float vein = sin(i.model.y*59.0 + 2.0*sin(i.model.z*20.0+i.model.x*29.0));
                float sweep = pow(saturate(.5+.5*sin(i.model.y*2.1+_Time.y*1.15)),5);
                float3 color = i.color * (.54+.42*key+_Glow*.34+vein*.035);
                color += float3(.32,.57,.69)*edge*(.11+.13*sweep);
                float fog = 1-exp(-max(0,range-3)*max(.004,_EchoesAir.w)*.40);
                return float4(lerp(color,_EchoesAir.rgb,fog*.67),1);
            }
            ENDCG
        }
    }
}
