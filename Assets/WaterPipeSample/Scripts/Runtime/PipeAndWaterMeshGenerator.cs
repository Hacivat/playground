using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterPipeSample
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PipeAndWaterMeshGenerator : MonoBehaviour
    {
        public const string GeneratedPipeName = "Generated_Pipe";
        public const string GeneratedWaterName = "Generated_Water";

        [Header("Path")]
        public List<Transform> controlPoints = new List<Transform>();
        public bool closedPath;

        [Header("Geometry")]
        [Min(0.01f)] public float pipeOuterRadius = 0.5f;
        [Min(0.001f)] public float pipeWallThickness = 0.05f;
        [Min(0.01f)] public float waterRadius = 0.42f;
        [Range(6, 96)] public int radialSegments = 24;
        [Range(2, 64)] public int samplesPerSegment = 12;
        [Min(0.01f)] public float uvTilingPerMeter = 1.0f;
        public bool generateEndCaps = true;

        [Header("Materials")]
        public Material waterMaterial;
        public Material pipeMaterial;

        [Header("Editor")]
        public bool autoRegenerateInEditor = true;

        private bool validationQueued;

        public GameObject PipeObject => transform.Find(GeneratedPipeName)?.gameObject;
        public GameObject WaterObject => transform.Find(GeneratedWaterName)?.gameObject;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && autoRegenerateInEditor && !validationQueued)
            {
                validationQueued = true;
                EditorApplication.delayCall += DelayedEditorRegenerate;
            }
#endif
        }

        [ContextMenu("Generate Meshes")]
        public void GenerateMeshes()
        {
            if (!TryCollectControlPoints(out List<Vector3> points))
            {
                Debug.LogWarning("PipeAndWaterMeshGenerator needs at least four control points for a smooth editable sample path.", this);
                return;
            }

            ClampInvalidValues();
            ClearGeneratedMeshes();

            List<PathSample> samples = BuildPathSamples(points);
            if (samples.Count < 2)
            {
                Debug.LogWarning("PipeAndWaterMeshGenerator could not create enough path samples.", this);
                return;
            }

            Mesh pipeMesh = BuildPipeMesh(samples);
            Mesh waterMesh = BuildWaterMesh(samples);

            GameObject pipe = CreateGeneratedChild(GeneratedPipeName);
            MeshFilter pipeFilter = pipe.AddComponent<MeshFilter>();
            MeshRenderer pipeRenderer = pipe.AddComponent<MeshRenderer>();
            pipeFilter.sharedMesh = pipeMesh;
            pipeRenderer.sharedMaterial = pipeMaterial;

            GameObject water = CreateGeneratedChild(GeneratedWaterName);
            MeshFilter waterFilter = water.AddComponent<MeshFilter>();
            MeshRenderer waterRenderer = water.AddComponent<MeshRenderer>();
            waterFilter.sharedMesh = waterMesh;
            waterRenderer.sharedMaterial = waterMaterial;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(pipe);
                EditorUtility.SetDirty(water);
            }
#endif
        }

        [ContextMenu("Clear Generated Meshes")]
        public void ClearGeneratedMeshes()
        {
            DestroyGeneratedChild(GeneratedPipeName);
            DestroyGeneratedChild(GeneratedWaterName);
        }

        [ContextMenu("Create Default L-Shaped Path")]
        public void CreateDefaultLShapedPath()
        {
            for (int i = controlPoints.Count - 1; i >= 0; i--)
            {
                Transform point = controlPoints[i];
                if (point != null && point.parent == transform && point.name.StartsWith("CP_", StringComparison.Ordinal))
                {
                    DestroyUnityObject(point.gameObject);
                }
            }

            controlPoints.Clear();

            Vector3[] positions =
            {
                new Vector3(-3.2f, 0.0f, 0.0f),
                new Vector3(-1.4f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.35f, 0.0f),
                new Vector3(0.0f, 2.8f, 0.0f),
                new Vector3(1.2f, 4.0f, 0.0f),
                new Vector3(3.2f, 4.0f, 0.0f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject point = new GameObject($"CP_{i:00}");
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(point, "Create Pipe Control Point");
                }
#endif
                point.transform.SetParent(transform, false);
                point.transform.localPosition = positions[i];
                controlPoints.Add(point.transform);
            }

            if (autoRegenerateInEditor || Application.isPlaying)
            {
                GenerateMeshes();
            }
        }

        private void OnValidate()
        {
            ClampInvalidValues();

#if UNITY_EDITOR
            if (!Application.isPlaying && autoRegenerateInEditor && isActiveAndEnabled && !validationQueued)
            {
                validationQueued = true;
                EditorApplication.delayCall += DelayedEditorRegenerate;
            }
#endif
        }

