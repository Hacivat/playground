Shader "WaterPipeSample/Transparent Flowing Water"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.72, 1.0, 1.0)
        _Opacity ("Opacity", Range(0, 1)) = 0.38
        _FlowNoise ("Flow Noise", 2D) = "gray" {}
        _DetailNoise ("Detail Noise", 2D) = "gray" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BubbleMask ("Bubble Mask", 2D) = "black" {}
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _DetailFlowSpeed ("Detail Flow Speed", Float) = -0.18
        _NormalFlowSpeedA ("Normal Flow Speed A", Float) = 0.25
        _NormalFlowSpeedB ("Normal Flow Speed B", Float) = -0.14
        _FlowTiling ("Flow Tiling", Float) = 1.0
        _DetailTiling ("Detail Tiling", Float) = 3.0
        _NormalTilingA ("Normal Tiling A", Float) = 1.4
        _NormalTilingB ("Normal Tiling B", Float) = 3.3
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5
        _DistortionStrength ("Distortion Strength", Range(0, 0.2)) = 0.035
        _BubbleThreshold ("Bubble Threshold", Range(0, 1)) = 0.74
        _BubbleIntensity ("Bubble Intensity", Range(0, 1)) = 0.15
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.25
        _EmissionIntensity ("Emission Intensity", Range(0, 2)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FlowNoise);
            SAMPLER(sampler_FlowNoise);
            TEXTURE2D(_DetailNoise);
            SAMPLER(sampler_DetailNoise);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_BubbleMask);
            SAMPLER(sampler_BubbleMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                half _FlowSpeed;
                half _DetailFlowSpeed;
                half _NormalFlowSpeedA;
                half _NormalFlowSpeedB;
                half _FlowTiling;
                half _DetailTiling;
                half _NormalTilingA;
                half _NormalTilingB;
                half _NormalStrength;
                half _DistortionStrength;
                half _BubbleThreshold;
                half _BubbleIntensity;
                half _FresnelPower;
                half _FresnelIntensity;
                half _EmissionIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 UnpackNormalStrength(half4 packedNormal, half strength)
            {
                half3 normalTS = packedNormal.xyz * 2.0h - 1.0h;
                normalTS.xy *= strength;
                normalTS.z = sqrt(saturate(1.0h - dot(normalTS.xy, normalTS.xy)));
                return normalTS;
            }

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
                half3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.tangentWS = half4(tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float time = _Time.y;
                float2 flowUv = float2(input.uv.x, input.uv.y + time * _FlowSpeed) * _FlowTiling;
                half flowNoise = SAMPLE_TEXTURE2D(_FlowNoise, sampler_FlowNoise, flowUv).r;

                float2 distortedUv = input.uv + (flowNoise - 0.5h) * _DistortionStrength;
                float2 detailUv = float2(distortedUv.x, distortedUv.y + time * _DetailFlowSpeed) * _DetailTiling;
                half detailNoise = SAMPLE_TEXTURE2D(_DetailNoise, sampler_DetailNoise, detailUv).r;

                float2 normalUvA = float2(input.uv.x, input.uv.y + time * _NormalFlowSpeedA) * _NormalTilingA;
                float2 normalUvB = float2(input.uv.x + 0.37, input.uv.y + time * _NormalFlowSpeedB) * _NormalTilingB;
                half3 normalA = UnpackNormalStrength(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUvA), _NormalStrength);
                half3 normalB = UnpackNormalStrength(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUvB), _NormalStrength * 0.65h);
                half3 normalTS = normalize(half3(normalA.xy + normalB.xy, normalA.z * normalB.z));

                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = normalize(cross(normalWS, tangentWS) * input.tangentWS.w);
                half3 bumpedNormalWS = normalize(tangentWS * normalTS.x + bitangentWS * normalTS.y + normalWS * normalTS.z);

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(bumpedNormalWS, viewDirWS)), _FresnelPower) * _FresnelIntensity;

                float2 bubbleUv = float2(input.uv.x, input.uv.y + time * (_FlowSpeed * 0.7h)) * float2(1.0, 1.35);
                half bubbleMask = SAMPLE_TEXTURE2D(_BubbleMask, sampler_BubbleMask, bubbleUv).r;
                half bubbles = smoothstep(_BubbleThreshold, 1.0h, bubbleMask) * _BubbleIntensity;

                half variation = lerp(0.82h, 1.18h, flowNoise) + (detailNoise - 0.5h) * 0.16h;
                half3 color = _BaseColor.rgb * variation;
                color += fresnel.xxx + bubbles.xxx + _BaseColor.rgb * _EmissionIntensity * 0.2h;

                half alpha = saturate(_Opacity * (0.86h + flowNoise * 0.18h + detailNoise * 0.08h) + fresnel * 0.22h + bubbles * 0.15h);
                alpha = min(alpha, 0.68h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
