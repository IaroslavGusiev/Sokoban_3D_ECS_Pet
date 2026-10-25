using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core._Sokoban.Code.Core.Ecs;

namespace Sokoban.Core.Systems
{
    internal sealed class MovementValidationSystem : ISystem<float>
    {
        private readonly EntitySet _requests;
        private readonly EntityMap<GridPosition> _boxes;
        private readonly EntityMap<GridPosition> _walls;

        public bool IsEnabled { get; set; } = true;

        public MovementValidationSystem(World world)
        {
            _requests = world.GetEntities()
                .With<PlayerTag>()
                .With<GridPosition>()
                .With<MoveRequest>()
                .AsSet();

            _boxes = world
                .GetEntities()
                .With<BoxTag>()
                .AsMap<GridPosition>();
            
            _walls = world
                .GetEntities()
                .With<WallTag>()
                .AsMap<GridPosition>();
        }

        public void Update(float deltaTime)
        {
            foreach (Entity player in _requests.GetEntities().ToArray())
            {
                MoveCommand command = player.Get<MoveRequest>().Command;
                
                player.Remove<MoveRequest>();

                if (PlanMove(player, command) is { } plan)
                {
                    player.Set(plan);
                }
            }
        }

        public void Dispose()
        {
            _requests.Dispose();
            _boxes.Dispose();
            _walls.Dispose();
        }

        private MovementPlan? PlanMove(Entity player, MoveCommand command)
        {
            GridPosition step = command.ToStep();
            
            if (step == default)
            {
                return null;
            }

            var from = player.Get<GridPosition>();

            if (TryGetDestination(from, step, out GridPosition to) == false || _walls.ContainsKey(to))
            {
                return null;
            }

            var playerStep = new MovementStep(player, from, to);

            return _boxes.TryGetEntity(to, out Entity box)
                ? PlanPush(box, playerStep, step)
                : new MovementPlan(playerStep);
        }

        private MovementPlan? PlanPush(Entity box, MovementStep playerStep, GridPosition step)
        {
            var from = box.Get<GridPosition>();

            if (TryGetDestination(from, step, out GridPosition to) == false || _walls.ContainsKey(to) || _boxes.ContainsKey(to))
            {
                return null;
            }

            return new MovementPlan(new MovementStep(box, from, to), playerStep);
        }

        private static bool TryGetDestination(GridPosition from, GridPosition step, out GridPosition destination)
        {
            long x = (long)from.X + step.X;
            long y = (long)from.Y + step.Y;

            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                destination = default;
                return false;
            }

            destination = new GridPosition((int)x, (int)y);
            return true;
        }
    }
}
