Shader "Hidden/PuzzleGame/DimensionRipple"
{
    Properties
    {
        [HideInInspector] _DimensionRippleProgress ("Ripple Progress", Float) = 0
        [HideInInspector] _DimensionRippleDirection ("Ripple Direction", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Dimension Ripple"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DimensionRippleProgress;
                float _DimensionRippleDirection;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = DYNAMIC_SCALING_REMOVE_SCALEBIAS(input.texcoord);
                float2 aspect = float2(_ScreenParams.x / max(_ScreenParams.y, 1.0), 1.0);
                float2 radialOffset = (uv - 0.5) * aspect;
                float radius = length(radialOffset);
                float2 radialDirection = radialOffset / max(radius, 0.0001);

                float progress = saturate(_DimensionRippleProgress);
                float lastRadius = length(aspect * 0.5);
                float front = lerp(-0.025, lastRadius + 0.14, progress);
                float distanceFromFront = (radius - front) / 0.15;
                float fade = smoothstep(0.0, 0.12, progress) * (1.0 - smoothstep(0.83, 1.0, progress));
                float band = exp2(-3.4 * distanceFromFront * distanceFromFront) * fade;

                float phase = distanceFromFront * 7.6;
                float displacement = (sin(phase) * 0.020 + sin(phase * 2.0) * 0.003) * band;
                float2 halfPixel = 0.5 / max(GetScaledScreenParams().xy, 1.0);
                float2 refractedUV = clamp(uv + radialDirection / aspect * displacement * _DimensionRippleDirection,
                    halfPixel, 1.0 - halfPixel);
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    DYNAMIC_SCALING_APPLY_SCALEBIAS(refractedUV));

                // A soft light crest makes the same wave readable in dark, flat parts of the scene.
                float crestPhase = cos(phase - 0.45);
                float crest = smoothstep(0.25, 1.0, crestPhase) * band;
                float trough = smoothstep(0.40, 1.0, -crestPhase) * band;
                half3 tint = _DimensionRippleDirection > 0.0 ? half3(0.25, 0.86, 1.0) : half3(1.0, 0.65, 0.32);
                scene.rgb = scene.rgb * (1.0 - 0.11 * trough) + tint * (0.12 * crest);
                return scene;
            }
            ENDHLSL
        }
    }
}
