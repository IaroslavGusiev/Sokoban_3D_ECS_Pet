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
            : base(ActorQuery.Actors(world).With<ActorViewState>().AsSet(), useBuffer: true)
        {
            _duration = duration;
            try
            {
                _players = ActorQuery.Actors(world).With<ActorViewState>().With<PlayerTag>().AsSet();
                _boxes = ActorQuery.Actors(world).With<ActorViewState>().With<BoxTag>().AsSet();
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
            var state = entity.Get<ActorViewState>();
            
            if (position == state.Position)
            {
                return;
            }

            Vector3 previous = GridCoordinates.ToWorld(state.Position);
            
            Vector3 from = entity.Has<ViewMove>() 
                ? entity.Get<ViewMove>().Position 
                : previous;
            
            Vector3 to = GridCoordinates.ToWorld(position);
            
            entity.Set(new ViewMove(from, to, _duration, entity.Has<PlayerTag>() && _pushing));
            
            state.Facing = Quaternion.LookRotation(to - previous);
            state.HasFacing = true;
            state.Position = position;
            
            entity.Set(state);
        }

        private bool IsSuccessfulPush()
        {
            foreach (Entity player in _players.GetEntities())
            {
                GridPosition from = player.Get<ActorViewState>().Position;
                var to = player.Get<GridPosition>();
                
                if (from == to)
                {
                    continue;
                }

                long x = (long)to.X - from.X;
                long y = (long)to.Y - from.Y;
                
                foreach (Entity box in _boxes.GetEntities())
                {
                    if (box.Get<ActorViewState>().Position != to)
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
