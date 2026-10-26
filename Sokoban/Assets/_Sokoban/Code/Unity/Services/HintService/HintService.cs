using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core;

namespace Sokoban.Presentation
{
    public sealed class HintService : IHintService
    {
        public const int StateBudget = 100_000;
        private readonly HintSolver _solver;

        public HintService(HintSolver solver) =>
            _solver = solver;

        public Task<HintResult> CalculateAsync(LevelDefinition board, CancellationToken token) =>
            Task.Run(() => _solver.Solve(board, StateBudget, token), token);
    }
}
