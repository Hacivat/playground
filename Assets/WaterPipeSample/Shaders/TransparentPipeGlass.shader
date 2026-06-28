Shader "WaterPipeSample/Transparent Pipe Glass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.78, 0.94, 1.0, 1.0)
        _Opacity ("Opacity", Range(0, 1)) = 0.16
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2.2
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.7
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                half _FresnelPower;
                half _FresnelIntensity;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _FresnelPower) * _FresnelIntensity;
                half3 color = _BaseColor.rgb + fresnel.xxx * lerp(0.35h, 0.85h, _Smoothness);
                half alpha = saturate(_Opacity + fresnel * 0.34h);
                return half4(color, min(alpha, 0.52h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
