using DefaultEcs;
using System.Linq;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class MoveCountSystem : AEntitySetSystem<float>
    {
        private readonly Entity _player;
        private GridPosition _lastPosition;

        public MoveCountSystem(World world)
            : base(world.GetEntities()
                .With<LevelStateTag>()
                .With<MoveCount>()
                .AsSet(), useBuffer: true)
        {
            _player = world.Single(entity => entity.Has<PlayerTag>());
            _lastPosition = _player.Get<GridPosition>();
        }

        protected override void Update(float deltaTime, in Entity entity)
        {
            if (entity.Has<UndoProcessedTag>())
            {
                if (_player.IsAlive && _player.Has<GridPosition>())
                {
                    _lastPosition = _player.Get<GridPosition>();
                }
                entity.Remove<UndoProcessedTag>();
                return;
            }

            if (_player.IsEnabled() == false || _player.IsEnabled<GridPosition>() == false)
            {
                return;
            }

            var position = _player.Get<GridPosition>();
            
            if (position != _lastPosition)
            {
                var count = entity.Get<MoveCount>();
                _lastPosition = position;
                entity.Set(new MoveCount(count.Value + 1));
            }
        }
    }
}
