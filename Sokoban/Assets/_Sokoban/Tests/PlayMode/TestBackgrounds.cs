using System;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Sokoban.Tests.PlayMode
{
    internal static class TestBackgrounds
    {
        public static BackgroundCoordinator Create(TestStaticData data, GameObject target, BackgroundCatalog catalog)
        {
            data.Backgrounds = catalog;
            Camera camera = target.AddComponent<Camera>();
            camera.enabled = false;
            target.AddComponent<HDAdditionalCameraData>();
            Volume volume = target.AddComponent<Volume>();
            var display = new BackgroundDisplay(camera, volume, Shader.Find("Hidden/Sokoban/BackgroundProjection"));
            var service = new TestAddressablesService(load: (_, _) => throw new InvalidOperationException("Empty background fixture must not load assets."));
            return new BackgroundCoordinator(data, service, display, new System.Random(1));
        }
    }
}
