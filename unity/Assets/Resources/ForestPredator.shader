Shader "Hidden/Echoes/ForestPredator"
{
    Properties
    {
        _Projection ("Independent projection", Float) = 0
        _StageLight ("Main scene brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual Blend Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Projection, _StageLight;
            float4 _EchoesAir, _EchoesKey;
            struct Vertex { float4 position : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 color : COLOR;
            };

            Varyings vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.world = mul(unity_ObjectToWorld,input.position).xyz;
                output.color = input.color.rgb;
                return output;
            }

            float4 frag(Varyings input,fixed face : VFACE) : SV_Target
            {
                float3 normal = normalize(input.normal) * (face < 0 ? -1 : 1);
                float3 eye = normalize(_WorldSpaceCameraPos-input.world);
                float3 key = normalize(lerp(_EchoesKey.xyz,float3(-.48,.74,-.53),.39+.61*_Projection));
                float diffuse = saturate(dot(key,normal));
                float rim = pow(1-saturate(dot(normal,eye)),2.4);
                float gloss = pow(saturate(dot(normal,normalize(eye+key))),26);
                float grain = sin(dot(input.world,float3(113.7,121.4,-79.3))) *
                    sin(dot(input.world,float3(-78.5,88.5,76.3)));
                float3 tone = input.color * (.90-.25*_Projection+diffuse*.61+grain*.041);
                tone += rim * lerp(float3(.095,.139,.156),float3(.049,.072,.080),_Projection) +
                    gloss * float3(.103,.092,.068);
                float amber = saturate((input.color.r-input.color.g-.22)*1.89) *
                    smoothstep(.58,.93,input.color.r);
                tone += amber * float3(.52,.19,.061);
                float fog = 1-exp(-max(0,distance(_WorldSpaceCameraPos,input.world)-1.3)*_EchoesAir.w);
                // Predators are foreground performers in the main scene and opaque in the portrait.
                return float4(lerp(tone,_EchoesAir.rgb,fog*(1-_Projection)*.68)*_StageLight,_Projection);
            }
            ENDCG
        }
    }
}
