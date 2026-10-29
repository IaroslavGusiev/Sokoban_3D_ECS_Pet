using System;
using System.IO;
using System.Linq;
using Sokoban.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Sokoban.Editor
{
    public static class CharacterPortraitAuthoring
    {
        private const string CatalogPath = "Assets/Addressables/Config/LevelViewCatalog.asset";
        private const string OutputFolder = "Assets/_Sokoban/Art/UI/Portraits";
        private const int Width = 512;
        private const int Height = 640;

        [MenuItem("Sokoban/UI/Regenerate character portraits")]
        public static void Regenerate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Generate portraits outside Play Mode.");
            }

            ViewCatalog catalog = AssetDatabase.LoadAssetAtPath<ViewCatalog>(CatalogPath);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Directory.CreateDirectory(OutputFolder);
            foreach (ViewCatalog.SkinEntry skin in catalog.Entries.Single(entry => entry.Kind == ViewKind.Player).Skins)
            {
                var entry = settings.groups.Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .Single(asset => asset.address == skin.Address);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                Render(prefab, $"{OutputFolder}/{skin.Skin}.png");
            }

            AssetDatabase.Refresh();
        }

        private static void Render(GameObject prefab, string path)
        {
            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameObject instance = preview.InstantiatePrefabInScene(prefab);
                Animator animator = instance.GetComponentInChildren<Animator>();
                AnimationClip idle = animator.runtimeAnimatorController.animationClips
                    .Where(clip => clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(clip => clip.name, StringComparer.Ordinal).First();
                animator.applyRootMotion = false;
                idle.SampleAnimation(animator.gameObject, 0.4f);
                animator.enabled = false;

                // Fixed framing and authored model scale keep the portraits comparable.
                Camera camera = preview.camera;
                Vector3 target = new(0f, 0.9f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 1.2f;
                camera.transform.position = target + new Vector3(3f, 1.3f, 5f).normalized * 5f;
                camera.transform.LookAt(target);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                preview.lights[0].intensity = 8f;
                preview.lights[0].transform.rotation = Quaternion.Euler(25f, 210f, 0f);
                preview.lights[1].intensity = 3f;
                preview.lights[1].transform.rotation = Quaternion.Euler(10f, 130f, 0f);
                preview.ambientColor = new Color(0.3f, 0.3f, 0.3f);

                // EndStaticPreview discards alpha. Read the preview target instead.
                preview.BeginPreview(new Rect(0f, 0f, Width, Height), GUIStyle.none);
                preview.Render(true);
                var targetTexture = (RenderTexture)preview.EndPreview();
                RenderTexture.active = targetTexture;
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                if (targetTexture.sRGB == false)
                {
                    Color[] pixels = image.GetPixels();
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        pixels[index] = pixels[index].gamma;
                    }
                    image.SetPixels(pixels);
                }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
                // Owns the preview scene, prefab instance, camera, lights and render target.
                preview.Cleanup();
            }
        }
    }
}
