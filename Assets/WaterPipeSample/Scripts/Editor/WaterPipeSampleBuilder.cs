using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterPipeSample.Editor
{
    public static class WaterPipeSampleBuilder
    {
        private const string RootFolder = "Assets/WaterPipeSample";
        private const string MaterialFolder = RootFolder + "/Generated/Materials";
        private const string MeshFolder = RootFolder + "/Generated/Meshes";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ScenePath = SceneFolder + "/WaterPipeSample.unity";
        private const string PrefabPath = PrefabFolder + "/WaterPipeSample.prefab";

        [MenuItem("Tools/Water Pipe Sample/Create Or Rebuild Sample")]
        public static void CreateOrRebuildSample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolders();

            WaterTextureGenerator.TextureSet textures = WaterTextureGenerator.GenerateAll(WaterTextureGenerator.DefaultSeed);
            Material waterMaterial = CreateWaterMaterial(textures);
            Material pipeMaterial = CreatePipeMaterial();
            Material floorMaterial = CreateFloorMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WaterPipeSample";

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.045f);

            GameObject root = new GameObject("WaterPipeSample");
            Undo.RegisterCreatedObjectUndo(root, "Create Water Pipe Sample");

            PipeAndWaterMeshGenerator generator = root.AddComponent<PipeAndWaterMeshGenerator>();
            generator.pipeOuterRadius = 0.5f;
            generator.pipeWallThickness = 0.05f;
            generator.waterRadius = 0.42f;
            generator.radialSegments = 24;
            generator.samplesPerSegment = 12;
            generator.uvTilingPerMeter = 1.0f;
            generator.generateEndCaps = true;
            generator.waterMaterial = waterMaterial;
            generator.pipeMaterial = pipeMaterial;
            generator.autoRegenerateInEditor = false;
            generator.CreateDefaultLShapedPath();
            generator.GenerateMeshes();

            SaveGeneratedMeshes(generator);

            CreateFloor(floorMaterial);
            CreateLighting();
            CreateCamera(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = root;
            Debug.Log($"Water Pipe Sample rebuilt at {ScenePath}.");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(SceneFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(WaterTextureGenerator.TextureFolder);
        }

        private static Material CreateWaterMaterial(WaterTextureGenerator.TextureSet textures)
        {
            Shader shader = Shader.Find("WaterPipeSample/Transparent Flowing Water");
            if (shader == null)
            {
                throw new MissingReferenceException("Could not find WaterPipeSample/Transparent Flowing Water shader.");
            }

            Material material = LoadOrCreateMaterial(MaterialFolder + "/M_TransparentWater.mat", shader);
            material.SetColor("_BaseColor", new Color(0.015f, 0.38f, 0.72f, 1.0f));
            material.SetFloat("_Opacity", 0.42f);
            material.SetTexture("_FlowNoise", textures.flowNoise);
            material.SetTexture("_DetailNoise", textures.detailNoise);
            material.SetTexture("_NormalMap", textures.normalMap);
            material.SetTexture("_BubbleMask", textures.bubbleMask);
            material.SetFloat("_FlowSpeed", 0.35f);
            material.SetFloat("_DetailFlowSpeed", -0.18f);
            material.SetFloat("_NormalFlowSpeedA", 0.25f);
            material.SetFloat("_NormalFlowSpeedB", -0.14f);
            material.SetFloat("_FlowTiling", 1.0f);
            material.SetFloat("_DetailTiling", 3.0f);
            material.SetFloat("_NormalTilingA", 1.4f);
            material.SetFloat("_NormalTilingB", 3.3f);
            material.SetFloat("_NormalStrength", 0.5f);
            material.SetFloat("_DistortionStrength", 0.035f);
            material.SetFloat("_BubbleThreshold", 0.74f);
            material.SetFloat("_BubbleIntensity", 0.15f);
            material.SetFloat("_FresnelPower", 3.0f);
            material.SetFloat("_FresnelIntensity", 0.18f);
            material.SetFloat("_EmissionIntensity", 0.06f);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreatePipeMaterial()
        {
            Shader shader = Shader.Find("WaterPipeSample/Transparent Pipe Glass");
            if (shader == null)
            {
                throw new MissingReferenceException("Could not find WaterPipeSample/Transparent Pipe Glass shader.");
            }

            Material material = LoadOrCreateMaterial(MaterialFolder + "/M_TransparentPipe.mat", shader);
            material.SetColor("_BaseColor", new Color(0.78f, 0.94f, 1.0f, 1.0f));
            material.SetFloat("_Opacity", 0.16f);
            material.SetFloat("_FresnelPower", 2.2f);
            material.SetFloat("_FresnelIntensity", 0.7f);
            material.SetFloat("_Smoothness", 0.92f);
            material.renderQueue = 3100;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateFloorMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            Material material = LoadOrCreateMaterial(MaterialFolder + "/M_NeutralFloor.mat", shader);
            material.color = new Color(0.18f, 0.18f, 0.18f, 1.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            return material;
        }

        private static void SaveGeneratedMeshes(PipeAndWaterMeshGenerator generator)
        {
            SaveGeneratedMesh(generator.PipeObject, MeshFolder + "/Generated_Pipe.asset");
            SaveGeneratedMesh(generator.WaterObject, MeshFolder + "/Generated_Water.asset");
        }

        private static void SaveGeneratedMesh(GameObject meshObject, string path)
        {
            if (meshObject == null)
            {
                return;
            }

            MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Mesh mesh = meshFilter.sharedMesh;
            AssetDatabase.CreateAsset(mesh, path);
            meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorUtility.SetDirty(meshObject);
        }

        private static void CreateFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(floor, "Create Water Pipe Floor");
            floor.name = "Neutral_Floor";
            floor.transform.position = new Vector3(0.0f, -0.65f, 0.0f);
            floor.transform.localScale = new Vector3(8.5f, 0.05f, 5.5f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Directional_Key_Light");
            Undo.RegisterCreatedObjectUndo(sun, "Create Water Pipe Lighting");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 2.0f;
            sunLight.color = new Color(0.9f, 0.96f, 1.0f);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            CreatePointLight("Cyan_Rim_Light", new Vector3(-2.7f, 2.3f, -2.2f), new Color(0.25f, 0.82f, 1.0f), 3.2f);
            CreatePointLight("Soft_Warm_Fill", new Vector3(2.8f, 2.5f, 1.8f), new Color(1.0f, 0.86f, 0.66f), 1.2f);
        }

        private static void CreatePointLight(string name, Vector3 position, Color color, float intensity)
        {
            GameObject lightObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Water Pipe Point Light");
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7.0f;
            light.intensity = intensity;
            light.color = color;
        }

        private static void CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Water Pipe Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(5.2f, 3.15f, -9.2f);
            cameraObject.transform.LookAt(new Vector3(0.0f, 1.85f, 0.0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.035f, 1.0f);
            camera.fieldOfView = 56f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 100f;

            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = true;
        }
    }
}
