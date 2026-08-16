using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// Provides non-blocking, callback-driven access to an <see cref="IAsyncEnumerator{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the enumerated elements.</typeparam>
/// <remarks>
/// <para>
/// The underlying enumerator is driven one step at a time via <see cref="MoveNext"/>. Each step is
/// started without awaiting; its outcome is later exposed through <see cref="GetState"/> and
/// <see cref="GetResult"/>.
/// </para>
/// <para>
/// The <see cref="OnReady"/> event is raised whenever an in-flight <c>MoveNextAsync</c> operation
/// completes, which makes this type suitable for wiring an <see cref="IAsyncEnumerator{T}"/> into a
/// signal-driven loop (for example, a <see cref="FanInSlim"/> slot).
/// </para>
/// <para>
/// This type is not thread-safe: <see cref="MoveNext"/>, <see cref="GetState"/> and
/// <see cref="GetResult"/> must be accessed from a single thread, while <see cref="OnReady"/> may be
/// raised from any thread. Subscribe to <see cref="OnReady"/> before the first <see cref="MoveNext"/>.
/// </para>
/// </remarks>
public sealed class EventEnumerator<T> : IAsyncDisposable
{
    private readonly IAsyncEnumerator<T> _enumerator;

    private ValueTask<bool> _next;
    private EventEnumeratorState _state;
    private T _result = default!;
    private Exception? _exception;

    internal EventEnumerator(IAsyncEnumerator<T> enumerator)
    {
        _enumerator = enumerator;
    }

    /// <summary>
    /// Raised whenever an in-flight <c>MoveNextAsync</c> operation completes. Subscribe before the
    /// first <see cref="MoveNext"/> to observe every outcome; a subscription added later skips the
    /// iteration already in flight.
    /// </summary>
    public event Action? OnReady;

    /// <summary>
    /// Starts the next iteration of the underlying enumerator without blocking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new iteration is started only when no iteration has been started yet, or when the previous
    /// iteration completed with a value (<see cref="GetState"/> is <see cref="EventEnumeratorState.CompletedSuccessfully"/>).
    /// </para>
    /// <para>
    /// Calling <see cref="MoveNext"/> while the current iteration is still in flight, or after a
    /// canceled, faulted or completed iteration that has not been consumed, throws
    /// <see cref="InvalidOperationException"/> — the protocol is to advance only after the previous
    /// value has been read.
    /// </para>
    /// <para>
    /// When the started operation completes, the completion callback is invoked and the resulting
    /// state can be observed via <see cref="GetState"/>.
    /// </para>
    /// </remarks>
    [PublicAPI]
    public void MoveNext()
    {
        if (_state == default || _state.IsCompletedSuccessfully)
        {
            #pragma warning disable CA2012
            _next = _enumerator.MoveNextAsync();
            #pragma warning restore CA2012

            AsyncMarshal.FireUnsafeOnCompleted(_next, OnReady);

            _state = EventEnumeratorState.Pending();
        }
        else
            throw new InvalidOperationException("The enumerator cannot advance before the current iteration is consumed.");
    }

    private void AwaitResolve()
    {
        if (_next is not { IsCompleted: true })
        {
            _state = EventEnumeratorState.Pending();
            return;
        }

        if (_next is { IsCompletedSuccessfully: true })
        {
            var continuable = _next.GetAwaiter().GetResult();

            if (continuable)
            {
                _result = _enumerator.Current;
                _state = EventEnumeratorState.CompletedSuccessfully();
                return;
            }

            _result = default!;
            _state = EventEnumeratorState.EndOfStream();
            return;
        }

        _result = default!;

        if (_next.IsFaulted)
        {
            // The failure is captured without rethrowing; the allocation is irrelevant on this break path.
            _exception = _next.AsTask().Exception?.InnerException;
            _state = EventEnumeratorState.Faulted();
            return;
        }

        // A canceled operation leaves no exception; this mirrors the behavior of Task.Exception.
        _state = EventEnumeratorState.Canceled();
    }

    /// <summary>
    /// Gets the state of the current iteration, resolving it lazily from the in-flight operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the current operation is still in flight, the state is <see cref="EventEnumeratorState.Pending()"/>.
    /// </para>
    /// <para>
    /// If <see cref="MoveNext"/> has not been called yet, the default value is returned, for which
    /// none of the state flags is set.
    /// </para>
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventEnumeratorState GetState()
    {
        if (_state == default)
            return _state;

        if (_state.IsPending)
            AwaitResolve();

        return _state;
    }

    /// <summary>
    /// Gets the value produced by the current iteration.
    /// </summary>
    /// <returns>The value of the current element.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current operation is still pending, or no value is available
    /// (for example, the enumerator completed without producing a value).
    /// </exception>
    /// <exception cref="OperationCanceledException">The current operation was canceled.</exception>
    /// <exception cref="Exception">The current operation faulted; the captured source exception is rethrown.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResult()
    {
        var state = GetState();
            
        if (state.IsCompletedSuccessfully)
            return _result;

        if (state.IsPending)
            throw new InvalidOperationException("The current operation has not completed yet.");
            
        if (state.IsCanceled)
            throw new OperationCanceledException("The current operation was canceled.");

        if (state.IsFaulted)
            throw _exception!;

        throw new InvalidOperationException("No value is available in the current state.");
    }

    /// <summary>
    /// Gets the exception captured from a faulted operation, if any.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the current operation has not faulted.
    /// </remarks>
    [PublicAPI]
    public Exception? Exception => _exception;

    /// <summary>
    /// Releases the underlying async enumerator.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        return _enumerator.DisposeAsync();
    }
}

