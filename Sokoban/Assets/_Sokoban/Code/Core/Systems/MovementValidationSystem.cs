using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class MovementValidationSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly EntitySet _players;
        private readonly EntitySet _boxes;
        private readonly EntitySet _walls;

        public bool IsEnabled { get; set; } = true;

        public MovementValidationSystem(World world)
        {
            _world = world;
            _players = world.GetEntities().With<GridPosition>().With<PlayerTag>().AsSet();
            _boxes = world.GetEntities().With<GridPosition>().With<BoxTag>().AsSet();
            _walls = world.GetEntities().With<GridPosition>().With<WallTag>().AsSet();
        }

        public void Update(float deltaTime)
        {
            DiscardAbandonedPlans();
            ProcessPlayerRequests();
        }

        private void DiscardAbandonedPlans()
        {
            // This stage owns all plan output. World enumeration includes disabled owners.
            // Remove is a no-op when absent and does not change World entity membership.
            foreach (Entity entity in _world)
            {
                entity.Remove<MovementPlan>();
            }
        }

        private void ProcessPlayerRequests()
        {
            foreach (Entity player in _players.GetEntities().ToArray())
            {
                if (!player.Has<MoveRequest>())
                {
                    continue;
                }

                MoveCommand command = player.Get<MoveRequest>().Command;
                player.Remove<MoveRequest>();

                if (_players.Count == 1 && TryCreatePlan(player, command, out MovementPlan plan))
                {
                    player.Set(plan);
                }
            }
        }

        private bool TryCreatePlan(Entity player, MoveCommand command, out MovementPlan plan)
        {
            plan = default;
            if (!TryGetDirection(command, out var direction))
            {
                return false;
            }

            GridPosition source = player.Get<GridPosition>();
            if (!TryOffset(source, direction, out GridPosition destination) || IsWallAt(destination))
            {
                return false;
            }

            var playerStep = new MovementStep(player, source, destination);
            if (TryFindAt(_boxes, destination, out Entity box))
            {
                return TryCreatePushPlan(box, playerStep, direction, out plan);
            }

            plan = new MovementPlan(playerStep);
            return true;
        }

        private bool TryCreatePushPlan(Entity box, MovementStep playerStep,
            (int X, int Y) direction, out MovementPlan plan)
        {
            plan = default;
            GridPosition boxSource = box.Get<GridPosition>();
            if (!TryOffset(boxSource, direction, out GridPosition boxDestination)
                || IsWallAt(boxDestination)
                || TryFindAt(_boxes, boxDestination, out _))
            {
                return false;
            }

            var boxStep = new MovementStep(box, boxSource, boxDestination);
            plan = new MovementPlan(boxStep, playerStep);
            return true;
        }

        private bool IsWallAt(GridPosition position) => TryFindAt(_walls, position, out _);

        private static bool TryFindAt(EntitySet entities, GridPosition position, out Entity match)
        {
            foreach (Entity entity in entities.GetEntities())
            {
                if (entity.Get<GridPosition>() == position)
                {
                    match = entity;
                    return true;
                }
            }

            match = default;
            return false;
        }

        private static bool TryGetDirection(MoveCommand command, out (int X, int Y) direction)
        {
            direction = command switch
            {
                MoveCommand.Up => (0, 1),
                MoveCommand.Down => (0, -1),
                MoveCommand.Left => (-1, 0),
                MoveCommand.Right => (1, 0),
                _ => (0, 0)
            };

            return direction != (0, 0);
        }

        private static bool TryOffset(GridPosition position, (int X, int Y) direction,
            out GridPosition result)
        {
            long x = (long)position.X + direction.X;
            long y = (long)position.Y + direction.Y;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                result = default;
                return false;
            }

            result = new GridPosition((int)x, (int)y);
            return true;
        }

        public void Dispose()
        {
            _players.Dispose();
            _boxes.Dispose();
            _walls.Dispose();
        }
    }
}
