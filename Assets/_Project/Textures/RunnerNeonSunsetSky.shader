Shader "Runner/Neon Sunset Sky"
{
    Properties
    {
        _TopColor ("Upper sky", Color) = (0.06, 0.55, 0.88, 1)
        _HorizonColor ("Horizon", Color) = (0.48, 0.80, 0.94, 1)
        _GroundColor ("Lower haze", Color) = (0.72, 0.86, 0.90, 1)
        [HDR] _SunColor ("Sun glow", Color) = (1.7, 1.35, 0.82, 1)
        _CloudColor ("Cloud color", Color) = (1.0, 0.96, 0.86, 1)
        _CloudAmount ("Cloud amount", Range(0,1)) = 0.54
        _SunDirection ("Sun direction", Vector) = (-0.38, 0.10, 0.92, 0)
        _SunSize ("Sun size", Range(0.0005, 0.08)) = 0.0012
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float4 _SunColor;
            float4 _CloudColor;
            float4 _SunDirection;
            float _SunSize;
            float _CloudAmount;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + 1), f.x), f.y);
            }

            float cloudNoise(float2 p)
            {
                float n = noise21(p) * 0.58;
                n += noise21(p * 2.03 + 7.1) * 0.28;
                n += noise21(p * 4.07 + 19.7) * 0.14;
                return n;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float skyHeight = saturate(direction.y * 1.20 + 0.24);
                float horizonBand = smoothstep(0.02, 0.86, skyHeight);
                float3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, horizonBand);

                float belowHorizon = smoothstep(-0.30, 0.02, direction.y);
                color = lerp(_GroundColor.rgb, color, belowHorizon);

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunDot = dot(direction, sunDirection);
                float sunDisc = smoothstep(1.0 - _SunSize - 0.010, 1.0 - _SunSize + 0.010, sunDot);
                float sunHalo = smoothstep(1.0 - _SunSize * 4.5, 1.0 - _SunSize * 0.18, sunDot) * 0.12;
                color += _SunColor.rgb * (sunDisc + sunHalo);

                // Broad, soft cloud islands give the campus depth without a
                // panorama texture or expensive volumetrics.
                float2 cloudUV = float2(atan2(direction.x, direction.z) * 1.25,
                                        direction.y * 3.2);
                float cloudField = cloudNoise(cloudUV * 1.55);
                float cloudMask = smoothstep(0.66 - _CloudAmount * 0.18, 0.82, cloudField);
                cloudMask *= smoothstep(0.03, 0.18, direction.y) * (1.0 - smoothstep(0.55, 0.88, direction.y));
                color = lerp(color, _CloudColor.rgb, cloudMask * 0.88);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
