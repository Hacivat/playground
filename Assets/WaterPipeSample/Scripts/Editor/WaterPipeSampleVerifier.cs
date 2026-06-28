using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterPipeSample.Editor
{
    public static class WaterPipeSampleVerifier
    {
        [MenuItem("Tools/Water Pipe Sample/Verify Sample")]
        public static void VerifySample()
        {
            var failures = new List<string>();

            CheckAsset<SceneAsset>("Assets/WaterPipeSample/Scenes/WaterPipeSample.unity", failures);
            CheckAsset<GameObject>("Assets/WaterPipeSample/Prefabs/WaterPipeSample.prefab", failures);
            CheckAsset<Shader>("Assets/WaterPipeSample/Shaders/TransparentFlowingWater.shader", failures);
            CheckAsset<Shader>("Assets/WaterPipeSample/Shaders/TransparentPipeGlass.shader", failures);

            Texture2D flow = CheckTexture("Assets/WaterPipeSample/Generated/Textures/FlowNoise.png", false, false, failures);
            Texture2D detail = CheckTexture("Assets/WaterPipeSample/Generated/Textures/DetailNoise.png", false, false, failures);
            Texture2D normal = CheckTexture("Assets/WaterPipeSample/Generated/Textures/WaterNormal.png", true, false, failures);
            Texture2D bubbles = CheckTexture("Assets/WaterPipeSample/Generated/Textures/BubbleMask.png", false, false, failures);

            Material water = CheckAsset<Material>("Assets/WaterPipeSample/Generated/Materials/M_TransparentWater.mat", failures);
            Material pipe = CheckAsset<Material>("Assets/WaterPipeSample/Generated/Materials/M_TransparentPipe.mat", failures);

            if (water != null)
            {
                if (water.renderQueue != 3000) failures.Add("Water material renderQueue is not 3000.");
                if (water.GetTexture("_FlowNoise") != flow) failures.Add("Water material FlowNoise is not assigned.");
                if (water.GetTexture("_DetailNoise") != detail) failures.Add("Water material DetailNoise is not assigned.");
                if (water.GetTexture("_NormalMap") != normal) failures.Add("Water material NormalMap is not assigned.");
                if (water.GetTexture("_BubbleMask") != bubbles) failures.Add("Water material BubbleMask is not assigned.");
                float opacity = water.GetFloat("_Opacity");
                if (opacity < 0.3f || opacity > 0.45f) failures.Add("Water opacity is outside the requested default range.");
            }

            if (pipe != null && pipe.renderQueue != 3100)
            {
                failures.Add("Pipe material renderQueue is not 3100.");
            }

            PipeAndWaterMeshGenerator generator = Object.FindAnyObjectByType<PipeAndWaterMeshGenerator>();
            if (generator == null)
            {
                failures.Add("No PipeAndWaterMeshGenerator found in the active scene.");
            }
            else
            {
                if (generator.PipeObject == null) failures.Add("Generated_Pipe child is missing.");
                if (generator.WaterObject == null) failures.Add("Generated_Water child is missing.");
                CheckGeneratedMesh(generator.PipeObject, "pipe", failures);
                CheckGeneratedMesh(generator.WaterObject, "water", failures);
                CheckWaterLongitudinalUvs(generator.WaterObject, failures);
            }

            if (failures.Count > 0)
            {
                Debug.LogError("Water Pipe Sample verification failed:\n- " + string.Join("\n- ", failures));
                return;
            }

            Debug.Log("Water Pipe Sample verification passed.");
        }

        private static T CheckAsset<T>(string path, List<string> failures) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                failures.Add($"Missing asset: {path}");
            }

            return asset;
        }

        private static Texture2D CheckTexture(string path, bool normalMap, bool sRgb, List<string> failures)
        {
            Texture2D texture = CheckAsset<Texture2D>(path, failures);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (texture == null || importer == null)
            {
                return texture;
            }

            if (texture.width != 512 || texture.height != 512) failures.Add($"{path} is not 512x512.");
            if (importer.wrapMode != TextureWrapMode.Repeat) failures.Add($"{path} wrap mode is not Repeat.");
            if (importer.filterMode != FilterMode.Bilinear) failures.Add($"{path} filter mode is not Bilinear.");
            if (normalMap && importer.textureType != TextureImporterType.NormalMap) failures.Add($"{path} is not imported as a Normal Map.");
            if (!normalMap && importer.sRGBTexture != sRgb) failures.Add($"{path} sRGB setting is incorrect.");
            return texture;
        }

        private static void CheckGeneratedMesh(GameObject target, string label, List<string> failures)
        {
            if (target == null)
            {
                return;
            }

            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null) failures.Add($"Generated {label} mesh is missing.");
            if (renderer == null || renderer.sharedMaterial == null) failures.Add($"Generated {label} material is missing.");
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount <= 0) failures.Add($"Generated {label} mesh has no vertices.");
        }

        private static void CheckWaterLongitudinalUvs(GameObject waterObject, List<string> failures)
        {
            if (waterObject == null)
            {
                return;
            }

            MeshFilter filter = waterObject.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return;
            }

            Vector2[] uvs = filter.sharedMesh.uv;
            if (uvs.Length == 0)
            {
                failures.Add("Water mesh has no UVs.");
                return;
            }

            float minV = float.MaxValue;
            float maxV = float.MinValue;
            for (int i = 0; i < uvs.Length; i++)
            {
                minV = Mathf.Min(minV, uvs[i].y);
                maxV = Mathf.Max(maxV, uvs[i].y);
            }

            if (maxV - minV < 2.0f)
            {
                failures.Add("Water mesh longitudinal UV range is too small; V should represent path distance.");
            }
        }
    }
}
