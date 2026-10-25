using System.Collections.Generic;

namespace Sokoban.Core.Components
{
    internal sealed class MoveHistory
    {
        public Stack<CommittedMove> Entries { get; } = new();
    }
}
