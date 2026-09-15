Shader "LowerBay/FootageSquareTile" {
 Properties {
  _Color("Tile colour",Color)=(0.65,0.63,0.57,1)
  _MainTex("Surface wear",2D)="white" {}
  _TilesPerMetre("Tiles per metre",Float)=2.5
  _Glossiness("Smoothness",Range(0,1))=0.22
 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;
  fixed4 _Color;
  float _TilesPerMetre;
  half _Glossiness;
  struct Input { float2 uv_MainTex; };
  float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float2 cell=IN.uv_MainTex*_TilesPerMetre;
   float2 edge=min(frac(cell),1-frac(cell));
   float seam=min(edge.x,edge.y);
   float aa=max(fwidth(cell.x),fwidth(cell.y));
   float tile=smoothstep(.012-aa,.012+aa,seam);
   float wear=lerp(.91,1.04,hash21(floor(cell)));
   fixed3 sampled=tex2D(_MainTex,IN.uv_MainTex*.24).rgb;
   float grime=lerp(.8,1.0,dot(sampled,float3(.299,.587,.114)));
   o.Albedo=lerp(_Color.rgb*.30,_Color.rgb*wear*grime,tile);
   float2 local=frac(cell);
   float2 edgeSlope=(smoothstep(.01,.025,local)-smoothstep(.025,.04,local))
                  -(smoothstep(.96,.975,local)-smoothstep(.975,.99,local));
   o.Normal=normalize(float3(-edgeSlope*.18,1));
   o.Metallic=0; o.Smoothness=_Glossiness*tile; o.Occlusion=lerp(.8,1,tile); o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
