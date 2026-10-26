namespace Simple_QR_Code_Maker.Helpers;

public static class AsyncBatchProcessor
{
    public static async Task RunAsync<T>(
        IReadOnlyList<T> items,
        int maxConcurrency,
        Func<T, Task> process,
        Action<int>? updateProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        ArgumentNullException.ThrowIfNull(process);
        using SemaphoreSlim semaphore = new(maxConcurrency, maxConcurrency);
        int completed = 0;
        await Task.WhenAll(items.Select(ProcessAsync));

        async Task ProcessAsync(T item)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await process(item);
            }
            finally
            {
                try
                {
                    if (updateProgress is not null)
                    {
                        lock (semaphore)
                            updateProgress(++completed);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }
    }
}
