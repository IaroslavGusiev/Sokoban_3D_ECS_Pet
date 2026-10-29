using System;
using UnityEngine;
using Random = System.Random;

namespace Sokoban.Presentation
{
    internal sealed class BackgroundCoordinator : IDisposable
    {
        private readonly BackgroundDisplay _display;
        private readonly IStaticDataProvider _staticData;
        private readonly IAddressablesService _addressables;
        private readonly Random _random;

        private BackgroundLoadOperation _pending;
        private BackgroundLoadOperation _active;
        private object _entry;

        private int _selectedIndex = -1;
        private bool _entryReady;
        private bool _nextRequested;
        private bool _disposed;

        public BackgroundStatus Status { get; private set; }
        
        public string SelectedName =>
            _entryReady && _selectedIndex >= 0
            ? _staticData.GetBackgroundCatalog().Presets[_selectedIndex].Name
            : string.Empty;
        public bool CanPreview =>
            _disposed == false && _entryReady && _selectedIndex >= 0;

        public BackgroundCoordinator(IStaticDataProvider staticData, IAddressablesService addressables, BackgroundDisplay display, Random random)
        {
            _staticData = staticData;
            _addressables = addressables;
            _display = display;
            _random = random;
        }

        public void BeginLevelEntry()
        {
            if (_disposed)
            {
                return;
            }

            _entry = new object();
            _entryReady = false;
            _nextRequested = false;
            _pending?.Dispose();
            _pending = null;
            
            Status = _active == null 
                ? BackgroundStatus.Neutral 
                : BackgroundStatus.Ready;
        }

        public void EnterLevel()
        {
            if (_disposed)
            {
                return;
            }

            _entryReady = true;
            
            int count = _staticData.GetBackgroundCatalog().Presets.Count;
            if (count == 0)
            {
                _selectedIndex = -1;
                return;
            }
            
            Select(_random.Next(count));
        }

        public void RequestNext()
        {
            if (CanPreview)
            {
                _nextRequested = true;
            }
        }

        public void Update()
        {
            if (_disposed)
            {
                return;
            }

            if (_nextRequested)
            {
                _nextRequested = false;
                
                Select(Status == BackgroundStatus.Failed
                    ? _selectedIndex
                    : (_selectedIndex + 1) % _staticData.GetBackgroundCatalog().Presets.Count);
            }

            BackgroundLoadOperation request = _pending;
            
            if (request == null || request.Task.IsCompleted == false || IsCurrent(request) == false)
            {
                return;
            }

            _pending = null;
            BackgroundLoadOperation.Completion completion = request.Task
                .GetAwaiter()
                .GetResult();
            
            if (completion.Error != null || completion.Cubemap == null)
            {
                Fail(completion.Error ?? new InvalidOperationException("Background load returned no cubemap."));
                request.Dispose();
                return;
            }

            try
            {
                _display.Show(completion.Cubemap, request.Preset);
            }
            catch (Exception exception)
            {
                Fail(exception);
                request.Dispose();
                return;
            }

            BackgroundLoadOperation previous = _active;
            _active = request;
            Status = BackgroundStatus.Ready;
            previous?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
            _entryReady = false;
            _entry = null;
            
            _pending?.Dispose();
            _pending = null;

            try
            {
                _display.Dispose();
            }
            finally
            {
                _active?.Dispose();
                _active = null;
            }
        }

        private void Select(int index)
        {
            _selectedIndex = index;
            _pending?.Dispose();
            _pending = null;
            
            BackgroundPreset preset = _staticData.GetBackgroundCatalog().Presets[index];

            if (_active != null && _active.Preset == preset)
            {
                Status = BackgroundStatus.Ready;
                return;
            }

            Status = BackgroundStatus.Loading;
            
            try
            {
                _pending = new BackgroundLoadOperation(_addressables, preset, _entry);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Fail(Exception exception)
        {
            Status = BackgroundStatus.Failed;
            Debug.LogException(exception);
        }

        private bool IsCurrent(BackgroundLoadOperation request) =>
            _disposed == false
            && _entryReady
            && ReferenceEquals(_pending, request)
            && ReferenceEquals(request.Entry, _entry);
    }
}
