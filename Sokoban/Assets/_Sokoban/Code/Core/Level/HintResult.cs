using Sokoban.Core.Commands;

namespace Sokoban.Core
{
    public readonly struct HintResult
    {
        public HintOutcome Outcome { get; }
        public MoveCommand Move { get; }

        public HintResult(HintOutcome outcome, MoveCommand move = default)
        {
            Outcome = outcome;
            Move = move;
        }
    }
}
