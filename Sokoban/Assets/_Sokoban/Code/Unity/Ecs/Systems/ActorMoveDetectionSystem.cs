using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    internal sealed class ActorMoveDetectionSystem : AEntitySetSystem<float>
    {
        private readonly EntitySet _players;
        private readonly EntitySet _boxes;
        private readonly float _duration;
        private bool _pushing;

        public ActorMoveDetectionSystem(World world, float duration)
            : base(ActorQuery.Actors(world).With<ObservedPosition>().AsSet(), useBuffer: true)
        {
            _duration = duration;
            try
            {
                _players = ActorQuery.Actors(world).With<ObservedPosition>().With<PlayerTag>().AsSet();
                _boxes = ActorQuery.Actors(world).With<ObservedPosition>().With<BoxTag>().AsSet();
            }
            catch
            {
                try
                {
                    _boxes?.Dispose();
                    _players?.Dispose();
                }
                finally
                {
                    base.Dispose();
                }
                throw;
            }
        }

        public override void Dispose()
        {
            try
            {
                _boxes.Dispose();
                _players.Dispose();
            }
            finally
            {
                base.Dispose();
            }
        }

        protected override void PreUpdate(float deltaTime) =>
            _pushing = IsSuccessfulPush();

        protected override void Update(float deltaTime, in Entity entity)
        {
            var position = entity.Get<GridPosition>();
            var observed = entity.Get<ObservedPosition>().Value;
            
            if (position == observed)
            {
                return;
            }

            Vector3 previous = GridCoordinates.ToWorld(observed);
            
            Vector3 from = entity.Has<ViewMove>() 
                ? entity.Get<ViewMove>().Position 
                : previous;
            
            Vector3 to = GridCoordinates.ToWorld(position);
            
            entity.Set(new ViewMove(from, to, _duration, entity.Has<PlayerTag>() && _pushing));
            
            if (entity.Has<PlayerTag>())
            {
                entity.Set(new ActorFacing(Quaternion.LookRotation(to - previous)));
            }
            
            entity.Set(new ObservedPosition(position));
        }

        private bool IsSuccessfulPush()
        {
            foreach (Entity player in _players.GetEntities())
            {
                GridPosition from = player.Get<ObservedPosition>().Value;
                var to = player.Get<GridPosition>();
                
                if (from == to)
                {
                    continue;
                }

                long x = (long)to.X - from.X;
                long y = (long)to.Y - from.Y;
                
                foreach (Entity box in _boxes.GetEntities())
                {
                    if (box.Get<ObservedPosition>().Value != to)
                    {
                        continue;
                    }
                    
                    var boxTo = box.Get<GridPosition>();
                    
                    if ((long)boxTo.X - to.X == x && (long)boxTo.Y - to.Y == y)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
