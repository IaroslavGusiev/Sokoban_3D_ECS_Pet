using System.Collections.Generic;
using NUnit.Framework;
using Sokoban.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Sokoban.Tests.Editor
{
    public sealed class LegacySkyboxConversionTests
    {
        [Test]
        public void Conversion_MatchesRenderedLegacyDirectionsAndUvOrientationOnEveryFace()
        {
            var objects = new List<Object>();
            RenderPipelineAsset quality = QualitySettings.renderPipeline;
            RenderPipelineAsset graphics = GraphicsSettings.defaultRenderPipeline;
            RenderTexture active = RenderTexture.active;
            Object qualitySettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            bool qualityWasDirty = EditorUtility.IsDirty(qualitySettings);
            bool graphicsWereDirty = EditorUtility.IsDirty(graphicsSettings);

            try
            {
                var source = new Material(Shader.Find("Skybox/6 Sided"));
                objects.Add(source);
                source.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
                source.SetFloat("_Exposure", 1f);
                source.SetFloat("_Rotation", 0f);
                string[] properties = { "_LeftTex", "_RightTex", "_UpTex", "_DownTex", "_FrontTex", "_BackTex" };
                for (int face = 0; face < properties.Length; face++)
                {
                    var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, false);
                    objects.Add(texture);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    var pixels = new Color[64 * 64];
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            // R identifies the face; G/B expose every rotation or axis flip.
                            pixels[y * 64 + x] = new Color((face + 1) / 8f, x / 63f, y / 63f);
                        }
                    }
                    texture.SetPixels(pixels);
                    texture.Apply();
                    source.SetTexture(properties[face], texture);
                }
                Cubemap converted = LegacySkyboxConverter.CreateCubemap(source, 64);
                objects.Add(converted);
                Assert.That(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(converted.graphicsFormat), Is.True);

                var target = new GameObject("Isolated legacy sky reference", typeof(Camera), typeof(Skybox));
                objects.Add(target);
                Camera camera = target.GetComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.Skybox;
                target.GetComponent<Skybox>().material = source;
                var reference = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32) { dimension = TextureDimension.Cube };
                objects.Add(reference);

                // Only this isolated reference render uses Built-in. Restore both settings even
                // on assertion failure; the actual gameplay camera/scene is never reconfigured.
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                Assert.That(camera.RenderToCubemap(reference), Is.True);

                Shader shader = ShaderUtil.CreateShaderAsset(@"Shader ""Hidden/Sokoban/ConversionTest"" {
SubShader { Pass { ZTest Always Cull Off ZWrite Off CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include ""UnityCG.cginc""
samplerCUBE _Cube; float3 _Forward; float3 _Right; float3 _Up;
float4 frag(v2f_img i):SV_Target { float2 p=i.uv*2-1; return texCUBElod(_Cube,float4(_Forward+p.x*_Right+p.y*_Up,0)); }
ENDCG } } }", false);
                objects.Add(shader);
                var sample = new Material(shader);
                objects.Add(sample);
                var flat = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
                objects.Add(flat);
                var read = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                objects.Add(read);
                Vector3[] forward = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                Vector3[] up = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
                for (int face = 0; face < forward.Length; face++)
                {
                    sample.SetVector("_Forward", forward[face]);
                    sample.SetVector("_Right", Vector3.Cross(up[face], forward[face]));
                    sample.SetVector("_Up", up[face]);
                    Color32[] expected = Read(reference, sample, flat, read);
                    Color32[] actual = Read(converted, sample, flat, read);
                    for (int y = 4; y < 60; y++)
                    {
                        for (int x = 4; x < 60; x++)
                        {
                            int index = y * 64 + x;
                            Assert.That(Mathf.Abs(expected[index].r - actual[index].r), Is.LessThanOrEqualTo(3), properties[face] + " identity");
                            Assert.That(Mathf.Abs(expected[index].g - actual[index].g), Is.LessThanOrEqualTo(3), properties[face] + " horizontal orientation");
                            Assert.That(Mathf.Abs(expected[index].b - actual[index].b), Is.LessThanOrEqualTo(3), properties[face] + " vertical orientation");
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = active;
                QualitySettings.renderPipeline = quality;
                GraphicsSettings.defaultRenderPipeline = graphics;
                // The temporary setters must not cause unrelated ProjectSettings reserialization.
                if (qualityWasDirty == false)
                {
                    EditorUtility.ClearDirty(qualitySettings);
                }
                if (graphicsWereDirty == false)
                {
                    EditorUtility.ClearDirty(graphicsSettings);
                }
                for (int index = objects.Count - 1; index >= 0; index--)
                {
                    Object.DestroyImmediate(objects[index]);
                }
            }
        }

        private static Color32[] Read(Texture cube, Material material, RenderTexture target, Texture2D read)
        {
            material.SetTexture("_Cube", cube);
            Graphics.Blit(null, target, material);
            RenderTexture.active = target;
            read.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            read.Apply();
            return read.GetPixels32();
        }
    }
}
