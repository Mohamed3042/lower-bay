Shader "Hidden/LowerBay/Film"
{
    Properties { _MainTex ("Image", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Exposure;
            float3 aces(float3 x) { return saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14)); }
            fixed4 frag(v2f_img i):SV_Target
            {
                float3 c=tex2D(_MainTex,i.uv).rgb;
                float3 bloom=0;
                const float2 offsets[8]={float2(-2,-2),float2(0,-3),float2(2,-2),float2(-3,0),
                    float2(3,0),float2(-2,2),float2(0,3),float2(2,2)};
                for(int n=0;n<8;n++) bloom+=max(0,tex2D(_MainTex,i.uv+offsets[n]*_MainTex_TexelSize.xy*2).rgb-1.2);
                c=aces((c+bloom*.008)*_Exposure);
                float2 vignette=i.uv*(1-i.uv.yx);
                c*=.95+.05*saturate(pow(max(.0001,16*vignette.x*vignette.y),.2));
                return float4(c,1);
            }
            ENDCG
        }
    }
}
