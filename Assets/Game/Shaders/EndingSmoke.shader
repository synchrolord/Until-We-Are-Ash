Shader "BURN/Ending Smoke"
{
    Properties
    {
        _MainTex ("Mist", 2D) = "white" {}
        _SmokeTint ("Smoke Tint", Color) = (0.2,0.18,0.16,1)
        _SmokeOpacity ("Smoke Opacity", Range(0,1)) = 0.7
        _Vignette ("Vignette", Range(0,1)) = 0.65
        _Tiling ("Tiling", Vector) = (3.5,2,0,0)
        _Scroll ("Scroll", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _SmokeTint, _Tiling, _Scroll;
            float _SmokeOpacity, _Vignette;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                //frac repeats this texture without changing its shared import settings
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    frac(input.uv * _Tiling.xy + _Scroll.xy)).a;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    frac(input.uv * _Tiling.xy * 0.73 - _Scroll.xy * 0.6 + 0.37)).a;
                float smoke = saturate((a + b - a * b) * 2.5) * _SmokeOpacity;
                float edge = smoothstep(0.25, 0.72, length((input.uv - 0.5) * float2(1.2, 1)));
                float vignette = edge * _Vignette;
                float alpha = smoke + vignette - smoke * vignette;
                return half4(_SmokeTint.rgb * smoke * (1 - vignette) / max(alpha, 0.001), alpha);
            }
            ENDHLSL
        }
    }
}
