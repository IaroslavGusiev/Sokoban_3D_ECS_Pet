using System;
using System.Linq;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class GameplaySnapshot : IEquatable<GameplaySnapshot>
    {
        public GridPosition PlayerPosition { get; }
        public IReadOnlyList<GridPosition> BoxPositions { get; }
        public int Moves { get; }
        public long BoardRevision { get; }
        public float RemainingSeconds { get; }

        private float ElapsedSeconds { get; }
        private bool IsCompleted { get; }
        private bool IsTimedOut { get; }
        private bool CanUndo { get; }

        internal GameplaySnapshot(GridPosition playerPosition, IEnumerable<GridPosition> boxes, LevelResult result,
            bool isCompleted, bool isTimedOut, bool canUndo, long boardRevision)
        {
            PlayerPosition = playerPosition;
            BoxPositions = Array.AsReadOnly(boxes.OrderBy(position => position.X).ThenBy(position => position.Y).ToArray());
            Moves = result.Moves;
            RemainingSeconds = result.RemainingSeconds;
            ElapsedSeconds = result.ElapsedSeconds;
            IsCompleted = isCompleted;
            IsTimedOut = isTimedOut;
            CanUndo = canUndo;
            BoardRevision = boardRevision;
        }

        public bool Equals(GameplaySnapshot other) =>
            other != null
            && PlayerPosition == other.PlayerPosition
            && BoxPositions.SequenceEqual(other.BoxPositions)
            && Moves == other.Moves
            && RemainingSeconds.Equals(other.RemainingSeconds)
            && ElapsedSeconds.Equals(other.ElapsedSeconds)
            && IsCompleted == other.IsCompleted
            && IsTimedOut == other.IsTimedOut
            && CanUndo == other.CanUndo
            && BoardRevision == other.BoardRevision;

        public override bool Equals(object obj) =>
            obj is GameplaySnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PlayerPosition.GetHashCode();
                
                foreach (GridPosition box in BoxPositions)
                {
                    hash = (hash * 397) ^ box.GetHashCode();
                }
                
                hash = (hash * 397) ^ Moves;
                hash = (hash * 397) ^ RemainingSeconds.GetHashCode();
                hash = (hash * 397) ^ ElapsedSeconds.GetHashCode();
                hash = (hash * 397) ^ IsCompleted.GetHashCode();
                hash = (hash * 397) ^ IsTimedOut.GetHashCode();
                hash = (hash * 397) ^ CanUndo.GetHashCode();
                return (hash * 397) ^ BoardRevision.GetHashCode();
            }
        }
    }
}
