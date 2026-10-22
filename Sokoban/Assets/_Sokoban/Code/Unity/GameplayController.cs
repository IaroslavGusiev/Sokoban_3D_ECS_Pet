using System;
using Zenject;
using UnityEngine;
using Sokoban.Core;
using System.Collections.Generic;

namespace Sokoban.Unity
{
    public sealed class GameplayController : IInitializable, ITickable, IDisposable
    {
        private readonly LevelRoot _root;
        private readonly IPlayerInput _input;
        private readonly ViewCatalog _catalog;
        private readonly ILevelProvider _levels;
        private readonly ViewFeatures _viewFeatures;
        private readonly List<IWorldLayer> _layers = new();

        public Simulation Current { get; private set; }

        public GameplayController(
            IPlayerInput input, 
            ILevelProvider levels, 
            ViewFeatures viewFeatures, 
            ViewCatalog catalog, 
            LevelRoot root)
        {
            _root = root;
            _input = input;
            _levels = levels;
            _catalog = catalog;
            _viewFeatures = viewFeatures;
        }

        public void Initialize() => 
            StartLevel();

        public void Dispose() => 
            ReleaseLevel();

        public void Tick()
        {
            Current.Send(_input.ReadMove());
            float deltaTime = Time.deltaTime;
            
            foreach (IWorldLayer layer in _layers)
            {
                layer.Tick(deltaTime);
            }

            if (_input.RestartPressed)
            {
                StartLevel();
            }
        }

        private void StartLevel()
        {
            ReleaseLevel();
            
            LevelDefinition level = _levels.GetLevel();
            Current = new Simulation(level);
            
            _layers.Add(Current);
            
            try
            {
                _layers.Add(new ViewLayer(_root, level, Current.World, _catalog, _viewFeatures.Create()));
            }
            catch
            {
                ReleaseLevel();
                throw;
            }
        }

        private void ReleaseLevel()
        {
            for (int index = _layers.Count - 1; index >= 0; index--)
            {
                _layers[index].Dispose();
            }

            _layers.Clear();
            Current = null;
        }
    }
}