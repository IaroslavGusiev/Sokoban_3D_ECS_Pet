using DefaultEcs;
using Sokoban.Presentation.Ecs.Model;

namespace Sokoban.Tests.PlayMode
{
    public readonly struct TestViewLifetimeMarker { }

    public sealed class TestViewLifetimeAdapter : EntityAdapter
    {
        public int InstallCalls;
        public int UninstallCalls;
        public int DisableCalls;
        public int DestroyCalls;

        public override void Install(Entity entity)
        {
            InstallCalls++;
            entity.Set<TestViewLifetimeMarker>();
        }

        public override void Uninstall(Entity entity)
        {
            UninstallCalls++;
            entity.Remove<TestViewLifetimeMarker>();
        }

        private void OnDisable() =>
            DisableCalls++;

        private void OnDestroy() =>
            DestroyCalls++;
    }
}
