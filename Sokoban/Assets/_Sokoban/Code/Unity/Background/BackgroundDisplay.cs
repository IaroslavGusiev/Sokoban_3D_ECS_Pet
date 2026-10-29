using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using UnityEngine.Rendering.HighDefinition;

namespace Sokoban.Presentation
{
    internal sealed class BackgroundDisplay : IDisposable
    {
        private static readonly int BackgroundCube = Shader.PropertyToID("_BackgroundCube");
        private static readonly int BackgroundExposure = Shader.PropertyToID("_BackgroundExposure");
        private static readonly int BackgroundRotation = Shader.PropertyToID("_BackgroundRotation");

        private readonly Camera _camera;
        private readonly Volume _volume;
        private readonly Material _material;
        private readonly VolumeProfile _profile;
        private readonly FullScreenCustomPass _pass;
        private readonly CustomPassVolume _projection;
        private readonly VisualEnvironment _environment;

        private readonly Color _originalColor;
        private readonly HDAdditionalCameraData _cameraData;
        private readonly HDAdditionalCameraData.ClearColorMode _originalClearMode;

        public BackgroundDisplay(Camera camera, Volume volume, Shader shader)
        {
            _camera = camera;
            _volume = volume;
            _cameraData = camera.GetComponent<HDAdditionalCameraData>();
            _originalClearMode = _cameraData.clearColorMode;
            _originalColor = _cameraData.backgroundColorHDR;

            try
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.HideAndDontSave;
                
                _environment = _profile.Add<VisualEnvironment>();
                _volume.profile = _profile;
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                
                _pass = new FullScreenCustomPass
                {
                    name = "Selected background gradient",
                    enabled = false,
                    fullscreenPassMaterial = _material,
                    materialPassName = "Background",
                    targetColorBuffer = CustomPass.TargetBuffer.Camera,
                    targetDepthBuffer = CustomPass.TargetBuffer.None
                };
                
                _projection = _volume.gameObject.AddComponent<CustomPassVolume>();
                _projection.hideFlags = HideFlags.HideAndDontSave;
                _projection.targetCamera = camera;
                _projection.injectionPoint = CustomPassInjectionPoint.BeforePreRefraction;
                _projection.customPasses.Add(_pass);
                
                ShowNeutral();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Show(Cubemap cubemap, BackgroundPreset preset)
        {
            if (_camera == null || _volume == null)
            {
                throw new ObjectDisposedException(nameof(BackgroundDisplay));
            }
            
            if (cubemap == null)
            {
                throw new ArgumentNullException(nameof(cubemap));
            }

            _material.SetTexture(BackgroundCube, cubemap);
            _material.SetFloat(BackgroundRotation, preset.RotationDegrees);
            _material.SetFloat(BackgroundExposure, preset.Exposure);
            
            _pass.enabled = true;
        }

        public void Dispose() => 
            Cleanup();

        private void ShowNeutral()
        {
            _environment.skyType.Override(0);
            _cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            _cameraData.backgroundColorHDR = new Color(0.2f, 0.22f, 0.24f);
        }

        private void Cleanup()
        {
            if (_pass != null)
            {
                _pass.enabled = false;
                _pass.fullscreenPassMaterial = null;
            }
            
            if (_projection != null)
            {
                _projection.enabled = false;
                _projection.customPasses.Clear();
                Object.Destroy(_projection);
            }
            
            if (_material != null)
            {
                _material.SetTexture(BackgroundCube, null);
                Object.Destroy(_material);
            }
            
            if (_volume != null)
            {
                _volume.profile = null;
            }
            
            if (_cameraData != null)
            {
                _cameraData.clearColorMode = _originalClearMode;
                _cameraData.backgroundColorHDR = _originalColor;
            }

            if (_profile == null)
            {
                return;
            }
            
            foreach (VolumeComponent component in _profile.components)
            {
                Object.Destroy(component);
            }
            
            Object.Destroy(_profile);
        }
    }
}
