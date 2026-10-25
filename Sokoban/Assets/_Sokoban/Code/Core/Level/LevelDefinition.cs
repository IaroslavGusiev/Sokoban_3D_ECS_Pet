using System;
using System.Linq;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class LevelDefinition
    {
        public int Width { get; }
        public int Height { get; }
        public GridPosition PlayerStart { get; }
        public IReadOnlyList<GridPosition> Walls { get; }
        public IReadOnlyList<GridPosition> Boxes { get; }
        public IReadOnlyList<GridPosition> Goals { get; }

        public LevelDefinition(
            int width, 
            int height, 
            GridPosition playerStart,
            IEnumerable<GridPosition> walls, 
            IEnumerable<GridPosition> boxes, 
            IEnumerable<GridPosition> goals)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            
            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            PlayerStart = playerStart;
            
            Walls = Array.AsReadOnly((walls ?? throw new ArgumentNullException(nameof(walls))).ToArray());
            Boxes = Array.AsReadOnly((boxes ?? throw new ArgumentNullException(nameof(boxes))).ToArray());
            Goals = Array.AsReadOnly((goals ?? throw new ArgumentNullException(nameof(goals))).ToArray());
        }
    }
}
