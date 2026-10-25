using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Core.Tests.Workflows;

[TestClass]
public sealed class AsyncBatchProcessorTests
{
    [TestMethod]
    public async Task ConcurrencyIsBoundedAndProgressIsMonotonic()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource full = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0;
        int maximum = 0;
        List<int> progress = [];
        Task batch = AsyncBatchProcessor.RunAsync(Enumerable.Range(0, 8).ToArray(), 2, async _ =>
        {
            int current = Interlocked.Increment(ref active);
            lock (progress)
                maximum = Math.Max(maximum, current);
            if (current == 2)
                full.TrySetResult();
            try
            {
                await release.Task;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }, count => progress.Add(count));

        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(2, active);
        }
        finally
        {
            release.TrySetResult();
            await batch.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.AreEqual(2, maximum);
        CollectionAssert.AreEqual(Enumerable.Range(1, 8).ToArray(), progress);
    }

    [TestMethod]
    public async Task WorkAndProgressFailuresReleaseSlotsAndPropagate()
    {
        List<int> processed = [];
        Task failure = AsyncBatchProcessor.RunAsync(new[] { 0, 1, 2 }, 1, value =>
        {
            processed.Add(value);
            return value == 0 ? Task.FromException(new IOException("synthetic failure")) : Task.CompletedTask;
        });
        await Assert.ThrowsExactlyAsync<IOException>(() => failure.WaitAsync(TimeSpan.FromSeconds(10)));
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, processed);

        Task progressFailure = AsyncBatchProcessor.RunAsync(new[] { 0, 1, 2 }, 1,
            _ => Task.CompletedTask, count =>
            {
                if (count == 1)
                    throw new InvalidOperationException("synthetic progress failure");
            });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => progressFailure.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public async Task PreCancellationDoesNotStartWork()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        int processed = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() => AsyncBatchProcessor.RunAsync(
            new[] { 1, 2 }, 2, _ => { processed++; return Task.CompletedTask; }, cancellationToken: cancellation.Token));
        Assert.AreEqual(0, processed);
    }

    [TestMethod]
    public async Task EmptyBatchAndInvalidConcurrencyAreExplicit()
    {
        await AsyncBatchProcessor.RunAsync(Array.Empty<int>(), 1, _ => throw new AssertFailedException("Should not run."));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => AsyncBatchProcessor.RunAsync(
            new[] { 1 }, 0, _ => Task.CompletedTask));
    }
}
