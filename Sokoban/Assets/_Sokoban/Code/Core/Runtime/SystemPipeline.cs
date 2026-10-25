using System;
using DefaultEcs;
using System.Linq;
using DefaultEcs.System;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class SystemPipeline : IDisposable
    {
        private readonly SequentialSystem<float> _systems;

        public SystemPipeline(World world, IReadOnlyList<IFeature> features)
        {
            var systems = new List<ISystem<float>>();
            
            try
            {
                systems
                    .AddRange(features
                    .Select(feature => feature.Create(world)));

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
