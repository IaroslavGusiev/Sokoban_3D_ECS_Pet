using System.Linq;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public static class LevelParser
    {
        public static LevelDefinition Parse(string text)
        {
            if (text == null)
            {
                throw new LevelFormatException("level: provide level text with at least one row.");
            }

            string[] rows = text.Replace("\r\n", "\n").Split('\n');
            int height = rows.Length;
            
            while (height > 0 && rows[height - 1].Length == 0)
            {
                height--;
            }

            if (height == 0)
            {
                throw new LevelFormatException("level: add at least one row.");
            }

            int width = rows[0].Length;
            var walls = new List<GridPosition>();
            var boxes = new List<GridPosition>();
            var goals = new List<GridPosition>();
            
            GridPosition player = default;
            var playerCount = 0;

            for (var row = 0; row < height; row++)
            {
                string line = rows[row];
                
                if (line.Length == 0)
                {
                    throw new LevelFormatException($"line {row + 1}: remove the blank line inside the level.");
                }

                if (line.Length != width)
                {
                    throw new LevelFormatException($"line {row + 1}: use {width} cells, matching the first row; found {line.Length}.");
                }

                for (var column = 0; column < width; column++)
                {
                    var position = new GridPosition(column, height - 1 - row);
                    
                    switch (line[column])
                    {
                        case '#':
                            walls.Add(position);
                            break;
                        case '-':
                            break;
                        case '@':
                            player = position;
                            playerCount++;
                            break;
                        case '$':
                            boxes.Add(position);
                            break;
                        case '.':
                            goals.Add(position);
                            break;
                        case '*':
                            boxes.Add(position);
                            goals.Add(position);
                            break;
                        case '+':
                            player = position;
                            playerCount++;
                            goals.Add(position);
                            break;
                        default:
                            string character = line[column] switch
                            {
                                '\t' => "\\t",
                                '\r' => "\\r",
                                _ => line[column].ToString()
                            };
                            throw new LevelFormatException($"line {row + 1}: column {column + 1} contains '{character}'; use only # - @ $ . * + (no spaces).");
                    }
                }
            }

            ValidatePieces(playerCount, boxes, goals);
            ValidateEnclosure(width, height, player, walls);
            return new LevelDefinition(width, height, player, walls, boxes, goals);
        }

        private static void ValidatePieces(int playerCount, List<GridPosition> boxes, List<GridPosition> goals)
        {
            if (playerCount != 1)
            {
                throw new LevelFormatException($"level: place exactly one player (@ or +); found {playerCount}.");
            }

            if (boxes.Count == 0)
            {
                throw new LevelFormatException("level: add at least one box ($ or *).");
            }

            if (boxes.Count != goals.Count)
            {
                throw new LevelFormatException($"level: use the same number of boxes and goals; found {boxes.Count} boxes and {goals.Count} goals.");
            }

            bool alreadySolved = boxes.All(goals.Contains);

            if (alreadySolved)
            {
                throw new LevelFormatException("level: the level is already solved; leave at least one box off its goal.");
            }
        }

        private static void ValidateEnclosure(int width, int height, GridPosition player, IEnumerable<GridPosition> walls)
        {
            var wallCells = new HashSet<GridPosition>(walls);
            var visited = new HashSet<GridPosition> { player };
            var pending = new Queue<GridPosition>();
            
            pending.Enqueue(player);

            while (pending.Count > 0)
            {
                GridPosition cell = pending.Dequeue();
                
                if (cell.X == 0 || cell.X == width - 1 || cell.Y == 0 || cell.Y == height - 1)
                {
                    throw new LevelFormatException("level: the playable area touches the grid edge; close it with walls (#) so the player and boxes cannot leave.");
                }

                foreach (GridPosition next in new[]
                {
                    new GridPosition(cell.X, cell.Y + 1),
                    new GridPosition(cell.X, cell.Y - 1),
                    new GridPosition(cell.X - 1, cell.Y),
                    new GridPosition(cell.X + 1, cell.Y)
                })
                {
                    if (wallCells.Contains(next) == false && visited.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }
        }
    }
}
