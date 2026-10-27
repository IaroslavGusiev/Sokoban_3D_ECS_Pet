using System;
using System.Linq;
using System.Threading;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class HintSolver
    {
        private static readonly MoveCommand[] Moves = { MoveCommand.Up, MoveCommand.Down, MoveCommand.Left, MoveCommand.Right };

        public HintResult Solve(LevelDefinition board, int stateBudget, CancellationToken token) =>
            Solve(board, stateBudget, token, null);

        internal HintResult Solve(LevelDefinition board, int stateBudget, CancellationToken token, Action stateExpanded)
        {
            if (stateBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stateBudget));
            }
            
            token.ThrowIfCancellationRequested();
            
            var walls = new HashSet<GridPosition>(board.Walls);
            var goals = new HashSet<GridPosition>(board.Goals);
            var initial = new State(board.PlayerStart, OrderBoxes(board.Boxes), default);
            
            if (Solved(initial, goals))
            {
                return new HintResult(HintOutcome.AlreadySolved);
            }
            
            var visited = new HashSet<State> { initial };
            var queue = new Queue<State>();
            queue.Enqueue(initial);
            
            while (queue.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                State state = queue.Dequeue();
                
                foreach (State next in Successors(state, board, walls, token))
                {
                    if (visited.Contains(next))
                    {
                        continue;
                    }
                    
                    if (visited.Count >= stateBudget)
                    {
                        return new HintResult(HintOutcome.BudgetExhausted);
                    }
                    
                    visited.Add(next);
                    
                    if (Solved(next, goals))
                    {
                        return new HintResult(HintOutcome.Found, next.FirstMove);
                    }
                    
                    queue.Enqueue(next);
                }
                stateExpanded?.Invoke();
            }
            
            return new HintResult(HintOutcome.NoSolution);
        }

        private static IEnumerable<State> Successors(State state, LevelDefinition board, HashSet<GridPosition> walls, CancellationToken token)
        {
            foreach (MoveCommand move in Moves)
            {
                token.ThrowIfCancellationRequested();
                
                if (TrySuccessor(state, move, board, walls, out State next))
                {
                    yield return next;
                }
            }
        }

        private static bool TrySuccessor(State state, MoveCommand move, LevelDefinition board, HashSet<GridPosition> walls, out State next)
        {
            next = null;
            
            if (TryStep(state.Player, move, board, walls, out GridPosition destination) == false)
            {
                return false;
            }
            
            GridPosition[] boxes = state.Boxes;
            int boxIndex = Array.IndexOf(boxes, destination);
            
            if (boxIndex >= 0 && TryPush(boxes, boxIndex, move, board, walls, out boxes) == false)
            {
                return false;
            }
            
            next = new State(destination, boxes, state.FirstMove == default ? move : state.FirstMove);
            return true;
        }

        private static bool TryPush(GridPosition[] boxes, int boxIndex, MoveCommand move, LevelDefinition board, HashSet<GridPosition> walls, out GridPosition[] pushedBoxes)
        {
            pushedBoxes = null;
            
            if (TryStep(boxes[boxIndex], move, board, walls, out GridPosition destination) == false || Array.IndexOf(boxes, destination) >= 0)
            {
                return false;
            }
            
            var changed = (GridPosition[])boxes.Clone();
            changed[boxIndex] = destination;
            pushedBoxes = OrderBoxes(changed);
            
            return true;
        }
        
        private static GridPosition[] OrderBoxes(IEnumerable<GridPosition> boxes)
        {
            return boxes
                .OrderBy(position => position.X)
                .ThenBy(position => position.Y)
                .ToArray();
        }

        private static bool TryStep(GridPosition from, MoveCommand move, LevelDefinition board, HashSet<GridPosition> walls, out GridPosition destination)
        {
            long x = from.X;
            long y = from.Y;
            
            switch (move)
            {
                case MoveCommand.Up: y++; break;
                case MoveCommand.Down: y--; break;
                case MoveCommand.Left: x--; break;
                case MoveCommand.Right: x++; break;
            }
            
            destination = default;
            
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            {
                return false;
            }
            
            destination = new GridPosition((int)x, (int)y);
            return walls.Contains(destination) == false;
        }

        private static bool Solved(State state, HashSet<GridPosition> goals) =>
            goals.All(goal => Array.IndexOf(state.Boxes, goal) >= 0);

        private sealed class State : IEquatable<State>
        {
            public GridPosition Player { get; }
            public GridPosition[] Boxes { get; }
            public MoveCommand FirstMove { get; }

            public State(GridPosition player, GridPosition[] boxes, MoveCommand firstMove)
            {
                Player = player;
                Boxes = boxes;
                FirstMove = firstMove;
            }

            // FirstMove is path metadata, not part of the board's identity.
            public bool Equals(State other) => other != null && Player == other.Player && Boxes.SequenceEqual(other.Boxes);
            public override bool Equals(object obj) => obj is State other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Player.GetHashCode();
                    foreach (GridPosition box in Boxes) hash = (hash * 397) ^ box.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