#if UNITY_EDITOR
        private void DelayedEditorRegenerate()
        {
            validationQueued = false;
            if (this == null || Application.isPlaying || !autoRegenerateInEditor || !isActiveAndEnabled)
            {
                return;
            }

            AssignDefaultMaterialsIfMissing();
            GenerateMeshes();
        }

        private void AssignDefaultMaterialsIfMissing()
        {
            if (waterMaterial == null)
            {
                waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterPipeSample/Generated/Materials/M_TransparentWater.mat");
            }

            if (pipeMaterial == null)
            {
                pipeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterPipeSample/Generated/Materials/M_TransparentPipe.mat");
            }
        }
#endif

        private void ClampInvalidValues()
        {
            radialSegments = Mathf.Clamp(radialSegments, 6, 96);
            samplesPerSegment = Mathf.Clamp(samplesPerSegment, 2, 64);
            uvTilingPerMeter = Mathf.Max(0.01f, uvTilingPerMeter);
            pipeOuterRadius = Mathf.Max(0.01f, pipeOuterRadius);
            pipeWallThickness = Mathf.Clamp(pipeWallThickness, 0.001f, pipeOuterRadius * 0.9f);

            float pipeInnerRadius = pipeOuterRadius - pipeWallThickness;
            float maxWaterRadius = Mathf.Max(0.005f, pipeInnerRadius - 0.025f);
            if (waterRadius >= pipeInnerRadius)
            {
                Debug.LogWarning("Water radius was clamped to leave a small transparent sorting gap inside the pipe wall.", this);
            }

            waterRadius = Mathf.Clamp(waterRadius, 0.005f, maxWaterRadius);
        }

        private bool TryCollectControlPoints(out List<Vector3> points)
        {
            points = new List<Vector3>(controlPoints.Count);
            foreach (Transform point in controlPoints)
            {
                if (point != null)
                {
                    points.Add(transform.InverseTransformPoint(point.position));
                }
            }

            return points.Count >= 4;
        }

        private List<PathSample> BuildPathSamples(IReadOnlyList<Vector3> points)
        {
            int segmentCount = closedPath ? points.Count : points.Count - 1;
            int ringCount = closedPath ? segmentCount * samplesPerSegment : segmentCount * samplesPerSegment + 1;
            List<PathSample> samples = new List<PathSample>(ringCount);

            for (int segment = 0; segment < segmentCount; segment++)
            {
                for (int step = 0; step < samplesPerSegment; step++)
                {
                    float t = step / (float)samplesPerSegment;
                    samples.Add(new PathSample { position = CatmullRom(points, segment, t) });
                }
            }

            if (!closedPath)
            {
                samples.Add(new PathSample { position = points[points.Count - 1] });
            }

            float distance = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                int previous = i == 0 ? (closedPath ? samples.Count - 1 : 0) : i - 1;
                int next = i == samples.Count - 1 ? (closedPath ? 0 : samples.Count - 1) : i + 1;

                if (i > 0)
                {
                    distance += Vector3.Distance(samples[i - 1].position, samples[i].position);
                }

                Vector3 tangent = samples[next].position - samples[previous].position;
                if (tangent.sqrMagnitude < 0.000001f)
                {
                    tangent = Vector3.forward;
                }

                PathSample sample = samples[i];
                sample.distance = distance;
                sample.tangent = tangent.normalized;
                samples[i] = sample;
            }

            ApplyParallelTransportFrames(samples);
            return samples;
        }

        private Vector3 CatmullRom(IReadOnlyList<Vector3> points, int segment, float t)
        {
            Vector3 p0 = GetControlPoint(points, segment - 1);
            Vector3 p1 = GetControlPoint(points, segment);
            Vector3 p2 = GetControlPoint(points, segment + 1);
            Vector3 p3 = GetControlPoint(points, segment + 2);
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 GetControlPoint(IReadOnlyList<Vector3> points, int index)
        {
            if (closedPath)
            {
                int count = points.Count;
                return points[(index % count + count) % count];
            }

            return points[Mathf.Clamp(index, 0, points.Count - 1)];
        }

        /// <summary>
        /// Parallel transport rotates the previous frame onto the next tangent, preventing the random flips
        /// that happen when a fixed up vector becomes parallel to the path.
        /// </summary>
        private static void ApplyParallelTransportFrames(List<PathSample> samples)
        {
            Vector3 tangent = samples[0].tangent;
            Vector3 normal = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
            normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    Quaternion rotation = Quaternion.FromToRotation(samples[i - 1].tangent, samples[i].tangent);
                    normal = rotation * normal;
                    normal = Vector3.ProjectOnPlane(normal, samples[i].tangent).normalized;
                    if (normal.sqrMagnitude < 0.000001f)
                    {
                        normal = Vector3.ProjectOnPlane(Vector3.up, samples[i].tangent).normalized;
                    }

                    binormal = Vector3.Cross(samples[i].tangent, normal).normalized;
                }

                PathSample sample = samples[i];
                sample.normal = normal;
                sample.binormal = binormal;
                samples[i] = sample;
            }
        }

        private Mesh BuildPipeMesh(IReadOnlyList<PathSample> samples)
        {
            MeshData data = new MeshData();
            AddTubeSurface(data, samples, pipeOuterRadius, false, true);
            AddTubeSurface(data, samples, pipeOuterRadius - pipeWallThickness, true, false);

            if (generateEndCaps && !closedPath)
            {
                AddPipeEndRing(data, samples[0], -samples[0].tangent);
                AddPipeEndRing(data, samples[samples.Count - 1], samples[samples.Count - 1].tangent);
            }

            return data.ToMesh("Generated Transparent Pipe Mesh");
        }

        private Mesh BuildWaterMesh(IReadOnlyList<PathSample> samples)
        {
            MeshData data = new MeshData();
            AddTubeSurface(data, samples, waterRadius, false, true);

            if (generateEndCaps && !closedPath)
            {
                AddWaterCap(data, samples[0], true);
                AddWaterCap(data, samples[samples.Count - 1], false);
            }

            return data.ToMesh("Generated Transparent Water Mesh");
        }

        private void AddTubeSurface(MeshData data, IReadOnlyList<PathSample> samples, float radius, bool flipWinding, bool outwardNormals)
        {
            int firstVertex = data.positions.Count;
            int verticesPerRing = radialSegments + 1;
            int ringCount = samples.Count;

            for (int ring = 0; ring < ringCount; ring++)
            {
                PathSample sample = samples[ring];
                for (int radial = 0; radial <= radialSegments; radial++)
                {
                    float angle = radial / (float)radialSegments * Mathf.PI * 2f;
                    Vector3 radialDirection = Mathf.Cos(angle) * sample.normal + Mathf.Sin(angle) * sample.binormal;
                    Vector3 circumferentialTangent = (-Mathf.Sin(angle) * sample.normal + Mathf.Cos(angle) * sample.binormal).normalized;

                    data.positions.Add(sample.position + radialDirection * radius);
                    data.normals.Add(outwardNormals ? radialDirection : -radialDirection);
                    data.tangents.Add(new Vector4(circumferentialTangent.x, circumferentialTangent.y, circumferentialTangent.z, 1f));
                    data.uvs.Add(new Vector2(radial / (float)radialSegments, sample.distance * uvTilingPerMeter));
                }
            }

            int segmentCount = closedPath ? ringCount : ringCount - 1;
            for (int ring = 0; ring < segmentCount; ring++)
            {
                int nextRing = ring == ringCount - 1 ? 0 : ring + 1;
                for (int radial = 0; radial < radialSegments; radial++)
                {
                    int a = firstVertex + ring * verticesPerRing + radial;
                    int b = firstVertex + ring * verticesPerRing + radial + 1;
                    int c = firstVertex + nextRing * verticesPerRing + radial;
                    int d = firstVertex + nextRing * verticesPerRing + radial + 1;

                    if (flipWinding)
                    {
                        data.AddTriangle(a, c, b);
                        data.AddTriangle(b, c, d);
                    }
                    else
                    {
                        data.AddTriangle(a, b, c);
                        data.AddTriangle(b, d, c);
                    }
                }
            }
        }

        private void AddPipeEndRing(MeshData data, PathSample sample, Vector3 capNormal)
        {
            int first = data.positions.Count;
            float innerRadius = pipeOuterRadius - pipeWallThickness;

            for (int radial = 0; radial <= radialSegments; radial++)
            {
                float angle = radial / (float)radialSegments * Mathf.PI * 2f;
                Vector3 radialDirection = Mathf.Cos(angle) * sample.normal + Mathf.Sin(angle) * sample.binormal;
                Vector3 tangent = (-Mathf.Sin(angle) * sample.normal + Mathf.Cos(angle) * sample.binormal).normalized;

                data.positions.Add(sample.position + radialDirection * pipeOuterRadius);
                data.positions.Add(sample.position + radialDirection * innerRadius);
                data.normals.Add(capNormal);
                data.normals.Add(capNormal);
                data.tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                data.tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                data.uvs.Add(new Vector2(radial / (float)radialSegments, sample.distance * uvTilingPerMeter));
                data.uvs.Add(new Vector2(radial / (float)radialSegments, sample.distance * uvTilingPerMeter));
            }

            for (int radial = 0; radial < radialSegments; radial++)
            {
                int outerA = first + radial * 2;
                int innerA = outerA + 1;
                int outerB = first + (radial + 1) * 2;
                int innerB = outerB + 1;
                data.AddTriangle(outerA, outerB, innerA);
                data.AddTriangle(innerA, outerB, innerB);
            }
        }

        private void AddWaterCap(MeshData data, PathSample sample, bool startCap)
        {
            int center = data.positions.Count;
            Vector3 capNormal = startCap ? -sample.tangent : sample.tangent;
            data.positions.Add(sample.position);
            data.normals.Add(capNormal);
            data.tangents.Add(new Vector4(sample.normal.x, sample.normal.y, sample.normal.z, 1f));
            data.uvs.Add(new Vector2(0.5f, sample.distance * uvTilingPerMeter));

            int first = data.positions.Count;
            for (int radial = 0; radial <= radialSegments; radial++)
            {
                float angle = radial / (float)radialSegments * Mathf.PI * 2f;
                Vector3 radialDirection = Mathf.Cos(angle) * sample.normal + Mathf.Sin(angle) * sample.binormal;
                Vector3 tangent = (-Mathf.Sin(angle) * sample.normal + Mathf.Cos(angle) * sample.binormal).normalized;
                data.positions.Add(sample.position + radialDirection * waterRadius);
                data.normals.Add(capNormal);
                data.tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                data.uvs.Add(new Vector2(radial / (float)radialSegments, sample.distance * uvTilingPerMeter));
            }

            for (int radial = 0; radial < radialSegments; radial++)
            {
                int a = first + radial;
                int b = first + radial + 1;
                if (startCap)
                {
                    data.AddTriangle(center, a, b);
                }
                else
                {
                    data.AddTriangle(center, b, a);
                }
            }
        }

        private GameObject CreateGeneratedChild(string childName)
        {
            GameObject child = new GameObject(childName);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(child, "Generate Pipe Meshes");
            }
#endif
            child.transform.SetParent(transform, false);
            return child;
        }

        private void DestroyGeneratedChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
            {
                DestroyUnityObject(child.gameObject);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private struct PathSample
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 normal;
            public Vector3 binormal;
            public float distance;
        }

        private sealed class MeshData
        {
            public readonly List<Vector3> positions = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector4> tangents = new List<Vector4>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void AddTriangle(int a, int b, int c)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            public Mesh ToMesh(string meshName)
            {
                Mesh mesh = new Mesh { name = meshName };
                if (positions.Count > 65535)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
