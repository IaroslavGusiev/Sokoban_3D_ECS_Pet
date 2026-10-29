using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Sokoban.Editor
{
    public static class LegacySkyboxConverter
    {
        private const string Destination = "Assets/_Sokoban/Art/Backgrounds/Cubemaps/";

        // Names describe the legacy skybox's interior faces, not screen-left/right.
        // RenderToCubemap reference checks also establish a vertical flip on EVERY face.
        private static readonly (CubemapFace Face, string Property)[] Faces =
        {
            (CubemapFace.PositiveX, "_LeftTex"),
            (CubemapFace.NegativeX, "_RightTex"),
            (CubemapFace.PositiveY, "_UpTex"),
            (CubemapFace.NegativeY, "_DownTex"),
            (CubemapFace.PositiveZ, "_FrontTex"),
            (CubemapFace.NegativeZ, "_BackTex")
        };

        [MenuItem("Tools/Sokoban/Convert legacy background presets")]
        public static void ConvertFromFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Read-only source Skybox folder", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            ConvertAll(folder);
        }

        public static void ConvertAll(string sourceFolder)
        {
            for (int variant = 1; variant <= 10; variant++)
            {
                string name = "SimpleCumulus_Gradient_" + variant.ToString("00");
                ConvertPreset(Path.Combine(sourceFolder, name, name + ".mat"), Destination + name + ".cubemap");
            }
        }

        public static void ConvertPreset(string sourceMaterialPath, string destinationAssetPath)
        {
            string text = File.ReadAllText(sourceMaterialPath);
            string folder = Path.GetDirectoryName(sourceMaterialPath);
            string[] metadata = Directory.GetFiles(folder, "*.png.meta", SearchOption.AllDirectories);
            var source = new Material(Shader.Find("Skybox/6 Sided"));
            var textures = new List<Texture2D>();
            Cubemap converted = null;

            try
            {
                foreach (var face in Faces)
                {
                    // Resolve the material's actual GUID assignment rather than assuming filenames.
                    Match assignment = Regex.Match(text, @"name: " + face.Property + @"\s+second:\s+m_Texture: \{fileID: \d+, guid: ([a-f0-9]+)");
                    if (assignment.Success == false)
                    {
                        throw new InvalidDataException("Missing legacy texture assignment: " + face.Property);
                    }
                    string guid = assignment.Groups[1].Value;
                    string path = metadata.Single(path => Regex.IsMatch(File.ReadAllText(path), @"(?m)^guid: " + guid + @"\s*$"));
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    textures.Add(texture);
                    if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path.Substring(0, path.Length - 5))) == false)
                    {
                        throw new InvalidDataException("Cannot decode " + path);
                    }
                    source.SetTexture(face.Property, texture);
                }

                converted = CreateCubemap(source, 1024);
                converted.name = Path.GetFileNameWithoutExtension(destinationAssetPath);
                EditorUtility.CompressCubemapTexture(converted, TextureFormat.BC7, TextureCompressionQuality.Best);
                converted.Apply(false, true);

                Cubemap existing = AssetDatabase.LoadAssetAtPath<Cubemap>(destinationAssetPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(converted, destinationAssetPath);
                    converted = null;
                }
                else
                {
                    // Copy data in place: catalog registrations and the asset GUID remain stable.
                    EditorUtility.CopySerialized(converted, existing);
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(converted);
                Object.DestroyImmediate(source);
                foreach (Texture2D texture in textures)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        // Returns an owned, readable sRGB cubemap. Source tint/exposure/rotation remain
        // presentation metadata; this conversion preserves the texture directions and colors.
        public static Cubemap CreateCubemap(Material source, int size)
        {
            var cube = new Cubemap(size, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.MipChain)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };

            try
            {
                foreach (var face in Faces)
                {
                    var texture = source.GetTexture(face.Property) as Texture2D;
                    if (texture == null || texture.width != texture.height || texture.width % size != 0)
                    {
                        throw new InvalidDataException("Expected a square texture divisible by destination size: " + face.Property);
                    }
                    int scale = texture.width / size;
                    Color[] input = texture.GetPixels();
                    var output = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            Color sum = Color.clear;
                            for (int dy = 0; dy < scale; dy++)
                            {
                                for (int dx = 0; dx < scale; dx++)
                                {
                                    int sourceY = (size - 1 - y) * scale + dy;
                                    sum += input[sourceY * texture.width + x * scale + dx].linear;
                                }
                            }
                            output[y * size + x] = (sum / (scale * scale)).gamma;
                        }
                    }
                    cube.SetPixels(output, face.Face);
                }
                cube.Apply(true);
                return cube;
            }
            catch
            {
                Object.DestroyImmediate(cube);
                throw;
            }
        }
    }
}
