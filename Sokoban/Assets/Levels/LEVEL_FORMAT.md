# Sokoban text levels

Each UTF-8 `.txt` file is one rectangular level. Use exactly one character per cell, without indentation, spaces, tabs, a title, or comments in the level file.

| Character | Cell contents |
| --- | --- |
| `#` | Wall |
| `-` | Empty floor |
| `@` | Player on floor |
| `$` | Box on floor |
| `.` | Empty goal |
| `*` | Box on a goal |
| `+` | Player on a goal |

The first line is the top of the board. For zero-based `lineIndex` and `columnIndex`, `x = columnIndex` and `y = Height - 1 - lineIndex`. Up increases y; Unity projects grid (x, y) to world (x, 0, y).

## Validation rules

- Include at least one nonempty row. Every row must have the same number of characters.
- Include exactly one player: count `@` and `+` together.
- Include at least one box: count `$` and `*` together.
- Match the total number of boxes to goals. Goals are `.`, `*`, and `+`.
- Use only the seven characters above. A space is invalid, including leading/trailing spaces. Rows are not trimmed.
- Do not put empty lines inside the level. Trailing empty lines are ignored. Both LF and CRLF line endings are accepted.
- Enclose the player-reachable non-wall area. Flood fill treats floors, goals, and boxes as passable. If it reaches any grid edge, parsing fails with `level: the playable area touches the grid edge; close it with walls (#) so the player and boxes cannot leave.` Unreachable edge cells do not count as openings.

Example (`level-01.txt`):

```text
#######
#-----#
#-.-.-#
#-$-$-#
#--@--#
#-----#
#######
```

All cells imply floor; there is no separate floor list. The parser validates shape, symbol counts, and playable-area enclosure, but not solvability or deadlocks. For authored levels, use a closed wall border. The test solver searches cells inside the rectangle; ECS movement itself retains the existing wall/box rules and does not enforce rectangle bounds.

## Adding a level

1. Create `Assets/Levels/level-NN.txt` using the format above. Prefer a small enclosed board (about 6x6 to 8x8) with 2–3 boxes and a short solution.
2. Ensure each box can reach a goal. A box in a non-goal corner cannot be recovered; adjacent boxes and narrow corridors can also block pushes.
3. Run the tests below. The file-based TestCaseSource automatically discovers every `Assets/Levels/*.txt`; no test case registration is required.
4. In Unity, mark the text file Addressable (for example, `levels/level-04`) and add its TextAsset reference to the ordered LevelList asset. The controller starts at index 0. N is a temporary developer shortcut for cycling levels; no win detection or completion-driven progression is implemented.

## Running tests

Open **Window > General > Test Runner**, select **EditMode**, select the `Sokoban.Core.Tests` assembly, and choose **Run Selected** (or Run All). Parser failures begin with `line N:` for a specific row, or `level:` for a whole-level problem, and explain what to fix. Unsupported-character errors include the offending character (tabs/carriage returns are displayed as `\t`/`\r`).

For each text file, the tests parse it, find the shortest list of directional moves with breadth-first search over player/box positions, print the move count, and replay Send/Tick through a real Simulation. Finally, every goal must hold a box. The solver lives only in the test assembly. Exceeding 2,000,000 states or exhausting the search fails with a clear “too complex or unsolvable” message; simplify or correct the level before retrying.
