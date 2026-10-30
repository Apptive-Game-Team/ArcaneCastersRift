// Ground of the first field. The splat map comes from the same layout grid as the
// colliders (ArcaneField), so painted paths, water and ruin floor sit exactly on the
// walkable and blocked cells. r = dirt path, g = water, b = flagstone, a = forest shade.
Shader "ArcaneCasters/GroundSplat" {
    Properties {
        _Splat ("Splat (r dirt, g water, b stone, a forest)", 2D) = "black" {}
        _Grass ("Grass", 2D) = "white" {}
        _Dirt ("Dirt", 2D) = "white" {}
        _Water ("Water", 2D) = "white" {}
        _Stone ("Stone", 2D) = "white" {}
        _Tiling ("World units per texture repeat", Float) = 4
    }
    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass {
            Tags { "LightMode" = "Universal2D" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Splat); SAMPLER(sampler_Splat);
            TEXTURE2D(_Grass); SAMPLER(sampler_Grass);
            TEXTURE2D(_Dirt); SAMPLER(sampler_Dirt);
            TEXTURE2D(_Water); SAMPLER(sampler_Water);
            TEXTURE2D(_Stone); SAMPLER(sampler_Stone);
            CBUFFER_START(UnityPerMaterial)
                float4 _Splat_ST;
                float _Tiling;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings Vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.world = TransformObjectToWorld(input.positionOS.xyz).xy;
                return output;
            }

            half Luma(half3 color) { return dot(color, half3(0.3, 0.55, 0.15)); }

            half4 Frag(Varyings input) : SV_Target {
                float4 splat = SAMPLE_TEXTURE2D(_Splat, sampler_Splat, input.uv);
                float2 w = input.world / _Tiling;
                half3 grass = SAMPLE_TEXTURE2D(_Grass, sampler_Grass, w).rgb;
                half3 dirt = SAMPLE_TEXTURE2D(_Dirt, sampler_Dirt, w + float2(0.37, 0.11)).rgb;
                half3 stone = SAMPLE_TEXTURE2D(_Stone, sampler_Stone, w * 0.9).rgb;
                float2 flow = float2(_Time.y * 0.035, -_Time.y * 0.02);
                half3 water = (SAMPLE_TEXTURE2D(_Water, sampler_Water, w + flow).rgb
                             + SAMPLE_TEXTURE2D(_Water, sampler_Water, w * 1.27 - flow * 0.6 + 0.5).rgb) * 0.5;

                // Height-aware blending keeps pebbles and stones crisp at the path edge.
                float dirtMask = smoothstep(0.38, 0.62, splat.r + (Luma(dirt) - 0.5) * 0.45);
                half3 color = lerp(grass, dirt, dirtMask);
                float stoneMask = smoothstep(0.36, 0.6, splat.b + (Luma(stone) - 0.5) * 0.35);
                color = lerp(color, stone, stoneMask);

                // Shoreline: a dark bank just outside the water, slightly deeper water inside.
                float shore = smoothstep(0.44, 0.56, splat.g);
                float bank = saturate(1.0 - abs(splat.g - 0.4) * 5.0) * (1.0 - shore);
                color *= 1.0 - 0.38 * bank;
                half3 deep = water * lerp(1.0, 0.78, saturate((splat.g - 0.6) * 2.5));
                color = lerp(color, deep, shore);

                // Forest floor under the canopy.
                color *= lerp(1.0, 0.52, splat.a);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
