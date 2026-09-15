// Diffuse Detail — restored from DummyShaderTextExporter stub.
// Unity legacy Diffuse Detail: base texture multiplied by detail texture at 2x contrast.
// Used by MonkeyIsland bridge, pillars, stone walls, shipwreck, tunnel, and other
// architectural props that need a tiling detail overlay for close-up surface quality.
Shader "Diffuse Detail" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _Detail ("Detail (RGB)", 2D) = "gray" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 200

    CGPROGRAM
    #pragma surface surf Lambert
    #pragma target 2.5

    sampler2D _MainTex;
    sampler2D _Detail;
    fixed4 _Color;

    struct Input {
        float2 uv_MainTex;
        float2 uv_Detail;
    };

    void surf(Input IN, inout SurfaceOutput o) {
        fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
        fixed4 detail = tex2D(_Detail, IN.uv_Detail);
        o.Albedo = tex.rgb * detail.rgb * 2.0 * _Color.rgb;
        o.Alpha = tex.a * _Color.a;
    }
    ENDCG
}
Fallback "Diffuse Detail"
}
