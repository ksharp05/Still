Shader "STILL/Particles"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _Shape ("Shape: glow / shard / ring / ribbon", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Shape;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Tint; o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                float a;
                if (_Shape < .5) a = pow(saturate(1-r), 2.4);
                else if (_Shape < 1.5) a = 1-smoothstep(.55,.95,abs(p.x)+abs(p.y));
                else if (_Shape < 2.5) a = (1-smoothstep(.055,.085,abs(r-.80))) * .8;
                else a = pow(saturate(1-abs(p.y)),1.3);
                return half4(i.color.rgb, saturate(i.color.a * a));
            }
            ENDHLSL
        }
    }
}
