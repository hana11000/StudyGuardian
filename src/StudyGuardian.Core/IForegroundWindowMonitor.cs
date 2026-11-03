namespace StudyGuardian.Core;

public interface IForegroundWindowMonitor : IDisposable
{
    WindowSnapshot Current { get; }
    event EventHandler<WindowSnapshot>? Changed;
    void Start();
    void Stop();
}

public interface IIdleTimeProvider
{
    TimeSpan GetIdleTime();
}
