using System;

namespace Sokoban.Core.Components
{
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public bool Equals(GridPosition other) => 
            X == other.X && Y == other.Y;

        public override bool Equals(object obj) => 
            obj is GridPosition other && Equals(other);

        public static bool operator ==(GridPosition left, GridPosition right) => 
            left.Equals(right);

        public static bool operator !=(GridPosition left, GridPosition right) => 
            left.Equals(right) == false;
        
        public static GridPosition operator +(GridPosition left, GridPosition right) =>
            new(left.X + right.X, left.Y + right.Y);

    }
}
