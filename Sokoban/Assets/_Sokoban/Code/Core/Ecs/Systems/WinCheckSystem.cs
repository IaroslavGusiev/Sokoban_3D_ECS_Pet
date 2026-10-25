using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class WinCheckSystem : ISystem<float>
    {
        private readonly EntitySet _goals;
        private readonly EntitySet _boxes;
        private readonly EntitySet _players;

        public bool IsEnabled { get; set; } = true;

        public WinCheckSystem(World world)
        {
            _players = world
                .GetEntities()
                .With<PlayerTag>()
                .Without<LevelTimedOutTag>()
                .AsSet();
            
            _goals = world
                .GetEntities()
                .With<GoalTag>()
                .With<GridPosition>()
                .AsSet();
            
            _boxes = world
                .GetEntities()
                .With<BoxTag>()
                .With<GridPosition>()
                .AsSet();
        }

        public void Update(float deltaTime)
        {
            foreach (Entity player in _players.GetEntities())
            {
                if (player.Has<LevelCompletedTag>() == false && AllGoalsCovered())
                {
                    player.Set<LevelCompletedTag>();
                }
            }
        }

        public void Dispose()
        {
            _boxes.Dispose();
            _goals.Dispose();
            _players.Dispose();
        }

        private bool AllGoalsCovered()
        {
            foreach (Entity goal in _goals.GetEntities())
            {
                var position = goal.Get<GridPosition>();
                var covered = false;
                
                foreach (Entity box in _boxes.GetEntities())
                {
                    if (box.Get<GridPosition>() != position)
                    {
                        continue;
                    }
                    
                    covered = true;
                    break;
                }

                if (covered == false)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
