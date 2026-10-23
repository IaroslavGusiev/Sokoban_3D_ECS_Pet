using System;

namespace Sokoban.Core
{
    public interface IWorldLayer : IDisposable
    {
        void Tick(float deltaTime); // TODO: think about not usage
    }
}
