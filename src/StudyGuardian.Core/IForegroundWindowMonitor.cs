namespace StudyGuardian.Core;

public interface IForegroundWindowMonitor : IDisposable
{
    WindowSnapshot Current { get; }
    event EventHandler<WindowSnapshot>? Changed;
    void Start();
    void Stop();
    WindowSnapshot Refresh();
}

public interface IIdleTimeProvider
{
    TimeSpan GetIdleTime();
}
