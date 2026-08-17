using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A lightweight handle that signals a specific <see cref="FanInSlim"/> slot, avoiding an allocation
/// when the callback is invoked directly.
/// </summary>
[PublicAPI]
public readonly struct FanInSignalCallback
{
    private readonly FanInSlim _source;
    private readonly int _index;
    
    internal FanInSignalCallback(FanInSlim source, int index)
    {
        _source = source;
        _index = index;
    }

    /// <summary>Signals the bound fan-in slot immediately.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fire() => _source?.Signal(_index);
    
    /// <summary>Gets a closure-free <see cref="Action"/> that signals the bound slot when invoked.</summary>
    [PublicAPI]
    public Action Handler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ArgumentNullException.ThrowIfNull(_source);
            
            var source = _source;
            var index = _index;
            return () => source.Signal(index);
        }
    }
}