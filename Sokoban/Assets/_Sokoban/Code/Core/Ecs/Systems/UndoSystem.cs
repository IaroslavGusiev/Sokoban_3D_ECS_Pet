using DefaultEcs;
using System.Linq;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class UndoSystem : AEntitySetSystem<float>
    {
        private readonly World _world;
        private readonly Entity _player;
        private readonly EntitySet _obstacles;

        public UndoSystem(World world)
            : base(world.GetEntities()
                .With<LevelStateTag>()
                .With<MoveHistory>()
                .With<UndoRequest>()
                .AsSet(), useBuffer: true)
        {
            _world = world;
            
            _player = world.Single(entity => entity.Has<PlayerTag>());
            
            _obstacles = world
                .GetEntities()
                .With<GridPosition>()
                .WithEither<WallTag>()
                .Or<BoxTag>()
                .AsSet();
        }

        public override void Dispose()
        {
            _obstacles.Dispose();
            base.Dispose();
        }

        protected override void Update(float deltaTime, in Entity entity)
        {
            entity.Remove<UndoRequest>();
            
            _player.Remove<MoveRequest>();
            _player.Remove<MovementPlan>();
            
            entity.Set<UndoProcessedTag>();
            
            var history = entity.Get<MoveHistory>();
            
            if (entity.Has<LevelCompletedTag>() || entity.Has<LevelTimedOutTag>() || history.Entries.Count == 0)
            {
                return;
            }

            CommittedMove move = history.Entries.Peek();
            
            for (var index = 0; index < move.Plan.StepCount; index++)
            {
                if (CanReverse(move.Plan[index]) == false || IsDestinationFree(move.Plan, move.Plan[index].ExpectedSource) == false)
                {
                    return;
                }
            }

            for (var index = 0; index < move.Plan.StepCount; index++)
            {
                MovementStep step = move.Plan[index];
                step.Target.Set(step.ExpectedSource);
                
                if (step.Target.Has<BoxTag>())
                {
                    step.Target.Set(new BoxGoalState(step.ExpectedSource));
                }
            }

            entity.Set(new MoveCount(move.PreviousMoveCount));
            entity.Set(new BoardRevision(entity.Get<BoardRevision>().Value + 1));
            
            history.Entries.Pop();
        }

        private bool IsDestinationFree(MovementPlan plan, GridPosition destination)
        {
            foreach (Entity obstacle in _obstacles.GetEntities())
            {
                if (obstacle.Get<GridPosition>() != destination)
                {
                    continue;
                }

                var participant = false;
                
                for (var index = 0; index < plan.StepCount; index++)
                {
                    participant |= plan[index].Target == obstacle;
                }
                if (participant == false)
                {
                    return false;
                }
            }
            return true;
        }

        private bool CanReverse(MovementStep step) =>
            ReferenceEquals(step.Target.World, _world)
            && step.Target.IsAlive
            && step.Target.IsEnabled()
            && step.Target.Has<GridPosition>()
            && step.Target.IsEnabled<GridPosition>()
            && step.Target.Get<GridPosition>() == step.Destination;
    }
}
