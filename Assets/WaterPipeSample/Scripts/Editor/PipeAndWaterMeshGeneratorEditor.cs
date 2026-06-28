using UnityEditor;
using UnityEngine;

namespace WaterPipeSample.Editor
{
    [CustomEditor(typeof(PipeAndWaterMeshGenerator))]
    public sealed class PipeAndWaterMeshGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8);
            PipeAndWaterMeshGenerator generator = (PipeAndWaterMeshGenerator)target;

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Meshes"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Water Pipe Meshes");
                    generator.GenerateMeshes();
                    MarkSceneDirty(generator);
                }

                if (GUILayout.Button("Clear Generated Meshes"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Water Pipe Meshes");
                    generator.ClearGeneratedMeshes();
                    MarkSceneDirty(generator);
                }
            }

            if (GUILayout.Button("Create Default L-Shaped Path"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Create Default Water Pipe Path");
                generator.CreateDefaultLShapedPath();
                MarkSceneDirty(generator);
            }
        }

        private static void MarkSceneDirty(PipeAndWaterMeshGenerator generator)
        {
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }
}
