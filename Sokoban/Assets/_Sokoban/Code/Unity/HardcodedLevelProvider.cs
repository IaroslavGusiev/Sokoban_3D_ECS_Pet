using Sokoban.Core;
namespace Sokoban.Unity
{
    public sealed class HardcodedLevelProvider : ILevelProvider
    {
        public LevelDefinition GetLevel() => 
            HardcodedLevel.Create();
    }
}
