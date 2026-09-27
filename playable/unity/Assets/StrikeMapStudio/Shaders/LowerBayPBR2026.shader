Shader "LowerBay/PBR 2026"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Base color (sRGB)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _SurfaceMap ("Roughness / glTF metallic-roughness", 2D) = "white" {}
        _OcclusionMap ("Ambient occlusion", 2D) = "white" {}
        _Packed ("glTF G roughness / B metallic", Float) = 0
        _Metallic ("Metallic multiplier", Range(0,1)) = 0
        _RoughnessScale ("Roughness multiplier", Range(0.1,2)) = 1
        _Glossiness ("Fallback smoothness", Range(0,1)) = 0
        _Wetness ("Patchy floor moisture", Range(0,1)) = 0
        _Weather ("Wall grime", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Face culling", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull [_Cull]
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _SurfaceMap, _OcclusionMap;
        fixed4 _Color, _EmissionColor;
        half _BumpScale, _Packed, _Metallic, _RoughnessScale, _Wetness, _Weather;
        struct Input { float2 uv_MainTex; float3 worldPos; float facing : VFACE; };
        float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise21(float2 p)
        {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                        lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 color=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
            half3 surface=tex2D(_SurfaceMap,IN.uv_MainTex).rgb;
            half rough=lerp(surface.r,surface.g,_Packed)*_RoughnessScale;
            float patch=noise21(IN.worldPos.xz*.55)+noise21(IN.worldPos.xz*1.8)*.25;
            half wet=_Wetness*smoothstep(.55,.95,patch);
            half grime=_Weather*(.13+ .3*saturate(1-IN.worldPos.y*.85))*noise21(IN.worldPos.xz*5.7);
            o.Albedo=color*(1-grime)*(1-wet*.2);
            o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale)*(IN.facing>=0?1:-1);
            o.Metallic=_Metallic*lerp(1,surface.b,_Packed);
            o.Smoothness=lerp(1-saturate(rough),.87,wet);
            o.Occlusion=tex2D(_OcclusionMap,IN.uv_MainTex).r;
            o.Emission=_EmissionColor.rgb;
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
