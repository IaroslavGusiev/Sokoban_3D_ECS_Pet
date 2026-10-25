using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Tests.PlayMode
{
    internal sealed class TestOperation<T> : AsyncOperationBase<T>
    {
        public int DestroyCalls { get; private set; }

        protected override void Execute() { }

        public void Finish(T value) => Complete(value, true, (Exception)null);

        public void Fail(Exception exception) => Complete(default, false, exception);

        protected override void Destroy() => DestroyCalls++;
    }
}
