Shader "LowerBay/WornSurface" {
 Properties {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Surface",2D)="white" {}
  _Metallic("Metallic",Range(0,1))=0
  _Glossiness("Smoothness",Range(0,1))=.25
  _Relief("Micro relief",Range(0,2))=.7
  _NeutralMetal("Brushed neutral metal",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;
  float4 _MainTex_TexelSize;
  fixed4 _Color;
  half _Metallic,_Glossiness,_Relief,_NeutralMetal;
  struct Input {float2 uv_MainTex;};
  void surf(Input IN,inout SurfaceOutputStandard o) {
   fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
   float value=dot(c,float3(.299,.587,.114));
   float x=dot(tex2D(_MainTex,IN.uv_MainTex+float2(_MainTex_TexelSize.x,0)).rgb,float3(.299,.587,.114));
   float y=dot(tex2D(_MainTex,IN.uv_MainTex+float2(0,_MainTex_TexelSize.y)).rgb,float3(.299,.587,.114));
   o.Normal=normalize(float3((value-x)*_Relief,(value-y)*_Relief,1));
   // Metal01 is a rusty door atlas, not aluminum train panel artwork.
   // Reuse its wear value only for roughness; geometry supplies the actual ribs.
   float brushPhase=IN.uv_MainTex.y*900;
   float brush=.5+.5*sin(brushPhase);
   float aa=saturate(1-fwidth(brushPhase));
   brush=lerp(.5,brush,aa);
   float3 neutral=_Color.rgb*lerp(.86,.98,brush);
   o.Albedo=lerp(c*_Color.rgb,neutral,_NeutralMetal);o.Metallic=_Metallic;
   o.Normal=normalize(lerp(o.Normal,float3(0,(brush-.5)*.025,1),_NeutralMetal));
   o.Smoothness=_Glossiness*lerp(.65,1,value);o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
