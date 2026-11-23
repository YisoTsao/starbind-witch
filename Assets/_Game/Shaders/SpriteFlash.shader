// Sprite shader for characters and enemies: alpha-blended sprite, optional white hit flash
// (_Flash, set per renderer through a MaterialPropertyBlock) and a global scene dim (_SBDim) so
// unlit characters match the scene's ambient light. URP 2D Renderer pass.
// SpriteRenderer.flipX / flipY are applied in the vertex shader (unity_SpriteProps), as in URP's own
// sprite shaders: without it flipX has no visible effect (left-facing characters looked right).
Shader "Starbind/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _Flash ("Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
                half _Flash;
            CBUFFER_END
            half _SBDim;

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pos = UnityFlipSprite(i.positionOS.xyz, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(pos);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb *= 1.0h - _SBDim;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _Flash);
                return c;
            }
            ENDHLSL
        }
    }
}
