using DefaultEcs;
using DefaultEcs.System;
using System.Collections.Generic;

namespace Sokoban.Presentation.Ecs.Systems
{
    internal sealed class BoxGoalFeedbackCompositeSystem : ISystem<float>
    {
        private readonly SequentialSystem<float> _systems;

        public bool IsEnabled
        {
            get => _systems.IsEnabled;
            set => _systems.IsEnabled = value;
        }

        public BoxGoalFeedbackCompositeSystem(World world)
        {
            var systems = new List<ISystem<float>>(2);
            try
            {
                systems.Add(new BoxGoalFeedbackLifecycleSystem(world));
                systems.Add(new BoxGoalFeedbackSystem(world));
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
