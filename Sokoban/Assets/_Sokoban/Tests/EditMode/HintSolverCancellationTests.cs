using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests.EditMode
{
    public sealed class HintSolverCancellationTests
    {
        [Test]
        public void Cancellation_AfterExpandedStatesCompletesTheActiveSearch()
        {
            var board = LevelParser.Parse("########\n#@-----#\n#--$--.#\n#------#\n########");
            using var cancellation = new CancellationTokenSource();
            using var expanded = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            var expansionCount = 0;
            Task<HintResult> task = Task.Run(() => new HintSolver().Solve(board, 100_000, cancellation.Token, () =>
            {
                if (Interlocked.Increment(ref expansionCount) != 3) return;
                expanded.Set();
                resume.Wait();
            }), cancellation.Token);
            try
            {
                // Timeouts only guard a broken test from hanging; the events determine ordering.
                Assert.That(expanded.Wait(TimeSpan.FromSeconds(10)), Is.True, "Search did not reach the expansion checkpoint.");
                Assert.That(Volatile.Read(ref expansionCount), Is.EqualTo(3));
                Assert.That(task.IsCompleted, Is.False);
                Assert.That(cancellation.IsCancellationRequested, Is.False);
                cancellation.Cancel();
                Assert.That(task.IsCompleted, Is.False, "The search is still held inside its expansion checkpoint.");
            }
            finally
            {
                cancellation.Cancel();
                resume.Set();
                Assert.That(((IAsyncResult)task).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True,
                    "The canceled search did not complete after the checkpoint was released.");
            }
            Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(Volatile.Read(ref expansionCount), Is.EqualTo(3));
        }
    }
}
