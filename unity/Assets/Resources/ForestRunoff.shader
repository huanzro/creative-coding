Shader "Hidden/Echoes/ForestRunoff"
{
    Properties
    {
        _CurrentTime ("Surface time", Float) = 0
        _ReflectionCenter ("Broken well reflection", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _CurrentTime;
            float4 _EchoesAir, _EchoesSkyHigh, _EchoesKey, _ReflectionCenter;

            struct AppData
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct Varying
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 color : COLOR;
            };

            Varying vert(AppData input)
            {
                Varying result;
                result.position = UnityObjectToClipPos(input.vertex);
                result.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                result.color = input.color;
                return result;
            }

            float4 frag(Varying input) : SV_Target
            {
                float2 xz = input.world.xz;
                float phase = xz.y * 8.8 + cos(xz.x * 5.9 + _CurrentTime * .34) * 1.1 - _CurrentTime * 2.2;
                float interference = xz.x * 13.1 - xz.y * 4.5 + _CurrentTime * .86;
                float3 normal = normalize(float3(.065 * cos(interference) + .027 * cos(phase), 1, .048 * cos(phase)));
                float3 eye = _WorldSpaceCameraPos - input.world;
                float range = max(length(eye), .01);
                float3 direction = eye / range;
                float grazing = pow(1 - saturate(dot(normal, direction)), 3.2);
                float sheen = pow(saturate(dot(normal, normalize(direction + _EchoesKey.xyz))), 44);
                float streaks = .5 + .5 * sin(2.9 * xz.x - 1.6 * xz.y + cos(phase) * .13 + _CurrentTime * .31);
                float3 oil = lerp(float3(.12,.20,.13), float3(.18,.08,.23), smoothstep(.14,.88,streaks));
                float3 color = lerp(input.color.rgb * .38 + oil * .49, _EchoesSkyHigh.xyz * .79 + oil * .20, grazing * .83);
                color += (.10 + .27 * sheen) * float3(.22,.34,.28) * grazing;

                float2 ghost = (xz - _ReflectionCenter.xy) / (float2(1.5, 2.84) * max(_ReflectionCenter.z, .1));
                ghost.x += .062 * sin(phase) + direction.x * .08;
                ghost.y += .016 * cos(interference);
                float reflectedFrame = saturate(1 - abs(length(ghost) - .83) * 18);
                reflectedFrame *= smoothstep(-.92,-.45,ghost.y) * (.68 + .32 * sin(phase + .7));
                color += float3(.13,.32,.33) * reflectedFrame * _ReflectionCenter.w * (.2 + grazing * .8);

                float fog = 1 - exp(-max(0, range - 1) * _EchoesAir.w);
                return float4(lerp(color, _EchoesAir.xyz, fog * .9), saturate(input.color.a * (.67 + grazing * .29)));
            }
            ENDCG
        }
    }
}
