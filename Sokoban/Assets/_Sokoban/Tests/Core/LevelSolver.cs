using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    internal static class LevelSolver
    {
        private const int StateCap = 2_000_000;
        private static readonly MoveCommand[] Moves = { MoveCommand.Up, MoveCommand.Down, MoveCommand.Left, MoveCommand.Right };

        public static (IReadOnlyList<MoveCommand> Moves, IReadOnlyList<GridPosition> PlayerPositions) Solve(LevelDefinition level)
        {
            int Cell(GridPosition position) => position.Y * level.Width + position.X;
            var walls = new HashSet<int>(level.Walls.Select(Cell));
            var goals = new HashSet<int>(level.Goals.Select(Cell));
            var initial = new State(Cell(level.PlayerStart), level.Boxes.Select(Cell).OrderBy(cell => cell).ToArray(), null, default);
            var visited = new HashSet<State> { initial };
            var queue = new Queue<State>();
            queue.Enqueue(initial);

            while (queue.Count > 0)
            {
                State state = queue.Dequeue();
                if (state.Boxes.All(goals.Contains)) return Reconstruct(state, level.Width);

                foreach (MoveCommand move in Moves)
                {
                    if (!TryStep(state.Player, move, level, walls, out int destination)) continue;
                    int boxIndex = Array.BinarySearch(state.Boxes, destination);
                    int[] boxes = state.Boxes;
                    if (boxIndex >= 0)
                    {
                        if (!TryStep(destination, move, level, walls, out int boxDestination)
                            || Array.BinarySearch(boxes, boxDestination) >= 0) continue;

                        boxes = (int[])boxes.Clone();
                        boxes[boxIndex] = boxDestination;
                        Array.Sort(boxes);
                    }

                    var next = new State(destination, boxes, state, move);
                    if (!visited.Add(next)) continue;
                    if (visited.Count > StateCap)
                    {
                        throw new InvalidOperationException($"Level is too complex or unsolvable: exceeded the {StateCap:N0}-state BFS cap.");
                    }

                    queue.Enqueue(next);
                }
            }

            throw new InvalidOperationException("Level is too complex or unsolvable: BFS exhausted all reachable states without placing every box on a goal.");
        }

        private static bool TryStep(int cell, MoveCommand move, LevelDefinition level, HashSet<int> walls, out int destination)
        {
            int x = cell % level.Width;
            int y = cell / level.Width;
            switch (move)
            {
                case MoveCommand.Up: y++; break;
                case MoveCommand.Down: y--; break;
                case MoveCommand.Left: x--; break;
                case MoveCommand.Right: x++; break;
            }

            destination = y * level.Width + x;
            return x >= 0 && x < level.Width && y >= 0 && y < level.Height && !walls.Contains(destination);
        }

        private static (IReadOnlyList<MoveCommand> Moves, IReadOnlyList<GridPosition> PlayerPositions) Reconstruct(State state, int width)
        {
            var moves = new List<MoveCommand>();
            var positions = new List<GridPosition>();
            for (; state.Previous != null; state = state.Previous)
            {
                moves.Add(state.Move);
                positions.Add(new GridPosition(state.Player % width, state.Player / width));
            }
            moves.Reverse();
            positions.Reverse();
            return (moves, positions);
        }

        private sealed class State : IEquatable<State>
        {
            public readonly int Player;
            public readonly int[] Boxes;
            public readonly State Previous;
            public readonly MoveCommand Move;

            public State(int player, int[] boxes, State previous, MoveCommand move)
            {
                Player = player;
                Boxes = boxes;
                Previous = previous;
                Move = move;
            }

            public bool Equals(State other) => other != null && Player == other.Player && Boxes.SequenceEqual(other.Boxes);
            public override bool Equals(object obj) => obj is State other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Player;
                    foreach (int box in Boxes) hash = hash * 397 ^ box;
                    return hash;
                }
            }
        }
    }
}
