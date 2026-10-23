using Sokoban.Presentation.Ecs.Model;

namespace Sokoban.Tests.PlayMode
{
    [System.Serializable]
    public struct TestSkinMarker
    {
        public int Value;
    }

    public sealed class TestSkinAdapter : ComponentAdapter<TestSkinMarker>
    {
    }
}
