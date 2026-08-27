namespace Steelax.Toolkit.HighPerformance.Concurrency.Collections;

/// <summary>
/// 
/// </summary>
[Flags]
public enum ConduitBehavior
{
    /// <summary>
    /// 
    /// </summary>
    Default = 0,
    
    /// <summary>
    /// 
    /// </summary>
    AwaitableReader,
    
    /// <summary>
    /// 
    /// </summary>
    AwaitableWriter
}