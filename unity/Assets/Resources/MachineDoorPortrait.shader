Shader "Hidden/Echoes/MachineDoorPortrait"
{
    Properties
    {
        _Tint ("Local machine color", Color) = (1,1,1,1)
        _Art ("Glow, opacity, water and time", Vector) = (0,1,0,0)
    }
    SubShader
    {
        CGINCLUDE
        #pragma target 3.0
        #include "UnityCG.cginc"
        float4 _Tint, _Art;
        struct Vertex { float4 position : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
        struct Varyings
        {
            float4 position : SV_POSITION;
            float3 normal : TEXCOORD0;
            float3 world : TEXCOORD1;
            float3 local : TEXCOORD2;
            float4 screen : TEXCOORD3;
            float3 color : COLOR;
        };
        Varyings vert(Vertex input)
        {
            Varyings output;
            output.position = UnityObjectToClipPos(input.position);
            output.screen = ComputeScreenPos(output.position);
            output.normal = UnityObjectToWorldNormal(input.normal);
            output.world = mul(unity_ObjectToWorld,input.position).xyz;
            output.local = input.position.xyz;
            output.color = input.color.rgb * _Tint.rgb;
            return output;
        }
        float4 frag(Varyings input, fixed face : VFACE) : SV_Target
        {
            if (_Art.z > .5 && _Art.z < 1.5)
            {
                float x = input.local.x;
                float y = input.local.y;
                float crest = .425 + .030 * sin(x*25 - _Art.w*4.2) + .016 * sin(x*43+_Art.w*6.5);
                float depth = crest - y;
                float edge = 1 - smoothstep(.32,.50,abs(x));
                float foam = exp(-abs(depth-.012)*68) * (.67+.33*sin(x*47+_Art.w*4));
                float strips = pow(saturate(.44+.39*sin(depth*69-x*11+_Art.w*3.7)),5);
                float under = smoothstep(-.012,.033,depth);
                float vertical = smoothstep(-.48,-.24,y);
                float3 tint = lerp(float3(.036,.075,.201),input.color*(.35+.42*_Art.x),foam+.23*strips);
                return float4(saturate(tint), _Art.y*edge*under*vertical*(.62+.30*foam+.10*strips));
            }
            float3 normal = normalize(input.normal) * (face < 0 ? -1 : 1);
            float3 eye = normalize(_WorldSpaceCameraPos-input.world);
            float3 key = normalize(float3(-.52,.69,-.50));
            float diffuse = saturate(dot(key,normal));
            float rim = pow(1-saturate(dot(eye,normal)),2.0);
            float shine = pow(saturate(dot(normal,normalize(eye+key))),26);
            float brushed = sin(dot(input.world,float3(141,-176,137)))*.048;
            float3 metal = input.color * (.58+.59*diffuse+brushed+_Art.x*.76);
            metal += rim*float3(.047,.081,.095)+shine*float3(.094,.111,.125);
            float coverage = _Art.y;
            if (_Art.z > 1.5)
            {
                float screenX = input.screen.x / input.screen.w;
                coverage *= smoothstep(.004,.092,min(screenX,1-screenX));
                clip(coverage-.001);
            }
            return float4(saturate(metal), coverage);
        }
        ENDCG
        Pass
        {
            Cull Off ZTest LEqual ZWrite On Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Cull Off ZTest LEqual ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}
