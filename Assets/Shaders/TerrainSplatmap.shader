Shader "ZoneForge/TerrainSplatmap"
{
    Properties
    {
        _GrassTex  ("Grass Texture",  2D) = "white" {}
        _DirtTex   ("Dirt Texture",   2D) = "white" {}
        _StoneTex  ("Stone Texture",  2D) = "white" {}
        _RavineTex ("Ravine Texture", 2D) = "black" {}
        _SplatTex  ("Splat Map",      2D) = "red"   {}
        _TileScale ("Tile Scale", Float) = 4.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 splatUV    : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldXZ     : TEXCOORD0;
                float2 splatUV     : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
            };

            TEXTURE2D(_GrassTex);  SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex);   SAMPLER(sampler_DirtTex);
            TEXTURE2D(_StoneTex);  SAMPLER(sampler_StoneTex);
            TEXTURE2D(_RavineTex); SAMPLER(sampler_RavineTex);
            TEXTURE2D(_SplatTex);  SAMPLER(sampler_SplatTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassTex_ST;
                float  _TileScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXZ = worldPos.xz / _TileScale;
                OUT.splatUV = IN.splatUV;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 splat = SAMPLE_TEXTURE2D(_SplatTex, sampler_SplatTex, IN.splatUV);

                half4 grass  = SAMPLE_TEXTURE2D(_GrassTex,  sampler_GrassTex,  IN.worldXZ);
                half4 dirt   = SAMPLE_TEXTURE2D(_DirtTex,   sampler_DirtTex,   IN.worldXZ);
                half4 stone  = SAMPLE_TEXTURE2D(_StoneTex,  sampler_StoneTex,  IN.worldXZ);
                half4 ravine = SAMPLE_TEXTURE2D(_RavineTex, sampler_RavineTex, IN.worldXZ);

                half3 albedo = grass.rgb  * splat.r
                             + dirt.rgb   * splat.g
                             + stone.rgb  * splat.b
                             + ravine.rgb * splat.a;

                half3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(N, mainLight.direction));
                half3 lighting = mainLight.color.rgb * NdotL + SampleSH(N);

                return half4(albedo * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
