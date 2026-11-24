Shader "Hidden/Echoes/Spirit3D"
{
    Properties
    {
        _Tint ("Material color and emission", Vector) = (1,1,1,0)
        _SceneLight ("Surrounding light", Vector) = (1,.82,.55,.2)
        _ContactLight ("Fingertip light and strength", Vector) = (0,0,0,0)
        _Fabric ("Embroidered silk", Float) = 0
        _Face ("Face detail mask", Float) = 0
        _Expression ("Lids, expression and gaze", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite On ZTest LEqual Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Tint;
            float4 _SceneLight;
            float4 _ContactLight;
            float _Fabric, _Face;
            float4 _Expression;
            struct Vertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float shade : TEXCOORD2;
                float2 cloth : TEXCOORD3;
                float3 local : TEXCOORD4;
            };

            Varyings vert(Vertex input)
            {
                Varyings output;
                float4 world = mul(unity_ObjectToWorld, input.position);
                output.position = UnityObjectToClipPos(input.position);
                output.world = world.xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.shade = input.color.r;
                output.cloth = input.uv;
                output.local = input.position.xyz;
                return output;
            }

            float ellipse(float2 p, float2 radius)
            {
                return 1 - smoothstep(.89, 1.04, length(p / radius));
            }

            float3 complexion(float3 skin, float3 local)
            {
                float surface = smoothstep(.40, .66, -local.z);
                float2 p = local.xy;
                float side = p.x < 0 ? -1 : 1;
                float sideX = abs(p.x);
                float2 cheek = float2((sideX - .57) / .15, (p.y + .30) / .104);
                float blush = exp(-dot(cheek, cheek) * 1.2);
                float3 color = lerp(skin, float3(.83,.47,.43), blush * .37 * surface);

                float y = p.y + .068 - (sideX - .44) * .16;
                float x = sideX - .44;
                float lid = lerp(.011, .131, saturate(_Expression.x * (1 + .09 * _Expression.y)));
                float lash = ellipse(float2(x, y), float2(.254, lid + .014)) * surface;
                float sclera = ellipse(float2(x, y + .013), float2(.230, lid)) * smoothstep(.09,.27,_Expression.x);
                float fromTop = 1 - smoothstep(-.8, .8, y / max(.02, lid));
                float3 eye = lerp(float3(.72,.62,.57), float3(.91,.87,.79), fromTop);
                float2 pupil = float2(p.x - side * .43 + clamp(_Expression.z * .0014,-.045,.045),
                                      p.y + .058 + clamp(_Expression.w * .0017,-.035,.035));
                float irisR = length(pupil * float2(1, 1.04));
                float iris = 1 - smoothstep(.113,.130,irisR);
                float rays = .5 + .5 * sin(18 * atan2(pupil.y + .0001, pupil.x + .0001) + 21 * irisR);
                float3 goldIris = lerp(float3(.26,.18,.088), float3(.72,.49,.19),
                    (1 - smoothstep(.093,.132,irisR)) * (.46 + .31 * rays + .19 * fromTop));
                goldIris = lerp(goldIris, float3(.097,.056,.045), 1 - smoothstep(.043,.054,irisR));
                float gleam = ellipse(pupil - float2(.035,.032), float2(.024,.021)) +
                    .58 * ellipse(pupil + float2(.047,.032), float2(.011,.012));
                eye = lerp(eye, lerp(goldIris, float3(.97,.92,.79), saturate(gleam)), iris);
                color = lerp(color, lerp(float3(.24,.115,.102), eye, sclera), lash);

                float eyebrow = .187 + .032 * cos(x * 10) + _Expression.y * (.041 - .17 * x);
                float brow = (1 - smoothstep(.012,.027,abs(p.y - eyebrow))) *
                    (1 - smoothstep(.145,.234,abs(x)));
                color = lerp(color, float3(.43,.29,.25), brow * .81 * surface);
                for (int freckle = 0; freckle < 3; ++freckle)
                {
                    float2 fleck = float2(sideX - .55 - freckle * .054,
                                         p.y + .225 - .021 * cos(freckle * 3.14));
                    color = lerp(color, float3(.56,.36,.29), ellipse(fleck, float2(.014,.011)) * surface * .62);
                }
                float lipY = p.y + .454 - p.x * p.x * 1.17;
                float mouth = ellipse(float2(p.x, lipY), float2(.136 - .022 * _Expression.y,.024 + .029 * _Expression.y));
                float lip = ellipse(float2(p.x, lipY + .025), float2(.10,.022));
                color = lerp(color, float3(.72,.39,.39), lip * .54 * surface);
                return lerp(color, float3(.48,.219,.248), mouth * .86 * surface);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 eye = normalize(float3(0, 1.16, -5.6) - input.world);
                float3 key = normalize(float3(.47, .83, -.43));
                float3 fill = normalize(float3(-.76, .18, .34));
                float diffuse = saturate(dot(normal, key));
                float cool = saturate(dot(normal, fill));
                float rim = pow(1 - saturate(dot(normal, eye)), 2.2);
                float sheen = pow(saturate(dot(normal, normalize(key + eye))), 31);
                float scattered = saturate(dot(normal, key) * .5 + .44);
                float grain = 1 + .023 * sin(218 * input.world.x + 81 * input.world.y) *
                    sin(143 * input.world.z - 164 * input.world.y);
                float3 materialColor = _Tint.rgb;
                if (_Face > .5) materialColor = complexion(materialColor, input.local);
                if (_Fabric > .1)
                {
                    float y = input.cloth.y;
                    float x = frac(input.cloth.x * 9) - .5;
                    float hem = 1 - smoothstep(.005, .016, abs(y - .962));
                    float stitch = 1 - smoothstep(.004, .013, abs(y - .914));
                    float petals = abs(abs(x) - .23 * sin(saturate((y - .61) / .29) * 3.14159));
                    float sprig = (1 - smoothstep(.012, .037, petals)) * smoothstep(.61,.67,y) * (1 - smoothstep(.87,.92,y));
                    float gold = max(hem * .88, max(stitch * .40, sprig * .42));
                    materialColor = lerp(materialColor, float3(.74,.49,.235), gold * _Fabric);
                    grain *= 1 + .025 * cos(input.cloth.x * 1120) * cos(y * 841);
                }
                float shade = lerp(input.shade, 1, step(.1, _Fabric));
                float3 color = materialColor * shade *
                    (float3(.41,.43,.50) + diffuse * float3(.61,.55,.47) + cool * float3(.19,.24,.29));
                color *= grain;
                color += (rim * .24 + scattered * .36) * _SceneLight.rgb * _SceneLight.a;
                color += rim * float3(.042,.069,.077) + sheen * float3(.056,.054,.047) + _Tint.rgb * _Tint.a * .38;
                float3 toFly = _ContactLight.xyz - input.world;
                float flyFalloff = rcp(1 + dot(toFly, toFly) * 17);
                float bounce = .40 + .60 * saturate(dot(normal, normalize(toFly + .0001)));
                color += _SceneLight.rgb * (flyFalloff * bounce * _ContactLight.w * .42);
                return float4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
