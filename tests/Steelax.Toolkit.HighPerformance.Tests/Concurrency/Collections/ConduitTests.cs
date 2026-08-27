using Steelax.Toolkit.HighPerformance.Concurrency.Collections;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

/// <summary>
/// Unit tests for the <see cref="Conduit{T}"/> bound SPSC transfer core.
/// </summary>
public static partial class ConduitTests
{

    private static async Task WriteSequence<T>(Conduit<T> conduit, IEnumerable<T> sequence, bool fallback, CancellationToken cancellationToken)
    {
        await using var ctr = cancellationToken.Register(() => conduit.TryTerminate(new OperationCanceledException(cancellationToken)));
        
        await Task.Run(async () =>
        {
            try
            {
                foreach (var item in sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    while (!conduit.TryWrite(item))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (fallback)
                        {
                            Thread.Yield();

                            if (conduit.IsCompleted)
                                break;
                        }
                        else
                        {
                            if (!await conduit.WaitToWriteAsync())
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _ = conduit.TryComplete(ex);
            }
            finally
            {
                _ = conduit.TryComplete();
            }
        }, cancellationToken);
    }
    
    /// <summary>Drains the conduit via a busy loop until the stream completes.</summary>
    private static List<int> ReadAll(Conduit<int> conduit)
    {
        var result = new List<int>();

        while (true)
        {
            if (conduit.TryRead(out var value))
            {
                result.Add(value);
                continue;
            }

            if (conduit.IsCompleted)
                break;
        }

        return result;
    }

    /// <summary>Drains the conduit via the await API until the stream completes.</summary>
    private static async Task<List<int>> ReadAllAsync(Conduit<int> conduit)
    {
        var result = new List<int>();

        while (true)
        {
            if (conduit.TryRead(out var value))
            {
                result.Add(value);
                continue;
            }

            // WaitToReadAsync returns false when the stream has ended.
            if (!await conduit.WaitToReadAsync())
                break;
        }

        return result;
    }
}
