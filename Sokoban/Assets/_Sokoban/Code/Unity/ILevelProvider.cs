using Sokoban.Core;
namespace Sokoban.Unity
{
    public interface ILevelProvider
    {
        LevelDefinition GetLevel();
    }
}
