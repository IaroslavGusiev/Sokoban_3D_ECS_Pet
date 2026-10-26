using System;
using DefaultEcs.System;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class SystemPipeline : IDisposable
    {
        private readonly SequentialSystem<float> _systems;

        public SystemPipeline(IEnumerable<ISystem<float>> source)
        {
            var systems = new List<ISystem<float>>();
            
            try
            {
                foreach (ISystem<float> system in source)
                {
                    systems.Add(system);
                }

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

        public void Tick(float deltaTime) => 
            _systems.Update(deltaTime);

        public void Dispose() => 
            _systems.Dispose();
    }
}
