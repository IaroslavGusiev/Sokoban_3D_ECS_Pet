using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core;

namespace Sokoban.Presentation
{
    public interface IHintService
    {
        Task<HintResult> CalculateAsync(LevelDefinition board, CancellationToken token);
    }
}
