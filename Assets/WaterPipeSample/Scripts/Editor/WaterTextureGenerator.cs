using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WaterPipeSample.Editor
{
    public static class WaterTextureGenerator
    {
        public const int DefaultSeed = 14321;
        public const string TextureFolder = "Assets/WaterPipeSample/Generated/Textures";

        [MenuItem("Tools/Water Pipe Sample/Regenerate Textures")]
        public static void GenerateDefaultTextures()
        {
            GenerateAll(DefaultSeed);
        }

        public static TextureSet GenerateAll(int seed)
        {
            Directory.CreateDirectory(TextureFolder);

            Texture2D flow = CreateGrayscaleTexture("FlowNoise", seed, 512, 4, 0.55f);
            Texture2D detail = CreateGrayscaleTexture("DetailNoise", seed + 97, 512, 9, 0.42f);
            Texture2D normal = CreateNormalTexture("WaterNormal", seed + 211, 512);
            Texture2D bubbles = CreateBubbleMask("BubbleMask", seed + 409, 512);

            string flowPath = SavePng(flow, "FlowNoise.png");
            string detailPath = SavePng(detail, "DetailNoise.png");
            string normalPath = SavePng(normal, "WaterNormal.png");
            string bubblesPath = SavePng(bubbles, "BubbleMask.png");

            UnityEngine.Object.DestroyImmediate(flow);
            UnityEngine.Object.DestroyImmediate(detail);
            UnityEngine.Object.DestroyImmediate(normal);
            UnityEngine.Object.DestroyImmediate(bubbles);

            AssetDatabase.ImportAsset(flowPath);
            AssetDatabase.ImportAsset(detailPath);
            AssetDatabase.ImportAsset(normalPath);
            AssetDatabase.ImportAsset(bubblesPath);

            ConfigureTexture(flowPath, false, TextureImporterType.Default);
            ConfigureTexture(detailPath, false, TextureImporterType.Default);
            ConfigureTexture(normalPath, false, TextureImporterType.NormalMap);
            ConfigureTexture(bubblesPath, false, TextureImporterType.Default);

            AssetDatabase.SaveAssets();

            return new TextureSet
            {
                flowNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(flowPath),
                detailNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(detailPath),
                normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                bubbleMask = AssetDatabase.LoadAssetAtPath<Texture2D>(bubblesPath)
            };
        }

        private static string SavePng(Texture2D texture, string fileName)
        {
            string path = $"{TextureFolder}/{fileName}";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return path;
        }

        private static void ConfigureTexture(string path, bool sRgb, TextureImporterType importerType)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"Could not configure texture importer for {path}.");
                return;
            }

            importer.textureType = importerType;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = sRgb;
            importer.SaveAndReimport();
        }

        private static Texture2D CreateGrayscaleTexture(string textureName, int seed, int size, int baseFrequency, float contrast)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float value = 0.0f;
                    float amplitude = 0.5f;
                    float amplitudeSum = 0.0f;

                    for (int octave = 0; octave < 5; octave++)
                    {
                        int frequency = baseFrequency << octave;
                        value += PeriodicValueNoise(u, v, frequency, seed + octave * 37) * amplitude;
                        amplitudeSum += amplitude;
                        amplitude *= 0.52f;
                    }

                    value /= amplitudeSum;
                    value = Mathf.Clamp01((value - 0.5f) * (1.0f + contrast) + 0.5f);
                    pixels[y * size + x] = new Color(value, value, value, 1.0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateNormalTexture(string textureName, int seed, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            float[] heights = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float h = PeriodicValueNoise(u, v, 12, seed) * 0.6f + PeriodicValueNoise(u, v, 28, seed + 53) * 0.4f;
                    heights[y * size + x] = h;
                }
            }

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int yPrev = (y - 1 + size) % size;
                int yNext = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xPrev = (x - 1 + size) % size;
                    int xNext = (x + 1) % size;
                    float dx = heights[y * size + xNext] - heights[y * size + xPrev];
                    float dy = heights[yNext * size + x] - heights[yPrev * size + x];
                    Vector3 normal = new Vector3(-dx * 4.0f, -dy * 4.0f, 1.0f).normalized;
                    pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1.0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateBubbleMask(string textureName, int seed, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float value = 0.0f;

                    for (int i = 0; i < 38; i++)
                    {
                        float centerU = Hash01(seed, i * 7 + 1);
                        float centerV = Hash01(seed, i * 7 + 2);
                        float width = Mathf.Lerp(0.006f, 0.025f, Hash01(seed, i * 7 + 3));
                        float length = Mathf.Lerp(0.05f, 0.22f, Hash01(seed, i * 7 + 4));
                        float strength = Mathf.Lerp(0.35f, 1.0f, Hash01(seed, i * 7 + 5));

                        float du = WrappedDistance(u, centerU);
                        float dv = WrappedDistance(v, centerV);
                        float streak = Mathf.Exp(-(du * du) / (width * width) - (dv * dv) / (length * length));
                        value = Mathf.Max(value, streak * strength);
                    }

                    float speckle = PeriodicValueNoise(u, v, 64, seed + 901);
                    value *= Mathf.SmoothStep(0.35f, 0.88f, speckle);
                    value = Mathf.Pow(Mathf.Clamp01(value), 1.45f);
                    pixels[y * size + x] = new Color(value, value, value, 1.0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float PeriodicValueNoise(float u, float v, int frequency, int seed)
        {
            float x = u * frequency;
            float y = v * frequency;
            int x0 = Mathf.FloorToInt(x) % frequency;
            int y0 = Mathf.FloorToInt(y) % frequency;
            int x1 = (x0 + 1) % frequency;
            int y1 = (y0 + 1) % frequency;
            float tx = Smooth01(x - Mathf.Floor(x));
            float ty = Smooth01(y - Mathf.Floor(y));

            float a = HashGrid(seed, x0, y0);
            float b = HashGrid(seed, x1, y0);
            float c = HashGrid(seed, x0, y1);
            float d = HashGrid(seed, x1, y1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Smooth01(float t)
        {
            return t * t * (3.0f - 2.0f * t);
        }

        private static float HashGrid(int seed, int x, int y)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 374761393u;
                h ^= (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0x00FFFFFFu) / 16777215.0f;
            }
        }

        private static float Hash01(int seed, int index)
        {
            unchecked
            {
                uint h = (uint)(seed + index * 1013904223);
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215.0f;
            }
        }

        private static float WrappedDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1.0f - d);
        }

        public struct TextureSet
        {
            public Texture2D flowNoise;
            public Texture2D detailNoise;
            public Texture2D normalMap;
            public Texture2D bubbleMask;
        }
    }
}
