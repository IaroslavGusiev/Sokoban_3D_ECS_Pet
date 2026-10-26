using DefaultEcs;
using DefaultEcs.System;
using System.Collections.Generic;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class ActorMotionSystem : ISystem<float>
    {
        private readonly SequentialSystem<float> _systems;

        public bool IsEnabled
        {
            get => _systems.IsEnabled;
            set => _systems.IsEnabled = value;
        }

        public ActorMotionSystem(World world, float duration)
        {
            var systems = new List<ISystem<float>>(3);
            try
            {
                systems.Add(new ActorStateInitSystem(world));
                systems.Add(new ActorMoveDetectionSystem(world, duration));
                systems.Add(new ViewMoveAdvanceSystem(world));
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
