using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public sealed class StaticDataProvider : IStaticDataProvider
    {
        public const string ConfigLabel = "Configs";

        private readonly IAddressablesService _addressables;
        private readonly CancellationTokenSource _cancellation = new();

        private Configs _configs;
        private ConfigAttempt _attempt;

        public int LevelCount => _configs.Levels.Count;

        public StaticDataProvider(IAddressablesService addressables) => 
            _addressables = addressables;

        public UniTask LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
                
            if (_configs != null)
            {
                return UniTask.CompletedTask;
            }

            ConfigAttempt attempt = _attempt;
            
            if (attempt == null)
            {
                attempt = new ConfigAttempt(_addressables.LoadByLabel<ScriptableObject>(ConfigLabel));
                _attempt = attempt;
                attempt.Task = LoadConfigsAsync(attempt, cancellationToken).Preserve();
            }

            return attempt.Task.AttachExternalCancellation(cancellationToken);
        }

        public GameplaySettings GetGameplaySettings() => 
            _configs.Gameplay;
        
        public ViewCatalog GetViewCatalog() => 
            _configs.Views;
        
        public LevelConfig GetLevelConfig(int levelIndex) => 
            _configs.Levels.Levels[levelIndex];

        public void Dispose()
        {
            ConfigAttempt attempt = _attempt;
            _cancellation.Cancel();
            
            if (attempt != null)
            {
                Release(attempt);
            }
            
            _configs = null;
            _cancellation.Dispose();
        }

        private async UniTask LoadConfigsAsync(ConfigAttempt attempt, CancellationToken callerToken)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _cancellation.Token);
            CancellationToken token = cancellation.Token;
            
            try
            {
                IList<ScriptableObject> assets = await _addressables.AwaitAsync(attempt.Handle, token);
                token.ThrowIfCancellationRequested();
                
                var gameplay = Single<GameplaySettings>(assets);
                var levels = Single<LevelList>(assets);
                var views = Single<ViewCatalog>(assets);
                
                _configs = new Configs(gameplay, levels, views);
            }
            catch
            {
                Release(attempt);
                throw;
            }
        }

        private void Release(ConfigAttempt attempt)
        {
            if (ReferenceEquals(_attempt, attempt) == false)
            {
                return;
            }
            
            _attempt = null;
            _addressables.Release(attempt.Handle);
        }

        private static T Single<T>(IList<ScriptableObject> assets) where T : ScriptableObject
        {
            T result = null;
            var count = 0;
            
            foreach (ScriptableObject asset in assets)
            {
                if (asset is not T config)
                {
                    continue;
                }
                
                result = config;
                count++;
            }

            return count != 1 
                ? throw new InvalidOperationException($"Label '{ConfigLabel}' must contain exactly one {typeof(T).Name}; found {count}.") 
                : result;
        }

        private sealed class ConfigAttempt
        {
            public readonly AsyncOperationHandle<IList<ScriptableObject>> Handle;
            public UniTask Task;

            public ConfigAttempt(AsyncOperationHandle<IList<ScriptableObject>> handle) => 
                Handle = handle;
        }

        private sealed class Configs
        {
            public readonly LevelList Levels;
            public readonly ViewCatalog Views;
            public readonly GameplaySettings Gameplay;

            public Configs(GameplaySettings gameplay, LevelList levels, ViewCatalog views)
            {
                Views = views;
                Levels = levels;
                Gameplay = gameplay;
            }
        }
    }
}
