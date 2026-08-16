using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A non-blocking, event-driven wrapper over a <see cref="ValueTask{T}"/>: observes the task without
/// awaiting it and raises <see cref="OnReady"/> when it completes, integrating with signal-driven
/// loops (for example, a <see cref="FanInSlim"/> slot). Mirrors the <see cref="EventEnumerator{T}"/>
/// model: <see cref="Observe"/>/<see cref="GetState"/>/<see cref="GetResult"/>/<see cref="Exception"/>.
/// </summary>
/// <typeparam name="T">The type of the task result.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Observe"/> consumes the supplied <see cref="ValueTask{T}"/> — do not await it elsewhere.
/// Only one operation may be observed at a time; observing while the previous task is still in flight
/// throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Subscribe to <see cref="OnReady"/> before the first <see cref="Observe"/>: the continuation is
/// captured at observe time (a subscription added later skips a task already in flight). Without a
/// subscriber, <see cref="GetState"/> and <see cref="GetResult"/> are polled.
/// </para>
/// <para>
/// The outcome is resolved lazily (at most once): <see cref="GetState"/> reflects
/// <see cref="EventTaskState.Pending()"/>, <see cref="EventTaskState.CompletedSuccessfully"/>,
/// <see cref="EventTaskState.Canceled()"/> or <see cref="EventTaskState.Faulted()"/>;
/// <see cref="GetResult"/> returns the value or rethrows the outcome.
/// </para>
/// <para>
/// This type is not thread-safe: <see cref="Observe"/>, <see cref="GetState"/>, <see cref="GetResult"/> and
/// <see cref="Exception"/> must be accessed from a single thread, while <see cref="OnReady"/> may be
/// raised from any thread.
/// </para>
/// </remarks>
public sealed class EventTask<T>
{
    private ValueTask<T> _task;
    private EventTaskState _state;
    private T _result = default!;
    private Exception? _exception;

    /// <summary>
    /// Raised when the observed <see cref="ValueTask{T}"/> completes. Subscribe before the first
    /// <see cref="Observe"/> to observe every completion.
    /// </summary>
    [PublicAPI]
    public event Action? OnReady;

    /// <summary>
    /// Starts observing a <see cref="ValueTask{T}"/> without awaiting it: registers
    /// <see cref="OnReady"/> as its continuation when a subscriber is present.
    /// </summary>
    /// <param name="task">The task to observe (consumed by this call).</param>
    /// <exception cref="InvalidOperationException">The previous task is still in flight.</exception>
    [PublicAPI]
    public void Observe(ValueTask<T> task)
    {
        if (_state.IsPending)
            throw new InvalidOperationException("The previous task is still in flight.");

        _task = task;

        AsyncMarshal.FireUnsafeOnCompleted(_task, OnReady);

        _state = EventTaskState.Pending();
    }

    /// <summary>
    /// Gets the state of the observed task, resolving it lazily from the in-flight operation.
    /// </summary>
    /// <remarks>
    /// If <see cref="Observe"/> has not been called yet, the default value is returned, for which none
    /// of the state flags is set.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventTaskState GetState()
    {
        if (_state == default)
            return _state;

        if (_state.IsPending)
            Resolve();

        return _state;
    }

    /// <summary>
    /// Gets the resolved result of the observed task; rethrows a cancellation or the captured fault.
    /// </summary>
    /// <exception cref="InvalidOperationException">The observed task has not completed yet.</exception>
    /// <exception cref="OperationCanceledException">The observed task was canceled.</exception>
    /// <exception cref="Exception">The observed task faulted; the captured source exception is rethrown.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResult()
    {
        var state = GetState();

        if (state.IsCompletedSuccessfully)
            return _result;

        if (state.IsPending)
            throw new InvalidOperationException("The task has not completed yet.");

        if (state.IsCanceled)
            throw new OperationCanceledException("The task was canceled.");

        if (state.IsFaulted)
            throw _exception!;

        throw new InvalidOperationException("No value is available in the current state.");
    }

    /// <summary>Gets the exception captured from a faulted task, if any.</summary>
    [PublicAPI]
    public Exception? Exception => _exception;

    /// <summary>Resolves the task outcome lazily, mirroring <see cref="EventEnumerator{T}"/>.</summary>
    private void Resolve()
    {
        if (!_task.IsCompleted)
        {
            _state = EventTaskState.Pending();
            return;
        }

        if (_task.IsCompletedSuccessfully)
        {
            _result = _task.GetAwaiter().GetResult();
            _state = EventTaskState.CompletedSuccessfully();
            return;
        }

        _result = default!;

        if (_task.IsFaulted)
        {
            _exception = _task.AsTask().Exception?.InnerException;
            _state = EventTaskState.Faulted();
            return;
        }

        _state = EventTaskState.Canceled();
    }
}

/// <summary>
/// Observes a <see cref="ValueTask"/> without awaiting it, exposing completion readiness through an
/// <c>OnReady</c> event and a lightweight state machine.
/// </summary>
public sealed class EventTask
{
    private ValueTask _task;
    private EventTaskState _state;
    private Exception? _exception;

    /// <summary>
    /// Raised when the observed <see cref="ValueTask"/> completes. Subscribe before the first
    /// <see cref="Observe"/> to observe every completion.
    /// </summary>
    [PublicAPI]
    public event Action? OnReady;

    /// <summary>
    /// Starts observing a <see cref="ValueTask"/> without awaiting it: registers
    /// <see cref="OnReady"/> as its continuation when a subscriber is present.
    /// </summary>
    /// <param name="task">The task to observe (consumed by this call).</param>
    /// <exception cref="InvalidOperationException">The previous task is still in flight.</exception>
    [PublicAPI]
    public void Observe(ValueTask task)
    {
        if (_state.IsPending)
            throw new InvalidOperationException("The previous task is still in flight.");

        _task = task;

        AsyncMarshal.FireUnsafeOnCompleted(_task, OnReady);

        _state = EventTaskState.Pending();
    }

    /// <summary>
    /// Gets the state of the observed task, resolving it lazily from the in-flight operation.
    /// </summary>
    /// <remarks>
    /// If <see cref="Observe"/> has not been called yet, the default value is returned, for which none
    /// of the state flags is set.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventTaskState GetState()
    {
        if (_state == default)
            return _state;

        if (_state.IsPending)
            Resolve();

        return _state;
    }
    
    /// <summary>Gets the exception captured from a faulted task, if any.</summary>
    [PublicAPI]
    public Exception? Exception => _exception;

    /// <summary>Resolves the task outcome lazily, mirroring <see cref="EventEnumerator{T}"/>.</summary>
    private void Resolve()
    {
        if (!_task.IsCompleted)
        {
            _state = EventTaskState.Pending();
            return;
        }

        if (_task.IsCompletedSuccessfully)
        {
            _task.GetAwaiter().GetResult();
            _state = EventTaskState.CompletedSuccessfully();
            return;
        }
        
        if (_task.IsFaulted)
        {
            _exception = _task.AsTask().Exception?.InnerException;
            _state = EventTaskState.Faulted();
            return;
        }

        _state = EventTaskState.Canceled();
    }
}
