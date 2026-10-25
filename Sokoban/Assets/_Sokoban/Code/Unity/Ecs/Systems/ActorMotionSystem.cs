using System;
using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class ActorMotionSystem : ISystem<float>
    {
        private readonly EntitySet _actors;
        private readonly EntitySet _players;
        private readonly EntitySet _uninitialized;
        private readonly float _duration;
        private Entity[] _initializationBuffer = Array.Empty<Entity>();
        public bool IsEnabled { get; set; } = true;

        public ActorMotionSystem(World world, float duration)
        {
            _duration = duration;
            _actors = world.GetEntities().With<GridPosition>().WithEither<PlayerTag>().Or<BoxTag>().AsSet();
            _players = world.GetEntities().With<PlayerTag>().With<GridPosition>().AsSet();
            _uninitialized = world.GetEntities().With<GridPosition>().WithEither<PlayerTag>().Or<BoxTag>()
                .Without<ActorViewState>().AsSet();
        }

        public void Update(float deltaTime)
        {
            InitializeActors();
            bool pushing = IsSuccessfulPush();
            foreach (Entity actor in _actors.GetEntities())
            {
                GridPosition position = actor.Get<GridPosition>();
                ActorViewState state = actor.Get<ActorViewState>();
                if (position != state.Position)
                {
                    Vector3 from = actor.Has<ViewMove>() ? actor.Get<ViewMove>().Position : GridCoordinates.ToWorld(state.Position);
                    Vector3 to = GridCoordinates.ToWorld(position);
                    actor.Set(new ViewMove { From = from, To = to, Duration = _duration, IsPushing = actor.Has<PlayerTag>() && pushing });
                    state.Facing = Quaternion.LookRotation(to - GridCoordinates.ToWorld(state.Position));
                    state.HasFacing = true;
                    state.Position = position;
                    actor.Set(state);
                }

                if (!actor.Has<ViewMove>()) continue;
                ViewMove move = actor.Get<ViewMove>();
                move.Elapsed = Mathf.Min(move.Elapsed + deltaTime, move.Duration);
                if (move.Elapsed >= move.Duration) actor.Remove<ViewMove>();
                else actor.Set(move);
            }
        }

        private void InitializeActors()
        {
            int count = _uninitialized.Count;
            if (_initializationBuffer.Length < count) Array.Resize(ref _initializationBuffer, count);
            _uninitialized.GetEntities().CopyTo(_initializationBuffer);
            for (int index = 0; index < count; index++)
            {
                Entity actor = _initializationBuffer[index];
                actor.Set(new ActorViewState { Position = actor.Get<GridPosition>(), Facing = Quaternion.identity, HasFacing = false });
            }
        }

        private bool IsSuccessfulPush()
        {
            foreach (Entity player in _players.GetEntities())
            {
                GridPosition from = player.Get<ActorViewState>().Position;
                GridPosition to = player.Get<GridPosition>();
                if (from == to) continue;
                foreach (Entity actor in _actors.GetEntities())
                {
                    if (!actor.Has<BoxTag>() || actor.Get<ActorViewState>().Position != to) continue;
                    GridPosition boxTo = actor.Get<GridPosition>();
                    if ((long)boxTo.X - to.X == (long)to.X - from.X && (long)boxTo.Y - to.Y == (long)to.Y - from.Y)
                        return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            _uninitialized.Dispose();
            _players.Dispose();
            _actors.Dispose();
        }
    }
}
