using System.Threading.Tasks.Sources;
using Xunit;

namespace Steelax.Toolkit.HighPerformance.Exploration;

/// <summary>
///     Инварианты <see cref="ManualResetValueTaskSourceCore{TResult}" /> относительно
///     поведения <see cref="ManualResetValueTaskSourceCore{TResult}.Reset" />: что происходит при
///     двойном Reset без промежуточного потребления, как меняется версия, и что даёт
///     SetResult/GetResult с разными токенами.
/// </summary>
public class ManualResetValueTaskSourceCoreResetTests
{
    [Fact]
    public void Version_IncrementsOnReset()
    {
        var core = new ManualResetValueTaskSourceCore<bool>();

        var v0 = core.Version;
        core.Reset();
        var v1 = core.Version;
        core.Reset();
        var v2 = core.Version;

        Assert.True(v0 != v1, $"version should change after first Reset: {v0} -> {v1}");
        Assert.True(v1 != v2, $"version should change after second Reset: {v1} -> {v2}");
    }

    [Fact]
    public void Reset_TwiceWithoutConsumption_ThenSetResult()
    {
        var core = new ManualResetValueTaskSourceCore<bool>();

        // Двойной Reset без GetResult между ними (контрактно запрещено, наблюдаем поведение).
        core.Reset();
        core.Reset();
        var version = core.Version;

        // После двойного Reset core формально "готов": SetResult не должен бросить,
        // а актуальный токен (после последнего Reset) корректно завершается.
        core.SetResult(true);
        Assert.Equal(ValueTaskSourceStatus.Succeeded, core.GetStatus(version));
        Assert.True(core.GetResult(version));
    }

    [Fact]
    public void Reset_AfterSetResultWithoutConsumption_ThenSetResultAgain()
    {
        var core = new ManualResetValueTaskSourceCore<bool>();

        // Завершили, НЕ потребляя результат, затем Reset, затем снова SetResult —
        // имитация гонки waiter/продюсер в CompleteSignal.
        var staleToken = core.Version;
        core.SetResult(true);
        core.Reset();
        var version = core.Version;
        core.SetResult(false);

        // Свежий токен (после Reset) должен завершиться новым значением.
        Assert.Equal(ValueTaskSourceStatus.Succeeded, core.GetStatus(version));
        Assert.False(core.GetResult(version));

        // Старый токен (до Reset) неактуален: GetStatus/GetResult с ним бросают.
        Assert.ThrowsAny<InvalidOperationException>(() => core.GetStatus(staleToken));
        Assert.ThrowsAny<InvalidOperationException>(() => core.GetResult(staleToken));
    }

    [Fact]
    public void GetResult_StaleToken_DoesNotMatchCurrentVersion()
    {
        var core = new ManualResetValueTaskSourceCore<bool>();

        var v0 = core.Version;
        core.SetResult(true);
        core.Reset();
        core.SetResult(false);

        // Потребление по старому токену после нового цикла — должно бросить (version mismatch).
        Assert.ThrowsAny<InvalidOperationException>(() => core.GetResult(v0));
    }

    [Fact]
    public void SetResult_OnAlreadyCompletedSameVersion_Throws()
    {
        var core = new ManualResetValueTaskSourceCore<bool>();

        core.SetResult(true);

        // Второй SetResult без Reset между ними — двойное завершение одной версии.
        Assert.ThrowsAny<InvalidOperationException>(() => core.SetResult(false));
    }
}