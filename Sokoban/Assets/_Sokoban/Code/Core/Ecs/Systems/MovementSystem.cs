using DefaultEcs;
using DefaultEcs.System;
using System.Collections.Generic;

namespace Sokoban.Core.Systems
{
    internal sealed class MovementSystem : ISystem<float>
    {
        private readonly SequentialSystem<float> _systems;

        public bool IsEnabled
        {
            get => _systems.IsEnabled;
            set => _systems.IsEnabled = value;
        }

        public MovementSystem(World world)
        {
            var systems = new List<ISystem<float>>(2);
            try
            {
                systems.Add(new MovementValidationSystem(world));
                systems.Add(new ApplyMovementSystem(world));
                _systems = new SequentialSystem<float>(systems);
            }
            catch
            {
                for (int index = systems.Count - 1; index >= 0; index--)
                {
                    systems[index].Dispose();
                }
                throw;
            }
        }

        public void Update(float deltaTime) =>
            _systems.Update(deltaTime);

        public void Dispose() =>
            _systems.Dispose();
    }
}
