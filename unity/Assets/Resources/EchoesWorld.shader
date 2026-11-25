Shader "Hidden/Echoes/WorldSurface"
{
    Properties
    {
        _Surface ("Material", Float) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _Glow ("Emission", Float) = .24
        _GrainTex ("Surface color", 2D) = "gray" {}
        _Grain ("Surface texture strength and scale", Vector) = (0,.5,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            float _Surface, _Glow;
            float4 _Tint;
            sampler2D _GrainTex;
            float4 _Grain, _EchoesFootfall, _EchoesTerrainSink;
            float4 _EchoesAir, _EchoesKey, _EchoesAmbience, _EchoesWeather, _EchoesPool, _EchoesPoolColor;
            struct Vertex { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct Varying
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half3 color : COLOR;
                SHADOW_COORDS(2)
            };

            Varying vert(Vertex v)
            {
                Varying o;
                if (_Surface > 1.5 && _Surface < 2.5)
                {
                    float breeze = sin(v.vertex.z * .64 + v.vertex.x * .39 + _EchoesWeather.x * 1.66);
                    float leaf = saturate(v.vertex.y * .86);
                    v.vertex.x += leaf * (breeze * .075 + _EchoesWeather.y * .48);
                    v.vertex.y -= leaf * (breeze * .028 + _EchoesWeather.y * .17);
                }
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color.rgb * _Tint.rgb;
                TRANSFER_SHADOW(o)
                return o;
            }

            float h(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p,p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(lerp(h(i),h(i+float3(1,0,0)),f.x),
                                 lerp(h(i+float3(0,1,0)),h(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(h(i+float3(0,0,1)),h(i+float3(1,0,1)),f.x),
                                 lerp(h(i+float3(0,1,1)),h(i+float3(1,1,1)),f.x),f.y),f.z);
            }

            float4 frag(Varying i, fixed side : VFACE) : SV_Target
            {
                if (_Surface > 3.5 && _EchoesTerrainSink.w > .5)
                {
                    float2 fromMouth = i.world.xz - _EchoesTerrainSink.xy;
                    clip(dot(fromMouth,fromMouth) - _EchoesTerrainSink.z);
                }
                float3 n = normalize(i.normal);
                if (_Surface > 1.5 && _Surface < 2.5 && side < 0) n=-n;
                float3 eye = _WorldSpaceCameraPos - i.world;
                float range = length(eye);
                float3 lightDir = normalize(_EchoesKey.xyz);
                float shadow = lerp(.38, 1, SHADOW_ATTENUATION(i));
                float3 color = i.color;
                bool emissive = _Surface > 2.5 && _Surface < 3.5;
                if (!emissive)
                {
                    float3 p=i.world;
                    if (_Surface > .5 && _Surface < 1.5) p *= float3(4.1,.51,4.1);
                    float pattern = .69*noise(p*2.43)+.24*noise(p*7.21)+.07*noise(p*21.7);
                    float scratch = noise(p*14);
                    float pores = smoothstep(.44,.73,scratch);
                    color *= .74+.53*pattern + pores*.081;
                    if (_Grain.x>.01)
                    {
                        float3 q=i.world*_Grain.y;
                        if (_Surface>.5 && _Surface<1.5) q.y*=.63;
                        float3 w=pow(abs(n),4);
                        w/=max(.001,w.x+w.y+w.z);
                        float3 grain=tex2D(_GrainTex,q.zy).rgb*w.x+tex2D(_GrainTex,q.xz).rgb*w.y+
                            tex2D(_GrainTex,q.xy).rgb*w.z;
                        float relief=dot(grain,float3(.216,.715,.069));
                        color=lerp(color,color*.43+grain*.63,_Grain.x);
                        float3 dx=ddx(i.world), dy=ddy(i.world);
                        float3 tangent=cross(dy,n), bitangent=cross(n,dx);
                        float factor=dot(dx,tangent);
                        if (abs(factor)>.000025)
                            n=normalize(n-((tangent*ddx(relief)+bitangent*ddy(relief))/factor)*.044*_Grain.x);
                        pores=smoothstep(.21,.44,relief);
                    }
                    if (_Surface < .5)
                    {
                        float moss = smoothstep(.48,.74,noise(p*.64))*saturate(n.y)*_EchoesWeather.w*(1-.65*_EchoesWeather.z);
                        color = lerp(color,color * float3(.73,1.22,.61),moss);
                    }
                    float lit = saturate(dot(n,lightDir));
                    float warm = lit * shadow * _EchoesKey.w;
                    float rim = pow(saturate(1-dot(n,eye/max(range,.05))),3.0);
                    float damp = pow(saturate(dot(n,normalize(lightDir+eye/max(range,.05)))),24);
                    float3 pool = _EchoesPool.xyz-i.world;
                    float localized = _EchoesPool.w / (1 + dot(pool,pool)*.80);
                    color *= _EchoesAmbience.rgb * (1.15 + .27*n.y) + warm * float3(1.16,.93,.68);
                    color += _EchoesPoolColor.rgb * localized * (.10+.12*saturate(dot(n,normalize(pool+.0001))));
                    color += damp*.115 * warm * (.25+.75*pores) + rim * (.024+.031*lit) * _EchoesAir.rgb;
                    if (_Surface > 1.5 && _Surface < 2.5) color += i.color * (.32 * max(0,-dot(n,lightDir)) + .25*warm);
                    float3 track=i.world-_EchoesFootfall.xyz;
                    color*=1-_EchoesFootfall.w*exp(-dot(track.xz,track.xz)*3.7-abs(track.y)*4)*saturate(n.y);
                }
                else color *= (.65 + 2.55*_Glow) * (.78 + .22*abs(dot(n,eye/max(range,.05))));
                float fog = 1-exp(-max(0,range-1.3)*_EchoesAir.w);
                return float4(lerp(color,_EchoesAir.rgb,fog*(emissive ? .37 : .94)),1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}
