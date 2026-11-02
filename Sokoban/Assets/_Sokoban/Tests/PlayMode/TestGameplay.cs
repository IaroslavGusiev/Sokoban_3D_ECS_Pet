using System.Runtime.CompilerServices;
using Sokoban.Presentation;

namespace Sokoban.Tests.PlayMode
{
    internal static class TestGameplay
    {
        private static readonly ConditionalWeakTable<GameplayController, Boundaries> Sessions = new();

        public static GameplayController Create(IPlayerInput input, ILevelSource source, HintCoordinator hints,
            BackgroundCoordinator backgrounds, LevelRunFactory factory, IStaticDataProvider data)
        {
            var status = new GameplayStatus();
            var commands = new GameplayCommandInbox(status);
            var controller = new GameplayController(input, source, hints, backgrounds, factory, data, status, commands);
            Sessions.Add(controller, new Boundaries(status, commands));
            return controller;
        }

        public static GameplayStatus Status(GameplayController controller) =>
            Sessions.GetValue(controller, _ => throw new System.InvalidOperationException("Missing test composition.")).Status;

        public static GameplayCommandInbox Commands(GameplayController controller) =>
            Sessions.GetValue(controller, _ => throw new System.InvalidOperationException("Missing test composition.")).Commands;

        private sealed class Boundaries
        {
            public GameplayStatus Status { get; }
            public GameplayCommandInbox Commands { get; }

            public Boundaries(GameplayStatus status, GameplayCommandInbox commands)
            {
                Status = status;
                Commands = commands;
            }
        }
    }
}
