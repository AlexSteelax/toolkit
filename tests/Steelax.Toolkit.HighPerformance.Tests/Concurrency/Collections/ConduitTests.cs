using JetBrains.Annotations;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

/// <summary>
/// Unit tests for the <see cref="Conduit{T}"/> bound SPSC transfer core.
/// </summary>
public static partial class ConduitTests
{
    private sealed class SequenceState
    {
        [PublicAPI]
        public long Count
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        public void Increment() => Count++;
    }
    
    private static async Task WriteSequence<T>(Conduit<T> conduit, IEnumerable<T> sequence, bool fallback, CancellationToken cancellationToken, SequenceState? state = null)
    {
        // await using var ctr = cancellationToken.Register(() => conduit.TryTerminate(new OperationCanceledException(cancellationToken)));
        state ??= new SequenceState();
        
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
                    
                    state.Increment();
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
    private static List<int> ReadAll(Conduit<int> conduit, SequenceState? state = null)
    {
        var result = new List<int>();
        state ??= new SequenceState();

        while (true)
        {
            if (conduit.TryRead(out var value))
            {
                result.Add(value);
                state.Increment();
                continue;
            }

            if (conduit.IsCompleted)
                break;
        }

        return result;
    }

    /// <summary>Drains the conduit via the await API until the stream completes.</summary>
    private static async Task<List<int>> ReadAllAsync(Conduit<int> conduit, SequenceState? state = null)
    {
        var result = new List<int>();
        state ??= new SequenceState();
        
        while (true)
        {
            if (conduit.TryRead(out var value))
            {
                result.Add(value);
                state.Increment();
                continue;
            }

            // WaitToReadAsync returns false when the stream has ended.
            if (!await conduit.WaitToReadAsync())
                break;
        }

        return result;
    }
}
