Shader "Hidden/Sokoban/BackgroundProjection"
{
    Properties
    {
        _BackgroundCube("Selected background", Cube) = "black" {}
        _BackgroundRotation("Rotation", Float) = 0
        _BackgroundExposure("Visible exposure", Float) = 10
    }

    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    TEXTURECUBE(_BackgroundCube);
    SAMPLER(sampler_BackgroundCube);
    float _BackgroundRotation;
    float _BackgroundExposure;

    float3 Palette(float height)
    {
        float angle = radians(_BackgroundRotation);
        float2 axis = float2(cos(angle), sin(angle));
        float3 color = SAMPLE_TEXTURECUBE_LOD(_BackgroundCube, sampler_BackgroundCube, float3(axis.x, height, axis.y), 7).rgb;
        color += SAMPLE_TEXTURECUBE_LOD(_BackgroundCube, sampler_BackgroundCube, float3(-axis.x, height, -axis.y), 7).rgb;
        color += SAMPLE_TEXTURECUBE_LOD(_BackgroundCube, sampler_BackgroundCube, float3(axis.y, height, -axis.x), 7).rgb;
        color += SAMPLE_TEXTURECUBE_LOD(_BackgroundCube, sampler_BackgroundCube, float3(-axis.y, height, axis.x), 7).rgb;
        return color * 0.25;
    }

    float4 Background(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        // Draw only untouched background pixels, before transparents and post-processing.
        // Opaque board/actors, their lighting and all depth values remain unchanged.
        if (LoadCameraDepth(varyings.positionCS.xy) != UNITY_RAW_FAR_CLIP_VALUE)
        {
            discard;
        }
        float height = smoothstep(0.0, 1.0, varyings.positionCS.y * _ScreenSize.w);
        float3 color = lerp(Palette(0.0), Palette(1.0), height);
        return float4(color * exp2(_BackgroundExposure) * GetCurrentExposureMultiplier(), 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Background"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off
            HLSLPROGRAM
            #pragma fragment Background
            ENDHLSL
        }
    }
    Fallback Off
}
